using System.Globalization;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Painel;

/// <summary>
/// Caso de uso: a agregação configurável (GET /api/v1/painel/grupos/{grupo}/entidades/{entidade}/indicadores).
///   - Só GESTOR e ADMINISTRADOR (RN78), com a visibilidade da listagem (RN79).
///   - FILTROS: os mesmos da listagem, interpretados pelo mesmo <see cref="PreparadorConsultaRegistros"/>. "de" e "ate"
///     filtram a data de criação (RN80).
///   - agruparPor, metricas, ordenarPor e limite: <see cref="InterpretadorIndicadores"/> (RN85 a RN87).
///   - Todos os erros de todos os parâmetros num 400 só (RN05).
///   - A conta é toda no banco (<see cref="IPainelRepository.CalcularIndicadoresAsync"/>); aqui só se formatam as linhas.
/// </summary>
public sealed class CalcularIndicadores(
    IUsuarioAtual usuarioAtual,
    IMetadadosProvider metadados,
    PreparadorConsultaRegistros preparador,
    IModeloAvaliacaoRepository modelos,
    IPainelRepository painel,
    IRegistroRepository registros,
    OpcoesPainel opcoes)
{
    /// <summary>Parâmetros da listagem que não fazem sentido aqui (a ordenação e o limite são ordenarPor e limite).</summary>
    private static readonly IReadOnlySet<string> NaoSeAplicam =
        new HashSet<string>([ChavesReservadas.Pagina, ChavesReservadas.Tamanho, ChavesReservadas.Ordenacao], StringComparer.Ordinal);

    public async Task<IndicadoresDto> ExecutarAsync(
        string grupoChave, string entidadeChave, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancellationToken) =>
        (await CalcularAsync(grupoChave, entidadeChave, parametros.ToList(), new ErrosPorCampo(), cancellationToken)).Indicadores;

    /// <param name="erros">Erros que já vieram de quem chama (ex.: o "formato" da exportação), para saírem todos juntos.</param>
    internal async Task<(Entidade Entidade, IndicadoresDto Indicadores)> CalcularAsync(
        string grupoChave, string entidadeChave, IReadOnlyList<KeyValuePair<string, string>> parametros, ErrosPorCampo erros,
        CancellationToken cancellationToken)
    {
        var usuario = await usuarioAtual.ObterAsync(cancellationToken);
        PermissoesPainel.Exigir(usuario);
        (var grupo, var entidade, usuario) = await metadados.ObterEntidadeOuFalharAsync(usuario, grupoChave, entidadeChave, UsoEntidade.Leitura, cancellationToken);

        foreach (var chave in parametros.Select(p => p.Key).Distinct().Where(NaoSeAplicam.Contains))
            erros.Adicionar(chave, "Não se aplica aos indicadores: use ordenarPor e limite.");
        var doPainel = parametros.Where(p => InterpretadorIndicadores.Parametros.Contains(p.Key)).ToList();
        var filtros = parametros.Where(p => !InterpretadorIndicadores.Parametros.Contains(p.Key) && !NaoSeAplicam.Contains(p.Key)).ToList();

        // Cada parte valida o seu pedaço; os erros são juntados e saem todos de uma vez.
        ConsultaPreparada? preparada = null;
        try
        {
            preparada = await preparador.PrepararAsync(grupoChave, entidadeChave, filtros, cancellationToken);
        }
        catch (ValidacaoRegistroException ex)
        {
            Juntar(erros, ex);
        }

        ConsultaIndicadores? indicadores = null;
        try
        {
            indicadores = InterpretadorIndicadores.Interpretar(grupo, entidade, doPainel, await CriteriosAsync(usuario, entidade, cancellationToken));
        }
        catch (ValidacaoRegistroException ex)
        {
            Juntar(erros, ex);
        }

        var periodo = PeriodoPainel.Interpretar(doPainel, opcoes.Fuso, erros);
        erros.LancarSeHouverErros();

        var resultado = await painel.CalcularIndicadoresAsync(entidade.Id, preparada!.Consulta, periodo, indicadores!, cancellationToken);
        return (entidade, await MontarAsync(indicadores!, ComLinhaDoTotal(indicadores!, resultado), periodo, cancellationToken));
    }

    /// <summary>
    /// RN85: sem agruparPor, a resposta tem SEMPRE uma linha, com o total. Sem nenhum registro, o GROUP BY do banco não devolve
    /// linha: aqui ela é montada com contagem 0, soma 0 e as demais métricas nulas (RN86).
    /// </summary>
    internal static ResultadoIndicadores ComLinhaDoTotal(ConsultaIndicadores indicadores, ResultadoIndicadores resultado)
    {
        if (indicadores.Dimensoes.Count > 0 || resultado.Linhas.Count > 0)
            return resultado;

        var metricas = indicadores.Metricas.Select(m => m.Funcao == FuncaoMetrica.Soma ? 0m : (decimal?)null).ToList();
        return resultado with { Linhas = [new LinhaResultado([], 0, metricas)] };
    }

    /// <summary>Critérios de ESTRELAS ativos dos modelos ativos nas equipes visíveis (chave → nome): os de "media_avaliacao:".</summary>
    private async Task<IReadOnlyDictionary<string, string>> CriteriosAsync(UsuarioAtual usuario, Entidade entidade, CancellationToken cancellationToken)
    {
        if (entidade.EhGlobal)
            return new Dictionary<string, string>();

        return (await modelos.ListarAtivosAsync(entidade.Id, PoliticaPermissao.EquipesVisiveis(usuario)?.ToList(), cancellationToken))
            .SelectMany(m => m.CriteriosAtivos)
            .Where(c => c.Tipo == TipoCriterio.Estrelas)
            .DistinctBy(c => c.Chave)
            .ToDictionary(c => c.Chave, c => c.Nome, StringComparer.Ordinal);
    }

    private async Task<IndicadoresDto> MontarAsync(
        ConsultaIndicadores indicadores, ResultadoIndicadores resultado, PeriodoPainel periodo, CancellationToken cancellationToken)
    {
        // Rótulos das dimensões REFERENCIA em lote (o mesmo rótulo da listagem).
        var referencias = indicadores.Dimensoes
            .Select((d, i) => (d, i))
            .Where(x => x.d.Sistema is null && x.d.Campo!.EhReferencia)
            .SelectMany(x => resultado.Linhas.Select(l => l.Dimensoes[x.i].Id))
            .OfType<Guid>().Distinct().ToList();
        var rotulos = referencias.Count == 0 ? new Dictionary<Guid, string?>() : await registros.ObterRotulosAsync(referencias, cancellationToken);

        var linhas = resultado.Linhas.Select(linha =>
        {
            var valores = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < indicadores.Dimensoes.Count; i++)
                valores[indicadores.Dimensoes[i].Chave] = Formatar(indicadores.Dimensoes[i], linha.Dimensoes[i], rotulos);
            valores[ConsultaIndicadores.Contagem] = linha.Contagem;
            for (var j = 0; j < indicadores.Metricas.Count; j++)
            {
                if (indicadores.Metricas[j].Funcao != FuncaoMetrica.Contagem)
                    valores[indicadores.Metricas[j].Chave] = Arredondar(linha.Metricas[j]);
            }

            return (IReadOnlyDictionary<string, object?>)valores;
        }).ToList();

        return new IndicadoresDto(
            indicadores.Dimensoes.Select(d => new DimensaoIndicadorDto(d.Chave, d.Nome, d.Tipo, d.Periodo?.ToString().ToLowerInvariant())).ToList(),
            indicadores.Metricas.Select(m => new MetricaIndicadorDto(m.Chave, m.Nome)).ToList(),
            linhas,
            resultado.Truncado,
            PermissoesPainel.Periodo(periodo, opcoes));
    }

    /// <summary>
    /// O valor de uma dimensão na resposta: { id, rotulo } para REFERENCIA e equipe; texto ("Outros" para os valores de TEXTO
    /// fora dos mais frequentes); true/false; a situação ("PENDENTE"); o período ("2026-03-15", "2026-03", "2026-T1", "2026").
    /// Nulo = registros sem valor.
    /// </summary>
    internal static object? Formatar(Dimensao dimensao, ValorDimensao valor, IReadOnlyDictionary<Guid, string?> rotulos)
    {
        if (dimensao.Periodo is PeriodoAgrupamento periodo)
            return valor.Data is DateTime data ? FormatarPeriodo(data, periodo) : null;

        return dimensao switch
        {
            { Sistema: DimensaoSistema.Equipe } => valor.Id is Guid equipe ? new ValorReferenciaDto(equipe, valor.Texto) : null,
            { Sistema: DimensaoSistema.SituacaoAprovacao } => valor.Situacao is SituacaoAprovacao s ? AprovacaoConversor.ParaTexto(s) : null,
            { Campo.TipoDado: TipoDado.Referencia } => valor.Id is Guid id ? new ValorReferenciaDto(id, rotulos.GetValueOrDefault(id)) : null,
            { Campo.TipoDado: TipoDado.Booleano } => valor.Booleano,
            _ => valor.Outros ? Outros : valor.Texto,
        };
    }

    /// <summary>O grupo dos valores de TEXTO fora dos mais frequentes (RN85).</summary>
    public const string Outros = "Outros";

    private static string FormatarPeriodo(DateTime data, PeriodoAgrupamento periodo) => periodo switch
    {
        PeriodoAgrupamento.Dia => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        PeriodoAgrupamento.Mes => data.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        PeriodoAgrupamento.Trimestre => $"{data.Year:D4}-T{(data.Month - 1) / 3 + 1}",
        _ => data.Year.ToString("D4", CultureInfo.InvariantCulture),
    };

    /// <summary>4 casas, sem zeros sobrando (o banco devolve 10 casas: 1250.5000000000 vira 1250.5).</summary>
    private static decimal? Arredondar(decimal? valor) =>
        valor is decimal v ? Math.Round(v, 4, MidpointRounding.AwayFromZero) / 1.0000000000000000000000000000m : null;

    internal static void Juntar(ErrosPorCampo erros, ValidacaoRegistroException excecao)
    {
        foreach (var (chave, mensagens) in excecao.Erros)
        {
            foreach (var mensagem in mensagens)
                erros.Adicionar(chave, mensagem);
        }
    }
}
