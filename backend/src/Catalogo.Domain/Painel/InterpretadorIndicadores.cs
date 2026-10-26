using System.Globalization;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Painel;

/// <summary>
/// Transforma os parâmetros da agregação configurável (RN85 a RN87) numa <see cref="ConsultaIndicadores"/> tipada, conferindo
/// cada dimensão e métrica contra os metadados da entidade. Código puro. Todos os erros juntos (RN05), nas chaves
/// "agruparPor", "metricas", "ordenarPor" e "limite". Os FILTROS (os mesmos da listagem) são do <see cref="InterpretadorConsulta"/>.
///
///   agruparPor=ambiente,data_execucao:mes   (até 2; "relatorio_teste.ambiente" = campo do registro apontado, até 2 saltos, RN89)
///   metricas=contagem,media:vazao_media_tps,media_avaliacao:qualidade   (até 5; padrão "contagem"; o campo também por caminho)
///   ordenarPor=-media:vazao_media_tps   limite=100 (máximo 500)
/// </summary>
public static class InterpretadorIndicadores
{
    public const string ParametroAgruparPor = "agruparPor";
    public const string ParametroMetricas = "metricas";
    public const string ParametroOrdenarPor = "ordenarPor";
    public const string ParametroLimite = "limite";

    /// <summary>Os parâmetros próprios do painel: o resto são filtros da listagem.</summary>
    public static readonly IReadOnlySet<string> Parametros = new HashSet<string>(
        [ParametroAgruparPor, ParametroMetricas, ParametroOrdenarPor, ParametroLimite, PeriodoPainel.ParametroDe, PeriodoPainel.ParametroAte],
        StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, PeriodoAgrupamento> Periodos = new Dictionary<string, PeriodoAgrupamento>
    {
        ["dia"] = PeriodoAgrupamento.Dia,
        ["mes"] = PeriodoAgrupamento.Mes,
        ["trimestre"] = PeriodoAgrupamento.Trimestre,
        ["ano"] = PeriodoAgrupamento.Ano,
    };

    /// <param name="criteriosAvaliacao">
    /// Critérios de ESTRELAS ativos dos modelos ativos da entidade, nas equipes que o usuário vê (chave → nome). Só eles são
    /// aceitos em "media_avaliacao:&lt;criterio&gt;".
    /// </param>
    /// <param name="grupo">O grupo da entidade: de onde vêm as entidades apontadas nos caminhos "referencia.campo" (RN89).</param>
    /// <exception cref="ValidacaoRegistroException">Algum parâmetro inválido (todos os erros juntos).</exception>
    public static ConsultaIndicadores Interpretar(
        Grupo grupo, Entidade entidade, IReadOnlyList<KeyValuePair<string, string>> parametros,
        IReadOnlyDictionary<string, string>? criteriosAvaliacao = null)
    {
        var erros = new ErrosPorCampo();
        criteriosAvaliacao ??= new Dictionary<string, string>();

        var dimensoes = InterpretarDimensoes(grupo, entidade, InterpretadorConsulta.LerUnico(parametros, ParametroAgruparPor, erros), erros);
        var metricas = InterpretarMetricas(grupo, entidade, InterpretadorConsulta.LerUnico(parametros, ParametroMetricas, erros), criteriosAvaliacao, erros);
        var ordenacao = InterpretarOrdenacao(InterpretadorConsulta.LerUnico(parametros, ParametroOrdenarPor, erros), dimensoes, metricas, erros);
        var limite = InterpretarLimite(InterpretadorConsulta.LerUnico(parametros, ParametroLimite, erros), erros);

        erros.LancarSeHouverErros();
        return new ConsultaIndicadores(dimensoes, metricas, ordenacao, limite);
    }

    // ---- Dimensões (RN85) ----

    private static IReadOnlyList<Dimensao> InterpretarDimensoes(Grupo grupo, Entidade entidade, string? texto, ErrosPorCampo erros)
    {
        // Sem agruparPor: uma linha só, com o total.
        if (string.IsNullOrWhiteSpace(texto))
            return [];

        var itens = Separar(texto);
        if (itens.Count > ConsultaIndicadores.MaximoDimensoes)
            erros.Adicionar(ParametroAgruparPor, $"No máximo {ConsultaIndicadores.MaximoDimensoes} dimensões (vieram {itens.Count}).");

        var dimensoes = new List<Dimensao>();
        foreach (var item in itens)
        {
            if (InterpretarDimensao(grupo, entidade, item, erros) is not { } dimensao)
                continue;

            if (dimensoes.Any(d => d.Chave == dimensao.Chave))
                erros.Adicionar(ParametroAgruparPor, $"A dimensão '{dimensao.Chave}' aparece mais de uma vez.");
            else
                dimensoes.Add(dimensao);
        }

        return dimensoes;
    }

    private static Dimensao? InterpretarDimensao(Grupo grupo, Entidade entidade, string item, ErrosPorCampo erros)
    {
        if (item.Length == 0)
        {
            erros.Adicionar(ParametroAgruparPor, "Dimensão vazia: separe as dimensões por vírgula, sem vírgula sobrando.");
            return null;
        }

        var (chave, textoPeriodo) = SepararPeriodo(item);
        PeriodoAgrupamento? periodo = null;
        if (textoPeriodo is not null)
        {
            if (Periodos.TryGetValue(textoPeriodo, out var valor))
                periodo = valor;
            else
            {
                erros.Adicionar(ParametroAgruparPor, $"'{item}': período desconhecido. Use :dia, :mes, :trimestre ou :ano.");
                return null;
            }
        }

        // Dimensões de sistema.
        switch (chave)
        {
            case "equipe":
                if (entidade.EhGlobal)
                    return Erro($"'{item}': '{entidade.Nome}' é global, os registros não têm equipe.");
                return periodo is null ? new Dimensao(chave, "Equipe", "EQUIPE", null, DimensaoSistema.Equipe, null) : SemPeriodo(item);
            case ChavesReservadas.SituacaoAprovacao:
                return periodo is null
                    ? new Dimensao(chave, "Situação de aprovação", "SITUACAO_APROVACAO", null, DimensaoSistema.SituacaoAprovacao, null)
                    : SemPeriodo(item);
            case ChavesReservadas.CriadoEm:
                return periodo is PeriodoAgrupamento p
                    ? new Dimensao(item, $"Criado em ({NomePeriodo(p)})", "DATA_HORA", null, DimensaoSistema.CriadoEm, p)
                    : Erro($"'{item}': informe o período, ex.: criadoEm:mes.");
        }

        // Campos, também os de registros APONTADOS ("relatorio_teste.ambiente", até 2 saltos, RN89): o mesmo intérprete dos filtros.
        if (CaminhoCampo.Resolver(grupo, entidade, chave, out var erroCaminho) is not { } caminho)
            return Erro(erroCaminho!);

        var campo = caminho.Campo;
        var nomeCampo = caminho.Nome;
        var chaveCampoCompleta = caminho.Chave;

        switch (campo.TipoDado)
        {
            case TipoDado.Lista or TipoDado.Booleano or TipoDado.Referencia or TipoDado.Texto:
                return periodo is null ? new Dimensao(chaveCampoCompleta, nomeCampo, Tipo(campo), campo, null, null, caminho.Saltos) : SemPeriodo(item);
            case TipoDado.Data or TipoDado.DataHora:
                return periodo is PeriodoAgrupamento pd
                    ? new Dimensao(item, $"{nomeCampo} ({NomePeriodo(pd)})", Tipo(campo), campo, null, pd, caminho.Saltos)
                    : Erro($"'{item}': campo {Tipo(campo)} precisa de período, ex.: {chaveCampoCompleta}:mes.");
            default:
                return Erro($"'{chaveCampoCompleta}' é {Tipo(campo)}: dimensões aceitam LISTA, BOOLEANO, REFERENCIA, TEXTO, ou DATA e DATA_HORA com período.");
        }

        Dimensao? Erro(string mensagem)
        {
            erros.Adicionar(ParametroAgruparPor, mensagem);
            return null;
        }

        Dimensao? SemPeriodo(string valor) => Erro($"'{valor}': período só em dimensões de data (DATA, DATA_HORA, criadoEm).");
    }

    // ---- Métricas (RN86) ----

    private static IReadOnlyList<Metrica> InterpretarMetricas(
        Grupo grupo, Entidade entidade, string? texto, IReadOnlyDictionary<string, string> criteriosAvaliacao, ErrosPorCampo erros)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return [new Metrica(ConsultaIndicadores.Contagem, "Contagem", FuncaoMetrica.Contagem)];

        var itens = Separar(texto);
        if (itens.Count > ConsultaIndicadores.MaximoMetricas)
            erros.Adicionar(ParametroMetricas, $"No máximo {ConsultaIndicadores.MaximoMetricas} métricas (vieram {itens.Count}).");

        var metricas = new List<Metrica>();
        foreach (var item in itens)
        {
            if (InterpretarMetrica(grupo, entidade, item, criteriosAvaliacao, erros) is not { } metrica)
                continue;

            if (metricas.Any(m => m.Chave == metrica.Chave))
                erros.Adicionar(ParametroMetricas, $"A métrica '{metrica.Chave}' aparece mais de uma vez.");
            else
                metricas.Add(metrica);
        }

        return metricas;
    }

