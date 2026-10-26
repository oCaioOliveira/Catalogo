using System.Globalization;
using System.Runtime.CompilerServices;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso;

/// <summary>Configuração da exportação ("Exportacao:LimiteLinhas" e "Exportacao:FusoHorario").</summary>
/// <param name="LimiteLinhas">Acima disso, 400 EXPORTACAO_MUITO_GRANDE.</param>
/// <param name="Fuso">Fuso em que as datas e horas saem no arquivo (o Excel não guarda fuso).</param>
public sealed record OpcoesExportacao(int LimiteLinhas, TimeZoneInfo Fuso)
{
    public const int LimitePadrao = 10_000;
    public const string FusoPadrao = "America/Sao_Paulo";
}

/// <summary>O arquivo pronto para ser escrito na resposta. As linhas são lidas do banco enquanto o arquivo é escrito.</summary>
public sealed record ArquivoExportado(string NomeArquivo, string TipoConteudo, Func<Stream, CancellationToken, Task> EscreverAsync);

/// <summary>
/// Caso de uso: exportar a listagem em CSV ou XLSX (GET .../registros/exportar?formato=csv|xlsx).
///   - Os MESMOS filtros, ordenação e regras de visibilidade da listagem (equipe, aprovação, perfil): a consulta
///     é montada pelo mesmo <see cref="PreparadorConsultaRegistros"/>. "pagina" e "tamanho" não se aplicam.
///   - Colunas: os campos ativos na ordem dos metadados (cabeçalho = nome do campo; referências pelo rótulo) e,
///     no fim, equipe, situação de aprovação (se ativa), a média de cada critério de avaliação (se há modelo ativo),
///     criado por, criado em e atualizado em.
///   - No máximo <see cref="OpcoesExportacao.LimiteLinhas"/> linhas: acima disso, 400 pedindo mais filtros.
///   - Lê em lotes de <see cref="TamanhoLote"/>: o primeiro antes de responder (para conferir o limite), os demais
///     enquanto o arquivo é escrito.
///   - Textos protegidos contra injeção de fórmula (<see cref="ProtecaoFormula"/>).
/// </summary>
public sealed class ExportarRegistros(
    PreparadorConsultaRegistros preparador,
    IRegistroRepository registros,
    MontadorRegistroDto montador,
    IModeloAvaliacaoRepository modelos,
    IConfiguracaoRepository configuracoes,
    IEquipeRepository equipes,
    IEnumerable<IGeradorArquivoExportacao> geradores,
    OpcoesExportacao opcoes,
    TimeProvider relogio)
{
    public const int TamanhoLote = 500;

    public async Task<ArquivoExportado> ExecutarAsync(
        string grupoChave, string entidadeChave, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken)
    {
        var lista = parametros.ToList();

        // A exportação traz todas as linhas (até o limite): "pagina" e "tamanho" são ignorados, e "formato" não é filtro.
        var daConsulta = lista
            .Where(p => p.Key is not (ChavesReservadas.Formato or ChavesReservadas.Pagina or ChavesReservadas.Tamanho))
            .ToList();
        var (usuario, entidade, consulta) = await preparador.PrepararAsync(grupoChave, entidadeChave, daConsulta, cancellationToken);
        var gerador = EscolherGerador(geradores, lista);

        consulta = consulta with { Paginacao = new Paginacao(1, TamanhoLote) };
        var primeiroLote = await registros.ListarIdsAsync(entidade.Id, consulta, cancellationToken);
        if (primeiroLote.Total > opcoes.LimiteLinhas)
        {
            throw new ValidacaoRegistroException(
                new Dictionary<string, string[]>
                {
                    ["exportacao"] =
                    [
                        $"A exportação teria {primeiroLote.Total.ToString("N0", Cultura)} linhas, e o limite é " +
                        $"{opcoes.LimiteLinhas.ToString("N0", Cultura)}. Use mais filtros.",
                    ],
                },
                CodigosErro.ExportacaoMuitoGrande);
        }

        var colunas = await MontarColunasAsync(usuario, entidade, cancellationToken);
        var titulos = colunas.Select(c => c.Titulo).ToList();

        var hoje = TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), opcoes.Fuso);
        return new ArquivoExportado(
            $"{entidade.Chave}_{hoje:yyyy-MM-dd}.{gerador.Formato}",
            gerador.TipoConteudo,
            (destino, ct) => gerador.EscreverAsync(
                new PlanilhaExportacao(entidade.Nome, titulos, LinhasAsync(entidade, consulta, primeiroLote.Ids, colunas, ct)), destino, ct));
    }

    /// <summary>O gerador do parâmetro "formato" (obrigatório). Usado também pela exportação dos indicadores do painel.</summary>
    internal static IGeradorArquivoExportacao EscolherGerador(
        IEnumerable<IGeradorArquivoExportacao> geradores, IReadOnlyList<KeyValuePair<string, string>> parametros)
    {
        var erros = new ErrosPorCampo();
        var formato = InterpretadorConsulta.LerUnico(parametros, ChavesReservadas.Formato, erros)?.Trim();
        var aceitos = string.Join(" ou ", geradores.Select(g => g.Formato));

        var gerador = geradores.FirstOrDefault(g => string.Equals(g.Formato, formato, StringComparison.OrdinalIgnoreCase));
        if (gerador is null && !erros.PossuiErros)
        {
            erros.Adicionar(ChavesReservadas.Formato, string.IsNullOrEmpty(formato)
                ? $"Obrigatório: {aceitos}."
                : $"Formato '{formato}' não suportado. Use {aceitos}.");
        }

        erros.LancarSeHouverErros();
        return gerador!;
    }

    /// <summary>Uma coluna: o título e como tirar o valor do registro.</summary>
    private sealed record Coluna(string Titulo, Func<RegistroDto, IReadOnlyDictionary<Guid, string>, object?> Valor);

    private async Task<IReadOnlyList<Coluna>> MontarColunasAsync(UsuarioAtual usuario, Entidade entidade, CancellationToken cancellationToken)
    {
        var colunas = entidade.Campos
            .Where(c => c.Ativo)
            .OrderBy(c => c.Ordem).ThenBy(c => c.Chave, StringComparer.Ordinal)
            .Select(campo => new Coluna(campo.Nome, (r, _) => ValorDoCampo(campo, r)))
            .ToList();

        if (!entidade.EhGlobal)
        {
            var visiveis = PoliticaPermissao.EquipesVisiveis(usuario)?.ToList();
            colunas.Add(new Coluna("Equipe", (r, nomes) => r.EquipeId is Guid id ? nomes.GetValueOrDefault(id) : null));

            if (await configuracoes.RequerAprovacaoEmAlgumaAsync(entidade.Id, visiveis, cancellationToken))
                colunas.Add(new Coluna("Situação de aprovação", (r, _) => r.SituacaoAprovacao));

            // Critérios ESTRELAS ativos dos modelos ativos nas equipes que o usuário vê (os mesmos da ordenação, RN48).
            var criterios = (await modelos.ListarAtivosAsync(entidade.Id, visiveis, cancellationToken))
                .SelectMany(m => m.CriteriosAtivos)
                .Where(c => c.Tipo == TipoCriterio.Estrelas)
                .DistinctBy(c => c.Chave);
            foreach (var criterio in criterios)
                colunas.Add(new Coluna($"Média: {criterio.Nome}", (r, _) => r.Avaliacao?.Medias.GetValueOrDefault(criterio.Chave)));
        }

        colunas.Add(new Coluna("Criado por", (r, _) => r.CriadoPor?.Nome));
        colunas.Add(new Coluna("Criado em", (r, _) => NoFuso(r.CriadoEm)));
        colunas.Add(new Coluna("Atualizado em", (r, _) => r.AtualizadoEm is DateTime data ? NoFuso(data) : null));
        return colunas;
    }

    /// <summary>
    /// As linhas, lote a lote: cada lote é uma página da MESMA consulta da listagem, montada pelo mesmo
    /// <see cref="MontadorRegistroDto"/> (rótulos, autores e médias em lote, sem N+1).
    /// </summary>
    private async IAsyncEnumerable<IReadOnlyList<object?>> LinhasAsync(
        Entidade entidade, ConsultaRegistros consulta, IReadOnlyList<Guid> primeiroLote, IReadOnlyList<Coluna> colunas,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var nomesEquipes = new Dictionary<Guid, string>();
        var ids = primeiroLote;
        var emitidas = 0;

        for (var pagina = 1; ids.Count > 0; pagina++)
        {
            var lote = await ListarRegistros.CarregarNaOrdemAsync(registros, montador, entidade, ids, cancellationToken);
            await CarregarNomesEquipesAsync(lote, nomesEquipes, cancellationToken);

            foreach (var registro in lote)
            {
                // O limite já foi conferido; isto só protege contra registros criados durante a exportação.
                if (emitidas++ >= opcoes.LimiteLinhas)
                    yield break;

                yield return colunas.Select(c => Protegido(c.Valor(registro, nomesEquipes))).ToList();
            }

            if (ids.Count < TamanhoLote)
                yield break;

            var proxima = consulta with { Paginacao = new Paginacao(pagina + 1, TamanhoLote) };
            ids = (await registros.ListarIdsAsync(entidade.Id, proxima, cancellationToken)).Ids;
        }
    }

    private async Task CarregarNomesEquipesAsync(
        IReadOnlyList<RegistroDto> lote, Dictionary<Guid, string> nomes, CancellationToken cancellationToken)
    {
        var faltando = lote.Select(r => r.EquipeId).OfType<Guid>().Distinct().Where(id => !nomes.ContainsKey(id)).ToList();
        if (faltando.Count == 0)
            return;

        foreach (var equipe in await equipes.ObterPorIdsAsync(faltando, cancellationToken))
            nomes[equipe.Id] = equipe.Nome;
    }

    private object? ValorDoCampo(Campo campo, RegistroDto registro) =>
        ConversorValorTipado.ParaPlanilha(campo, registro.Dados[campo.Chave]) switch
        {
            DateTime dataHora => NoFuso(dataHora),
            bool booleano => booleano ? "Sim" : "Não",
            var valor => valor,
        };

    private DateTime NoFuso(DateTime utc) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), opcoes.Fuso), DateTimeKind.Unspecified);

    private static object? Protegido(object? valor) => valor is string texto ? ProtecaoFormula.Neutralizar(texto) : valor;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
}
