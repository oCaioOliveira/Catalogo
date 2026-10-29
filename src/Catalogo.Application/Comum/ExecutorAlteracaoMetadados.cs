using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.Comum;

/// <summary>
/// Uma alteração de metadados pronta para executar: o que verificar e escrever (<see cref="Plano"/>, do
/// <see cref="PlanejadorAlteracoes"/>), como gravar os metadados (<see cref="Gravar"/>) e o que vai no histórico.
/// </summary>
/// <param name="GrupoChave">Chave do grupo afetado (o cache dele é invalidado; a estrutura nova é relida por ela).</param>
/// <param name="GrupoId">Id do grupo, para o histórico quando ele deixa de existir (exclusão). Senão, vale o relido.</param>
/// <param name="Depois">O que vai no histórico depois da gravação, a partir do grupo RELIDO (nulo se ele foi excluído).</param>
public sealed record AlteracaoMetadados(
    string GrupoChave,
    Guid GrupoId,
    OperacaoHistorico Operacao,
    PlanoAlteracao Plano,
    Func<CancellationToken, Task> Gravar,
    JsonObject? Antes,
    Func<Grupo?, JsonObject?> Depois);

/// <summary>
/// O caminho de TODA escrita da administração de metadados (RN50 a RN58), igual para todas as operações:
///   1. só o ADMINISTRADOR (RN50);
///   2. numa transação SERIALIZABLE, as verificações contra os dados do plano (RN58) e os usos que impedem a mudança;
///   3. simulação (RN56): devolve o relatório e DESFAZ a transação, sem gravar nada;
///   4. bloqueio: 409 com o relatório em "detalhes" (ALTERACAO_INCOMPATIVEL_COM_DADOS ou METADADO_EM_USO);
///   5. grava os metadados, relê a estrutura nova e faz as escritas em massa em lotes (valor padrão, chaves únicas);
///   6. UMA linha METADADO no histórico, com antes, depois e a quantidade de registros reescritos (RN52);
///   7. confirma e invalida o cache de metadados.
/// As validações de configuração (400) são feitas por quem chama, antes, com o <see cref="RegrasMetadados"/>.
/// </summary>
public sealed class ExecutorAlteracaoMetadados(
    IUsuarioAtual usuarioAtual,
    IMetadadosAdminRepository admin,
    IVerificadorDadosMetadados verificador,
    IEscritorRegistrosMetadados escritor,
    IRegistroRepository registros,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    IMetadadosProvider metadados,
    ServicoHistorico historico,
    TimeProvider relogio)
{
    /// <summary>RN50: só o ADMINISTRADOR administra metadados (403 para os demais).</summary>
    public async Task<UsuarioAtual> ExigirAdministradorAsync(CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        if (!PoliticaPermissao.PodeAdministrarMetadados(usuario))
            throw new AcessoNegadoException("Somente o administrador administra os metadados: eles valem para todas as equipes.");
        return usuario;
    }

    public DateTime AgoraUtc => relogio.GetUtcNow().UtcDateTime;

    /// <summary>O grupo como está no banco agora (sem cache), inclusive entidades e campos inativos. 404 se não existe.</summary>
    public async Task<Grupo> ObterGrupoAsync(string grupoChave, CancellationToken cancellationToken) =>
        await admin.ObterGrupoAtualAsync(grupoChave, cancellationToken)
        ?? throw new RecursoNaoEncontradoException($"Grupo '{grupoChave}' não encontrado.", CodigosErro.GrupoNaoEncontrado);

    public static Entidade ObterEntidade(Grupo grupo, string entidadeChave) =>
        grupo.ObterEntidade(entidadeChave)
        ?? throw new RecursoNaoEncontradoException($"Entidade '{entidadeChave}' não encontrada no grupo '{grupo.Chave}'.", CodigosErro.EntidadeNaoEncontrada);

    public static Campo ObterCampo(Entidade entidade, string campoChave) =>
        entidade.ObterCampo(campoChave)
        ?? throw new RecursoNaoEncontradoException($"Campo '{campoChave}' não encontrado em '{entidade.Chave}'.", CodigosErro.CampoNaoEncontrado);

    /// <summary>
    /// RN52: a "versao" enviada (Base64, como o GET devolve) é obrigatória e tem de ser a atual: senão, 409 (alguém alterou
    /// depois que o administrador leu). Devolve os bytes, para o UPDATE conferir de novo (... WHERE Versao = @versao).
    /// </summary>
    public static byte[] ConferirVersao(string? texto, byte[] atual, string oQue)
    {
        byte[]? versao = null;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            try
            {
                versao = Convert.FromBase64String(texto);
            }
            catch (FormatException)
            {
                versao = null;
            }
        }

        if (versao is null)
        {
            throw new ValidacaoRegistroException(new Dictionary<string, string[]>
            {
                [ChavesReservadas.Versao] = [$"Obrigatória: envie a \"versao\" {oQue} exatamente como o GET devolveu."],
            });
        }

        if (!versao.AsSpan().SequenceEqual(atual))
            throw new ConflitoConcorrenciaException($"Os metadados {oQue} foram alterados por outra pessoa depois que você os leu. Recarregue e tente de novo.");

        return versao;
    }

    /// <summary>Lança 400 com os erros de configuração (RN05), se houver.</summary>
    public static void LancarSeHouverErros(ErrosPorCampo erros) => erros.LancarSeHouverErros();

    public async Task<ResultadoAdministracaoDto> ExecutarAsync(
        UsuarioAtual usuario, AlteracaoMetadados alteracao, bool simular, CancellationToken cancellationToken)
    {
        try
        {
            return await unidadeDeTrabalho.ExecutarSerializavelAsync(async ct =>
            {
                var impactos = await AvaliarAsync(alteracao.Plano, ct);
                var impedem = impactos.Where(i => i.Impede).ToList();

                if (simular)
                    return (new ResultadoAdministracaoDto(impedem.Count == 0, true, await ParaDtosAsync(impactos, ct), null), false);

                if (impedem.Count > 0)
                    throw await BloqueioAsync(impedem, ct);

                await alteracao.Gravar(ct);
                await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);

                // A estrutura NOVA, lida dentro da transação: é com ela que se calculam chaves e se preenchem valores.
                var depois = await admin.ObterGrupoAtualAsync(alteracao.GrupoChave, ct);
                var afetados = 0;
                foreach (var escrita in alteracao.Plano.Escritas)
                {
                    var entidade = depois?.ObterEntidade(EntidadeDa(escrita))
                        ?? throw new InvalidOperationException("A entidade da escrita em massa não existe mais.");
                    afetados += await escritor.AplicarAsync(escrita, entidade, ct);
                }

                var jsonDepois = alteracao.Depois(depois);
                if (jsonDepois is not null && alteracao.Plano.Escritas.Count > 0)
                    jsonDepois["registrosAfetados"] = afetados;

                historico.RegistrarMetadado(alteracao.Operacao, depois?.Id ?? alteracao.GrupoId, usuario, alteracao.Antes, jsonDepois);
                await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);

                var grupo = depois is null ? null : await MontarAsync(depois, ct);
                return (new ResultadoAdministracaoDto(true, false, await ParaDtosAsync(impactos, ct), grupo), true);
            }, cancellationToken);
        }
        finally
        {
            // Sem isto, a API continuaria usando os metadados antigos por até 10 minutos (MetadadosProvider.TempoCache).
            if (!simular)
                metadados.InvalidarCache(alteracao.GrupoChave);
        }
    }

    /// <summary>A estrutura completa do grupo para a administração, com a quantidade de registros de cada entidade.</summary>
    public async Task<GrupoAdminDto> MontarAsync(Grupo grupo, CancellationToken cancellationToken) =>
        grupo.ParaAdminDto(await admin.ContarRegistrosAsync(grupo.Entidades.Select(e => e.Id).ToList(), cancellationToken));

    private async Task<IReadOnlyList<ImpactoDados>> AvaliarAsync(PlanoAlteracao plano, CancellationToken cancellationToken)
    {
        var impactos = new List<ImpactoDados>(plano.Usos);
        foreach (var verificacao in plano.Verificacoes)
        {
            var resultado = await verificador.VerificarAsync(verificacao, cancellationToken);
            impactos.Add(new ImpactoDados(
                verificacao.Tipo,
                verificacao.Descricao,
                verificacao.Bloqueia,
                resultado.Quantidade,
                resultado.Exemplos.Select(id => new RegistroAfetado(id, null)).ToList(),
                resultado.Grupos?.Select(g => (IReadOnlyList<RegistroAfetado>)g.Select(id => new RegistroAfetado(id, null)).ToList()).ToList()));
        }

        return impactos;
    }

    private async Task<RegraDeNegocioException> BloqueioAsync(IReadOnlyList<ImpactoDados> impedem, CancellationToken cancellationToken)
    {
        // Registros afetados → incompatível com os dados; só usos de configuração → em uso.
        var codigo = impedem.Any(i => i.Quantidade > 0) ? CodigosErro.AlteracaoIncompativelComDados : CodigosErro.MetadadoEmUso;
        var mensagem = codigo == CodigosErro.AlteracaoIncompativelComDados
            ? "A alteração deixaria registros inválidos: " + string.Join(" ", impedem.Select(i => $"{i.Quantidade} registro(s): {i.Descricao}"))
            : string.Join(" ", impedem.Select(i => $"{i.Descricao} Usos: {string.Join("; ", i.Usos ?? [])}."));
        return new RegraDeNegocioException(mensagem, codigo,
            new Dictionary<string, object?> { ["impactos"] = await ParaDtosAsync(impedem, cancellationToken) });
    }

    /// <summary>Os impactos como DTO, com o rótulo de cada registro de exemplo (buscados de uma vez, RN20).</summary>
    private async Task<IReadOnlyList<ImpactoDto>> ParaDtosAsync(IReadOnlyList<ImpactoDados> impactos, CancellationToken cancellationToken)
    {
        var ids = impactos
            .SelectMany(i => i.Exemplos.Concat(i.Grupos?.SelectMany(g => g) ?? []))
            .Select(r => r.Id)
            .Distinct()
            .ToList();
        var rotulos = ids.Count == 0 ? new Dictionary<Guid, string?>() : await registros.ObterRotulosAsync(ids, cancellationToken);

        RegistroAfetadoDto Dto(RegistroAfetado r) => new(r.Id, rotulos.GetValueOrDefault(r.Id));

        return impactos
            .Select(i => new ImpactoDto(
                i.Tipo, i.Descricao, i.Bloqueia, i.Quantidade,
                i.Exemplos.Select(Dto).ToList(),
                i.Grupos?.Select(g => (IReadOnlyList<RegistroAfetadoDto>)g.Select(Dto).ToList()).ToList(),
                i.Usos))
            .ToList();
    }

    private static Guid EntidadeDa(EscritaRegistros escrita) => escrita switch
    {
        PreencherValorPadrao preencher => preencher.EntidadeId,
        RecalcularChavesUnicas chaves => chaves.EntidadeId,
        _ => throw new ArgumentOutOfRangeException(nameof(escrita), escrita, "Escrita sem entidade."),
    };
}