    private static Metrica? InterpretarMetrica(
        Grupo grupo, Entidade entidade, string item, IReadOnlyDictionary<string, string> criteriosAvaliacao, ErrosPorCampo erros)
    {
        if (item == ConsultaIndicadores.Contagem)
            return new Metrica(item, "Contagem", FuncaoMetrica.Contagem);

        var (funcao, alvo) = SepararPeriodo(item);
        if (alvo is null || alvo.Length == 0)
            return Erro($"'{item}': use contagem, media:campo, soma:campo, minimo:campo, maximo:campo ou media_avaliacao:criterio.");

        if (funcao == "media_avaliacao")
        {
            return criteriosAvaliacao.TryGetValue(alvo, out var nomeCriterio)
                ? new Metrica(item, $"Média de avaliação: {nomeCriterio}", FuncaoMetrica.MediaAvaliacao, Criterio: alvo)
                : Erro(criteriosAvaliacao.Count == 0
                    ? $"'{item}': '{entidade.Nome}' não tem modelo de avaliação ativo com critérios de estrelas."
                    : $"'{item}': critério desconhecido. Use um critério de estrelas ativo: {string.Join(", ", criteriosAvaliacao.Keys.Order(StringComparer.Ordinal))}.");
        }

        (FuncaoMetrica Funcao, string Nome)? tipo = funcao switch
        {
            "media" => (FuncaoMetrica.Media, "Média de"),
            "soma" => (FuncaoMetrica.Soma, "Soma de"),
            "minimo" => (FuncaoMetrica.Minimo, "Mínimo de"),
            "maximo" => (FuncaoMetrica.Maximo, "Máximo de"),
            _ => null,
        };
        if (tipo is null)
            return Erro($"'{item}': função desconhecida '{funcao}'. Use media, soma, minimo, maximo ou media_avaliacao.");

        // O campo pode ser de um registro apontado: "media:relatorio_teste.duracao" (RN89).
        if (CaminhoCampo.Resolver(grupo, entidade, alvo, out var erroCaminho) is not { } caminho)
            return Erro(erroCaminho!);
        var campo = caminho.Campo;
        if (campo.TipoDado is not (TipoDado.Inteiro or TipoDado.Decimal))
            return Erro($"'{item}': '{alvo}' é {Tipo(campo)}; {funcao} só vale para INTEIRO e DECIMAL.");

        return new Metrica(item, $"{tipo.Value.Nome} {caminho.Nome}", tipo.Value.Funcao, campo, Saltos: caminho.Saltos);

        Metrica? Erro(string mensagem)
        {
            erros.Adicionar(ParametroMetricas, mensagem);
            return null;
        }
    }

