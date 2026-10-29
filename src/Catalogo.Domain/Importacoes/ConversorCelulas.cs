using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Importacoes;

/// <summary>Resultado da conversão de uma célula: o valor JSON (como o cadastro manual envia) ou a mensagem de erro.</summary>
public sealed record ResultadoConversao(JsonNode? Valor, string? Erro)
{
    public static ResultadoConversao Ok(JsonNode? valor) => new(valor, null);
    public static ResultadoConversao Falha(string erro) => new(null, erro);
    public bool Valido => Erro is null;
}

/// <summary>
/// Converte uma célula da planilha no valor JSON que a API recebe no cadastro manual (RN100), aceitando o que um usuário brasileiro
/// digitaria. Aqui só se converte o FORMATO: tamanho, opções, casas decimais e obrigatoriedade continuam com o
/// <see cref="Validacao.ValidadorRegistro"/>, igual ao cadastro manual. Uma conversão por tipo, num dicionário (sem switch por tipo,
/// como os validadores); REFERENCIA não passa por aqui (é resolvida pelo nome, RN99).
///
/// | Tipo | Aceita |
/// |---|---|
/// | TEXTO, TEXTO_LONGO, URL | o texto (números e datas do Excel viram texto como aparecem) |
/// | INTEIRO, DECIMAL | número do Excel; ou texto com vírgula ou ponto decimal, e milhar com ponto quando há vírgula decimal (no inteiro, o ponto é sempre milhar) |
/// | DATA | data do Excel; "dd/MM/aaaa" ou "aaaa-MM-dd" |
/// | DATA_HORA | data do Excel; "dd/MM/aaaa HH:mm[:ss]" ou ISO (sem fuso = o fuso de negócio) |
/// | BOOLEANO | verdadeiro/falso do Excel; Sim/Não, Verdadeiro/Falso, true/false, 1/0 |
/// | LISTA | a opção sem diferenciar maiúsculas e acentos, gravando o texto oficial da opção |
/// </summary>
public static partial class ConversorCelulas
{
    private delegate ResultadoConversao Conversao(Campo campo, CelulaImportada celula, TimeZoneInfo fuso);

    private static readonly IReadOnlyDictionary<TipoDado, Conversao> Conversoes = new Dictionary<TipoDado, Conversao>
    {
        [TipoDado.Texto] = ComoTexto,
        [TipoDado.TextoLongo] = ComoTexto,
        [TipoDado.Url] = ComoTexto,
        [TipoDado.Inteiro] = ComoInteiro,
        [TipoDado.Decimal] = ComoDecimal,
        [TipoDado.Data] = ComoData,
        [TipoDado.DataHora] = ComoDataHora,
        [TipoDado.Booleano] = ComoBooleano,
        [TipoDado.Lista] = ComoLista,
    };

    /// <summary>Os tipos que esta classe converte (todos menos REFERENCIA). Conferido por teste.</summary>
    public static IReadOnlyCollection<TipoDado> TiposConvertidos => Conversoes.Keys.ToList();

    /// <summary>Célula vazia → nulo (o campo fica "não informado"). <paramref name="fuso"/> vale para DATA_HORA sem fuso.</summary>
    public static ResultadoConversao Converter(Campo campo, CelulaImportada celula, TimeZoneInfo fuso)
    {
        if (celula.EhVazia)
            return ResultadoConversao.Ok(null);
        if (!Conversoes.TryGetValue(campo.TipoDado, out var conversao))
            throw new InvalidOperationException($"O campo '{campo.Chave}' ({campo.TipoDado}) não é convertido aqui: referências são resolvidas pelo nome.");
        return conversao(campo, celula, fuso);
    }

    private static ResultadoConversao ComoTexto(Campo campo, CelulaImportada celula, TimeZoneInfo fuso) =>
        ResultadoConversao.Ok(JsonValue.Create(celula.ParaTexto()));

    private static ResultadoConversao ComoInteiro(Campo campo, CelulaImportada celula, TimeZoneInfo fuso)
    {
        // Num inteiro, o ponto só pode ser separador de milhar: "1.200" = 1200 (num decimal, "1.200" seria 1,2).
        if (celula.Numero is null && MilharSemDecimais().IsMatch((celula.Texto ?? "").Trim()))
            celula = CelulaImportada.DeTexto(celula.Texto!.Trim().Replace(".", ""));
        if (!TentarNumero(celula, out var numero))
            return ResultadoConversao.Falha("Número inválido: use só algarismos (ex.: 1500).");
        if (numero != decimal.Truncate(numero) || numero < int.MinValue || numero > int.MaxValue)
            return ResultadoConversao.Falha("Deve ser um número inteiro (sem casas decimais).");
        return ResultadoConversao.Ok(JsonValue.Create((int)numero));
    }

    private static ResultadoConversao ComoDecimal(Campo campo, CelulaImportada celula, TimeZoneInfo fuso) =>
        TentarNumero(celula, out var numero)
            ? ResultadoConversao.Ok(JsonValue.Create(numero))
            : ResultadoConversao.Falha("Número inválido: use vírgula ou ponto como separador decimal (ex.: 1.234,56 ou 1234.56).");

