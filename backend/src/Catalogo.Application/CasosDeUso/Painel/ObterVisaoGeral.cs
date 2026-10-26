using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Painel;

/// <summary>
/// Caso de uso: visão geral do painel de um grupo (GET /api/v1/painel/grupos/{grupo}/visao-geral?de=&amp;ate=&amp;equipeId=).
///   - Só GESTOR e ADMINISTRADOR (RN78). O que entra nos números é o que o usuário veria na listagem (RN79): o escopo vem do
///     <see cref="PreparadorConsultaRegistros"/>, o mesmo da listagem e da exportação.
///   - Blocos: volume (RN81), aprovação (RN82, só onde está ativa), avaliação (RN83, só onde há modelo ativo) e atividade (RN84).
///   - Os números são agregados no banco, entidade por entidade (<see cref="IPainelRepository"/>), e só somados aqui.
/// </summary>
public sealed class ObterVisaoGeral(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    IPainelRepository painel,
    IModeloAvaliacaoRepository modelos,
    IRegistroRepository registros,
    IEquipeRepository equipes,
    IUsuarioRepository usuarios,
    OpcoesPainel opcoes,
    TimeProvider relogio)
{
    public const int TamanhoRanking = 5;
    public const int TamanhoAtividade = 10;

    private static readonly IReadOnlySet<string> ParametrosAceitos =
        new HashSet<string>([PeriodoPainel.ParametroDe, PeriodoPainel.ParametroAte, ChavesReservadas.EquipeId], StringComparer.Ordinal);

    public async Task<VisaoGeralDto> ExecutarAsync(
        string grupoChave, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        PermissoesPainel.Exigir(usuario);
        (var grupo, usuario) = await metadados.ObterGrupoOuFalharAsync(usuario, grupoChave, cancellationToken);

        var lista = parametros.ToList();
        var erros = new ErrosPorCampo();
        foreach (var desconhecido in lista.Select(p => p.Key).Distinct().Where(k => !ParametrosAceitos.Contains(k)))
            erros.Adicionar(desconhecido, "Parâmetro desconhecido. A visão geral aceita de, ate e equipeId.");
        var periodo = PeriodoPainel.Interpretar(lista, opcoes.Fuso, erros);
        var equipeId = PermissoesPainel.LerEquipe(lista, erros);
        erros.LancarSeHouverErros();
        PermissoesPainel.ExigirEquipeVisivel(usuario, equipeId);

        // As entidades que o usuário pode ver (RN75, RN77), na ordem do menu.
        var entidades = grupo.Entidades
            .Where(e => PoliticaPermissao.PodeUsarEntidade(usuario, grupo, e, escrita: false) == DecisaoPermissao.Permitido)
            .OrderBy(e => e.Ordem).ThenBy(e => e.Chave, StringComparer.Ordinal)
            .ToList();
        var escopos = entidades.ToDictionary(e => e.Id, e => PreparadorConsultaRegistros.Escopo(usuario, e, equipeId));

        return new VisaoGeralDto(
            grupo.Chave,
            PermissoesPainel.Periodo(periodo, opcoes),
            equipeId,
            await VolumeAsync(entidades, escopos, periodo, cancellationToken),
            await AprovacaoAsync(usuario, entidades, escopos, periodo, equipeId, cancellationToken),
            await AvaliacaoAsync(usuario, entidades, escopos, equipeId, cancellationToken),
            await AtividadeAsync(usuario, entidades, periodo, equipeId, cancellationToken));
    }

    // ---- Volume (RN81) ----

    private async Task<VolumeDto> VolumeAsync(
        IReadOnlyList<Entidade> entidades, IReadOnlyDictionary<Guid, ConsultaRegistros> escopos, PeriodoPainel periodo,
        CancellationToken cancellationToken)
    {
        var porEntidade = new List<VolumeEntidadeDto>();
        var porEquipe = new Dictionary<Guid, int>();
        var porMes = new SortedDictionary<(int Ano, int Mes), int>();

        foreach (var entidade in entidades)
        {
            var volume = await painel.ResumirVolumeAsync(entidade.Id, escopos[entidade.Id], periodo, cancellationToken);
            porEntidade.Add(new VolumeEntidadeDto(entidade.Chave, entidade.Nome, volume.Total, volume.CriadosNoPeriodo, volume.AlteradosNoPeriodo));
            foreach (var equipe in volume.PorEquipe)
                porEquipe[equipe.EquipeId] = porEquipe.GetValueOrDefault(equipe.EquipeId) + equipe.Quantidade;
            foreach (var mes in volume.CriadosPorMes)
                porMes[(mes.Ano, mes.Mes)] = porMes.GetValueOrDefault((mes.Ano, mes.Mes)) + mes.Quantidade;
        }

        var nomes = (await equipes.ObterPorIdsAsync(porEquipe.Keys.ToList(), cancellationToken)).ToDictionary(e => e.Id, e => e.Nome);
        return new VolumeDto(
            porEntidade.Sum(e => e.Total),
            porEntidade.Sum(e => e.CriadosNoPeriodo),
            porEntidade.Sum(e => e.AlteradosNoPeriodo),
            porEntidade,
            porEquipe
                .Select(e => new QuantidadeEquipeDto(e.Key, nomes.GetValueOrDefault(e.Key), e.Value))
                .OrderByDescending(e => e.Quantidade).ThenBy(e => e.Nome, StringComparer.Ordinal)
                .ToList(),
            porMes.Select(m => new QuantidadeMesDto($"{m.Key.Ano:D4}-{m.Key.Mes:D2}", m.Value)).ToList());
    }

    // ---- Aprovação (RN82) ----

    /// <summary>Só as entidades por equipe e só as equipes onde a aprovação está ATIVA. Nenhuma: bloco nulo.</summary>
    private async Task<AprovacaoPainelDto?> AprovacaoAsync(
        UsuarioAtual usuario, IReadOnlyList<Entidade> entidades, IReadOnlyDictionary<Guid, ConsultaRegistros> escopos,
        PeriodoPainel periodo, Guid? equipeId, CancellationToken cancellationToken)
    {
        var porEquipe = entidades.Where(e => !e.EhGlobal).ToList();
        if (porEquipe.Count == 0)
            return null;

        var ativas = await painel.ListarAprovacoesAtivasAsync(
            porEquipe.Select(e => e.Id).ToList(), PoliticaPermissao.EquipesVisiveis(usuario), cancellationToken);
        if (equipeId is Guid filtro)
            ativas = ativas.Where(a => a.EquipeId == filtro).ToList();
        if (ativas.Count == 0)
            return null;

        var pendenteAntesDe = relogio.GetUtcNow().UtcDateTime.AddDays(-opcoes.DiasPendenteAntigo);
        var resultados = new List<(Entidade Entidade, AprovacaoEntidade Numeros)>();
        foreach (var entidade in porEquipe)
        {
            var equipesComAprovacao = ativas.Where(a => a.EntidadeId == entidade.Id).Select(a => a.EquipeId).ToHashSet();
            if (equipesComAprovacao.Count == 0)
                continue;

            // O escopo da listagem, reduzido às equipes com aprovação ativa (que já são só as visíveis).
            var escopo = escopos[entidade.Id] with { EquipesPermitidas = equipesComAprovacao };
            resultados.Add((entidade, await painel.ResumirAprovacaoAsync(entidade.Id, escopo, periodo, pendenteAntesDe, cancellationToken)));
        }

        return new AprovacaoPainelDto(
            opcoes.DiasPendenteAntigo,
            Numeros(resultados.Select(r => r.Numeros).ToList()),
            resultados.Select(r => new AprovacaoEntidadeDto(r.Entidade.Chave, r.Entidade.Nome, Numeros([r.Numeros]))).ToList());
    }

    /// <summary>
    /// Soma os números de uma ou mais entidades. O tempo médio do total é a média PONDERADA pelas primeiras decisões (igual
    /// à média de todos os tempos juntos); a taxa de rejeição, rejeições / (aprovações + rejeições).
    /// </summary>
    private static NumerosAprovacaoDto Numeros(IReadOnlyList<AprovacaoEntidade> partes)
    {
        var primeiras = partes.Sum(p => p.PrimeirasDecisoes);
        var segundos = partes.Sum(p => (p.TempoMedioPrimeiraDecisaoSegundos ?? 0) * p.PrimeirasDecisoes);
        var aprovacoes = partes.Sum(p => p.Aprovacoes);
        var rejeicoes = partes.Sum(p => p.Rejeicoes);

        return new NumerosAprovacaoDto(
            partes.Sum(p => p.PorSituacao.GetValueOrDefault(SituacaoAprovacao.Pendente)),
            partes.Sum(p => p.PorSituacao.GetValueOrDefault(SituacaoAprovacao.Aprovado)),
            partes.Sum(p => p.PorSituacao.GetValueOrDefault(SituacaoAprovacao.Rejeitado)),
            partes.Sum(p => p.PendentesAntigos),
            primeiras == 0 ? null : Math.Round(segundos / primeiras / 3600, 2),
            primeiras,
            aprovacoes,
            rejeicoes,
            aprovacoes + rejeicoes == 0 ? null : Math.Round((double)rejeicoes / (aprovacoes + rejeicoes), 4));
    }

    // ---- Avaliação (RN83) ----

    /// <summary>Só as entidades com modelo de avaliação ATIVO nas equipes visíveis (e na filtrada). Nenhuma: bloco nulo.</summary>
    private async Task<AvaliacaoPainelDto?> AvaliacaoAsync(
        UsuarioAtual usuario, IReadOnlyList<Entidade> entidades, IReadOnlyDictionary<Guid, ConsultaRegistros> escopos, Guid? equipeId,
        CancellationToken cancellationToken)
    {
        var ativos = (await modelos.ListarAtivosAsync(null, PoliticaPermissao.EquipesVisiveis(usuario)?.ToList(), cancellationToken))
            .Where(m => equipeId is null || m.EquipeId == equipeId)
            .ToList();

        var porEntidade = new List<AvaliacaoEntidadeDto>();
        foreach (var entidade in entidades.Where(e => !e.EhGlobal))
        {
            // Nome de cada critério de estrelas ativo, pela chave (o mesmo critério pode existir no modelo de várias equipes).
            var nomes = ativos
                .Where(m => m.EntidadeId == entidade.Id)
                .SelectMany(m => m.CriteriosAtivos)
                .Where(c => c.Tipo == TipoCriterio.Estrelas)
                .DistinctBy(c => c.Chave)
                .ToDictionary(c => c.Chave, c => c.Nome);
            if (nomes.Count == 0)
                continue;

            var resumos = await painel.ResumirAvaliacaoAsync(
                entidade.Id, escopos[entidade.Id], opcoes.MinimoAvaliacoesRanking, TamanhoRanking, cancellationToken);
            var idsRanking = resumos.SelectMany(r => r.Melhores.Concat(r.Piores)).Select(i => i.RegistroId).Distinct().ToList();
            var rotulos = idsRanking.Count == 0 ? new Dictionary<Guid, string?>() : await registros.ObterRotulosAsync(idsRanking, cancellationToken);

            // Todos os critérios ativos aparecem, mesmo sem notas (média nula, quantidade 0).
            var criterios = nomes
                .OrderBy(n => n.Key, StringComparer.Ordinal)
                .Select(n => resumos.FirstOrDefault(r => r.Criterio == n.Key) is { } resumo
                    ? new AvaliacaoCriterioDto(
                        n.Key, n.Value, Arredondar(resumo.Media), resumo.Quantidade, Distribuicao(resumo.Distribuicao),
                        Ranking(resumo.Melhores, rotulos), Ranking(resumo.Piores, rotulos))
                    : new AvaliacaoCriterioDto(n.Key, n.Value, null, 0, Distribuicao([0, 0, 0, 0, 0]), [], []))
                .ToList();
            porEntidade.Add(new AvaliacaoEntidadeDto(entidade.Chave, entidade.Nome, criterios));
        }

        return porEntidade.Count == 0 ? null : new AvaliacaoPainelDto(opcoes.MinimoAvaliacoesRanking, porEntidade);
    }

    private static IReadOnlyDictionary<string, int> Distribuicao(IReadOnlyList<int> quantidades) =>
        quantidades.Select((q, i) => (Estrelas: (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), q))
            .ToDictionary(x => x.Estrelas, x => x.q);

    private static IReadOnlyList<ItemRankingDto> Ranking(IReadOnlyList<ItemRanking> itens, IReadOnlyDictionary<Guid, string?> rotulos) =>
        itens.Select(i => new ItemRankingDto(i.RegistroId, rotulos.GetValueOrDefault(i.RegistroId), Math.Round(i.Media, 2), i.Quantidade)).ToList();

    private static double? Arredondar(double? valor) => valor is double v ? Math.Round(v, 2) : null;

    // ---- Atividade (RN84) ----

    private async Task<IReadOnlyList<AtividadeUsuarioDto>> AtividadeAsync(
        UsuarioAtual usuario, IReadOnlyList<Entidade> entidades, PeriodoPainel periodo, Guid? equipeId, CancellationToken cancellationToken)
    {
        var filtro = new FiltroAtividade(
            entidades.Where(e => e.EhGlobal).Select(e => e.Id).ToList(),
            entidades.Where(e => !e.EhGlobal).Select(e => e.Id).ToList(),
            PoliticaPermissao.EquipesVisiveis(usuario),
            equipeId);
        var atividade = await painel.ListarAtividadeAsync(filtro, periodo, TamanhoAtividade, cancellationToken);
        var nomes = await usuarios.ObterResumosAsync(atividade.Select(a => a.UsuarioId).ToList(), cancellationToken);

        return atividade
            .Select(a => new AtividadeUsuarioDto(
                a.UsuarioId, nomes.GetValueOrDefault(a.UsuarioId)?.Matricula ?? a.Matricula, nomes.GetValueOrDefault(a.UsuarioId)?.Nome,
                a.Criacoes, a.Alteracoes, a.Criacoes + a.Alteracoes))
            .ToList();
    }
}