    // ---- Ordenação e limite (RN87) ----

    private static OrdenacaoIndicador? InterpretarOrdenacao(
        string? texto, IReadOnlyList<Dimensao> dimensoes, IReadOnlyList<Metrica> metricas, ErrosPorCampo erros)
    {
        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto))
            return null;

        var descendente = texto.StartsWith('-');
        var chave = descendente ? texto[1..] : texto;
        var aceitas = dimensoes.Select(d => d.Chave).Concat(metricas.Select(m => m.Chave)).Append(ConsultaIndicadores.Contagem).Distinct().ToList();
        if (aceitas.Contains(chave))
            return new OrdenacaoIndicador(chave, descendente);

        erros.Adicionar(ParametroOrdenarPor, $"'{chave}' não é uma dimensão nem uma métrica desta consulta. Use: {string.Join(", ", aceitas)}.");
        return null;
    }

    private static int InterpretarLimite(string? texto, ErrosPorCampo erros)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return ConsultaIndicadores.LimitePadrao;

        if (int.TryParse(texto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var limite)
            && limite is >= 1 and <= ConsultaIndicadores.LimiteMaximo)
        {
            return limite;
        }

        erros.Adicionar(ParametroLimite, $"Deve ser um número inteiro de 1 a {ConsultaIndicadores.LimiteMaximo}.");
        return ConsultaIndicadores.LimitePadrao;
    }

    // ---- Apoio ----

    private static List<string> Separar(string texto) => texto.Split(',').Select(i => i.Trim()).ToList();

    /// <summary>"chave:resto" → (chave, resto); sem ":", (texto, nulo).</summary>
    private static (string Antes, string? Depois) SepararPeriodo(string item)
    {
        var posicao = item.IndexOf(':');
        return posicao < 0 ? (item, null) : (item[..posicao], item[(posicao + 1)..]);
    }

    private static string Tipo(Campo campo) => TipoDadoConversor.ParaTexto(campo.TipoDado);

    public static string NomePeriodo(PeriodoAgrupamento periodo) => periodo switch
    {
        PeriodoAgrupamento.Dia => "dia",
        PeriodoAgrupamento.Mes => "mês",
        PeriodoAgrupamento.Trimestre => "trimestre",
        _ => "ano",
    };
}
