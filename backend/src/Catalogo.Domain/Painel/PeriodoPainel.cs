using System.Globalization;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Painel;

/// <summary>
/// O período do painel (RN80): "de" e "ate" são DATAS no fuso de negócio (inclusivas), e o deslocamento do fuso, FIXO
/// (TimeZoneInfo.BaseUtcOffset: sem horário de verão, que o Brasil não tem desde 2019). As datas de sistema estão em UTC:
/// os limites são convertidos aqui, e o agrupamento por período soma o <see cref="Deslocamento"/> na consulta (DATEADD).
/// </summary>
public sealed record PeriodoPainel(DateOnly? De, DateOnly? Ate, TimeSpan Deslocamento)
{
    public const string ParametroDe = "de";
    public const string ParametroAte = "ate";

    /// <summary>Início do período em UTC (00:00 local de "de"), ou nulo sem "de".</summary>
    public DateTime? InicioUtc => De is DateOnly de ? ParaUtc(de) : null;

    /// <summary>Fim EXCLUSIVO do período em UTC (00:00 local do dia seguinte a "ate"), ou nulo sem "ate".</summary>
    public DateTime? FimUtc => Ate is DateOnly ate ? ParaUtc(ate.AddDays(1)) : null;

    /// <summary>"de" e "ate" no formato aaaa-mm-dd. Erros nas chaves "de" e "ate", juntos com os outros (RN05).</summary>
    public static PeriodoPainel Interpretar(IReadOnlyList<KeyValuePair<string, string>> parametros, TimeZoneInfo fuso, ErrosPorCampo erros)
    {
        var de = LerData(parametros, ParametroDe, erros);
        var ate = LerData(parametros, ParametroAte, erros);
        if (de > ate)
            erros.Adicionar(ParametroAte, "Deve ser igual ou posterior a \"de\".");

        return new PeriodoPainel(de, ate, fuso.BaseUtcOffset);
    }

    private DateTime ParaUtc(DateOnly data) => DateTime.SpecifyKind(data.ToDateTime(TimeOnly.MinValue) - Deslocamento, DateTimeKind.Utc);

    private static DateOnly? LerData(IReadOnlyList<KeyValuePair<string, string>> parametros, string nome, ErrosPorCampo erros)
    {
        var texto = InterpretadorConsulta.LerUnico(parametros, nome, erros)?.Trim();
        if (string.IsNullOrEmpty(texto))
            return null;

        if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            return data;

        erros.Adicionar(nome, "Data inválida: use aaaa-mm-dd (no fuso de negócio).");
        return null;
    }
}