    private static ResultadoConversao ComoData(Campo campo, CelulaImportada celula, TimeZoneInfo fuso)
    {
        if (DataNativa(celula) is DateTime nativa)
            return ResultadoConversao.Ok(JsonValue.Create(DateOnly.FromDateTime(nativa).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

        var texto = celula.Texto?.Trim() ?? "";
        return DateOnly.TryParseExact(texto, FormatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? ResultadoConversao.Ok(JsonValue.Create(data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            : ResultadoConversao.Falha("Data inválida: use dd/mm/aaaa (ex.: 21/10/2026).");
    }

    private static ResultadoConversao ComoDataHora(Campo campo, CelulaImportada celula, TimeZoneInfo fuso)
    {
        DateTime local;
        if (DataNativa(celula) is DateTime nativa)
        {
            local = DateTime.SpecifyKind(nativa, DateTimeKind.Unspecified);
        }
        else
        {
            var texto = celula.Texto?.Trim() ?? "";
            // ISO com fuso: vai como veio (o validador confere e converte para UTC).
            if (IsoComFuso().IsMatch(texto))
                return ResultadoConversao.Ok(JsonValue.Create(texto));

            if (!DateTime.TryParseExact(texto, FormatosDataHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
                return ResultadoConversao.Falha("Data e hora inválidas: use dd/mm/aaaa hh:mm (ex.: 21/10/2026 14:30).");
        }

        // Sem fuso: é a hora local do negócio (o mesmo fuso da exportação).
        var deslocamento = fuso.GetUtcOffset(local);
        var instante = new DateTimeOffset(local, deslocamento);
        return ResultadoConversao.Ok(JsonValue.Create(instante.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)));
    }

    private static ResultadoConversao ComoBooleano(Campo campo, CelulaImportada celula, TimeZoneInfo fuso)
    {
        if (celula.Booleano is bool nativo)
            return ResultadoConversao.Ok(JsonValue.Create(nativo));
        if (celula.Numero is decimal numero && numero is 0 or 1)
            return ResultadoConversao.Ok(JsonValue.Create(numero == 1));

        return CalculadoraChaveUnica.NormalizarTexto(celula.Texto ?? "") switch
        {
            "SIM" or "VERDADEIRO" or "TRUE" or "1" => ResultadoConversao.Ok(JsonValue.Create(true)),
            "NAO" or "FALSO" or "FALSE" or "0" => ResultadoConversao.Ok(JsonValue.Create(false)),
            _ => ResultadoConversao.Falha("Use Sim ou Não."),
        };
    }

    private static ResultadoConversao ComoLista(Campo campo, CelulaImportada celula, TimeZoneInfo fuso)
    {
        var digitado = CalculadoraChaveUnica.NormalizarTexto(celula.ParaTexto());
        var opcao = campo.Opcoes.FirstOrDefault(o => CalculadoraChaveUnica.NormalizarTexto(o) == digitado);
        return opcao is not null
            ? ResultadoConversao.Ok(JsonValue.Create(opcao))
            : ResultadoConversao.Falha($"'{celula.ParaTexto()}' não é uma opção. Use uma destas: {string.Join(", ", campo.Opcoes)}.");
    }

    /// <summary>
    /// A data nativa da célula do Excel. Uma célula de data sem o formato de data chega como o número de série do Excel
    /// (ex.: 46316 = 21/10/2026): aceito como data também, entre 1900 e 9999.
    /// </summary>
    private static DateTime? DataNativa(CelulaImportada celula)
    {
        if (celula.Data is DateTime data)
            return data;
        if (celula.Numero is decimal serie && serie >= 1 && serie < 2958466)
            return DateTime.FromOADate((double)serie);
        return null;
    }

    /// <summary>
    /// Número do Excel ou texto: "1234,56", "1.234,56" e "1234.56" (vírgula ou ponto decimal; ponto como milhar só com a vírgula
    /// decimal ou em grupos de três sem decimais, como "1.234.567"). "1,234.56" (o formato americano) é recusado para não haver dúvida.
    /// </summary>
    public static bool TentarNumero(CelulaImportada celula, out decimal numero)
    {
        if (celula.Numero is decimal nativo)
        {
            numero = nativo;
            return true;
        }

        numero = 0;
        var texto = (celula.Texto ?? "").Trim().Replace(" ", "").Replace(" ", "");
        if (texto.Length == 0)
            return false;

        string normalizado;
        if (NumeroComVirgulaDecimal().IsMatch(texto))
            normalizado = texto.Replace(".", "").Replace(',', '.');
        else if (NumeroComMilharSemDecimais().IsMatch(texto))
            normalizado = texto.Replace(".", "");
        else if (NumeroComPontoDecimal().IsMatch(texto))
            normalizado = texto;
        else
            return false;

        return decimal.TryParse(normalizado, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numero);
    }

    private static readonly string[] FormatosData = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];

    private static readonly string[] FormatosDataHora =
    [
        "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "d/M/yyyy H:mm", "d/M/yyyy H:mm:ss",
        "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss",
    ];

    // "1.234,56", "1234,56", "-0,5": vírgula decimal, milhar com ponto (opcional, em grupos de três).
    [GeneratedRegex(@"^-?(\d{1,3}(\.\d{3})+|\d+),\d+$")]
    private static partial Regex NumeroComVirgulaDecimal();

    // "1.234.567": só milhar com ponto, sem decimais (dois ou mais grupos, para "1.234" continuar sendo ponto decimal).
    [GeneratedRegex(@"^-?\d{1,3}(\.\d{3}){2,}$")]
    private static partial Regex NumeroComMilharSemDecimais();

    // "1.200", "12.345.678": grupos de três com ponto (só nos inteiros, onde o ponto não pode ser decimal).
    [GeneratedRegex(@"^-?\d{1,3}(\.\d{3})+$")]
    private static partial Regex MilharSemDecimais();

    // "1234", "1234.56", "-0.5": ponto decimal (ou inteiro).
    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumeroComPontoDecimal();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex IsoComFuso();
}
