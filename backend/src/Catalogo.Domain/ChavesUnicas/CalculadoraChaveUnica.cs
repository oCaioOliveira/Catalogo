using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.ChavesUnicas;

/// <summary>
/// Calcula o hash SHA-256 de cada restrição única de um registro (RN09 e RN10).
/// O hash é gravado em RegistroChaveUnica, que tem UNIQUE (RestricaoUnicaId, ValorHash):
/// assim o próprio banco impede duplicados, mesmo com duas gravações ao mesmo tempo.
///
/// NORMALIZAÇÃO (cada valor vira um texto antes do hash):
///   - TEXTO, TEXTO_LONGO e LISTA: Trim, sem acentos e em maiúsculas (cultura invariante).
///     Assim "Comunidade Ágil" e "  comunidade agil" geram o mesmo hash, coerente com a
///     collation Latin1_General_CI_AI do banco (CI = ignora maiúsculas, AI = ignora acentos).
///   - INTEIRO: número em cultura invariante (ex.: "-42").
///   - DECIMAL: cultura invariante, sempre com a escala do campo (12.5 com escala 2 → "12.50").
///   - BOOLEANO: "true" ou "false".
///   - DATA: ISO 8601 "aaaa-mm-dd". DATA_HORA: ISO 8601 em UTC ("2026-10-21T17:30:00Z").
///   - REFERENCIA: GUID do registro apontado, em minúsculas, formato "D".
///   - URL: Trim, com o ESQUEMA e o HOST (e a porta) em minúsculas; o caminho, a consulta e o fragmento ficam como vieram
///     (podem diferenciar maiúsculas). "HTTPS://Exemplo.COM/a" e "https://exemplo.com/a" são a mesma; ".../A" não.
/// Os textos são unidos, NA ORDEM DOS CAMPOS DA RESTRIÇÃO (RestricaoUnica.Campos), pelo
/// separador \u001f (caractere "Unit Separator", que não aparece em texto digitado), e o
/// resultado em UTF-8 vai para o SHA-256.
/// Se QUALQUER valor da restrição for nulo, a chave não é gerada (RN10): igual ao UNIQUE do
/// SQL, nulo não conflita com nada.
/// Nas entidades POR EQUIPE, o id da equipe entra antes dos valores: a unicidade vale dentro de cada equipe.
///
/// ATENÇÃO: mudar esta normalização muda os hashes. Os registros já gravados precisariam
/// ter os hashes recalculados por um script.
/// </summary>
public sealed class CalculadoraChaveUnica
{
    public const char Separador = '\u001f';

    /// <summary>Calcula as chaves de todas as restrições da entidade que têm todos os valores preenchidos.</summary>
    /// <param name="equipeId">
    /// Equipe do registro, nas entidades por equipe: a unicidade vale DENTRO de cada equipe (RN09).
    /// Duas equipes podem ter um projeto "Portal" cada, e um 409 nunca revela registro de outra equipe (RN28).
    /// Nulo nas entidades globais (unicidade em todo o sistema).
    /// </param>
    public IReadOnlyList<ChaveUnicaCalculada> Calcular(Entidade entidade, RegistroValidado registro, Guid? equipeId = null)
    {
        var chaves = new List<ChaveUnicaCalculada>();

        foreach (var restricao in entidade.RestricoesUnicas)
        {
            var hash = CalcularHash(restricao, registro.ObterValor, equipeId);
            if (hash is not null)
                chaves.Add(new ChaveUnicaCalculada(restricao, hash));
        }

        return chaves;
    }

    /// <summary>Hash de uma restrição, ou nulo se algum valor estiver vazio (RN10).</summary>
    /// <param name="equipeId">Se informada, entra como PRIMEIRA parte do texto (GUID "D"): unicidade por equipe.</param>
    public byte[]? CalcularHash(RestricaoUnica restricao, Func<Campo, JsonNode?> obterValor, Guid? equipeId = null)
    {
        var partes = new List<string>(restricao.Campos.Count + 1);
        if (equipeId is Guid equipe)
            partes.Add(equipe.ToString("D"));

        foreach (var campo in restricao.Campos)
        {
            var valor = obterValor(campo);
            if (valor is null)
                return null;

            partes.Add(Normalizar(campo, valor));
        }

        var texto = string.Join(Separador, partes);
        return SHA256.HashData(Encoding.UTF8.GetBytes(texto));
    }

    /// <summary>Texto normalizado de um valor (ver regras no comentário da classe).</summary>
    public static string Normalizar(Campo campo, JsonNode valor) => campo.TipoDado switch
    {
        TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Lista => NormalizarTexto(valor.GetValue<string>()),
        TipoDado.Inteiro => valor.GetValue<int>().ToString(CultureInfo.InvariantCulture),
        TipoDado.Decimal => valor.GetValue<decimal>().ToString("F" + (campo.Escala ?? 0), CultureInfo.InvariantCulture),
        TipoDado.Booleano => valor.GetValue<bool>() ? "true" : "false",
        TipoDado.Data => DateOnly
            .ParseExact(valor.GetValue<string>(), DataValidador.Formato, CultureInfo.InvariantCulture)
            .ToString(DataValidador.Formato, CultureInfo.InvariantCulture),
        TipoDado.DataHora => DataHoraValidador.ParaTextoUtc(
            DateTimeOffset.Parse(valor.GetValue<string>(), CultureInfo.InvariantCulture)),
        TipoDado.Referencia => Guid.Parse(valor.GetValue<string>()).ToString("D"),
        TipoDado.Url => NormalizarUrl(valor.GetValue<string>()),
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo.TipoDado, "TipoDado sem normalização de chave única."),
    };

    /// <summary>URL: trim e esquema e autoridade (host e porta) em minúsculas, o resto como veio.</summary>
    public static string NormalizarUrl(string url)
    {
        var texto = url.Trim();
        var inicioAutoridade = texto.IndexOf("://", StringComparison.Ordinal);
        if (inicioAutoridade < 0)
            return texto;

        inicioAutoridade += 3;
        var fimAutoridade = texto.IndexOfAny(['/', '?', '#'], inicioAutoridade);
        if (fimAutoridade < 0)
            fimAutoridade = texto.Length;
        return texto[..fimAutoridade].ToLowerInvariant() + texto[fimAutoridade..];
    }

    /// <summary>Trim, remove acentos (decompõe "á" em "a" + acento e descarta o acento) e põe em maiúsculas.</summary>
    public static string NormalizarTexto(string texto)
    {
        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var semAcentos = new StringBuilder(decomposto.Length);

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                semAcentos.Append(caractere);
        }

        return semAcentos.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}

/// <summary>Hash calculado de uma restrição única.</summary>
public sealed record ChaveUnicaCalculada(RestricaoUnica Restricao, byte[] ValorHash);
