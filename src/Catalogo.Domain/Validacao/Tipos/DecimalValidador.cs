using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>
/// DECIMAL: número com Precisao (total de dígitos) e Escala (dígitos depois da vírgula),
/// igual ao decimal(p, s) do SQL Server. Ex.: Precisao 5 e Escala 2 aceita de -999.99 a 999.99. RN04.
/// </summary>
public sealed class DecimalValidador : ITipoDadoValidador
{
    public TipoDado Tipo => TipoDado.Decimal;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.Number)
            return ResultadoValidacaoValor.Falha("Deve ser um número.");

        if (!valor.TryGetDecimal(out var numero))
            return ResultadoValidacaoValor.Falha("Número fora do intervalo suportado.");

        // O CHECK CK_Campo_Decimal garante Precisao e Escala preenchidas em campos DECIMAL.
        int escala = campo.Escala ?? 0;
        int precisao = campo.Precisao ?? 18;

        // 12.50 com escala 2 é válido; 12.505 não. Zeros à direita não contam (12.500 é válido).
        if (decimal.Round(numero, escala) != numero)
            return ResultadoValidacaoValor.Falha($"Deve ter no máximo {escala} casas decimais.");

        int digitosAntesDaVirgula = precisao - escala;
        if (Math.Abs(decimal.Truncate(numero)) >= DezElevadoA(digitosAntesDaVirgula))
            return ResultadoValidacaoValor.Falha(
                $"Deve ter no máximo {digitosAntesDaVirgula} dígitos antes da vírgula.");

        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(ComEscalaFixa(numero, escala)));
    }

    /// <summary>
    /// Devolve o número sempre com a mesma quantidade de casas (12.5 → 12.50 com escala 2),
    /// para o JSON gravado e o hash da chave única ficarem iguais para o mesmo valor.
    /// </summary>
    public static decimal ComEscalaFixa(decimal numero, int escala) =>
        decimal.Parse(numero.ToString("F" + escala, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static decimal DezElevadoA(int expoente)
    {
        // O decimal do .NET vai até ~7,9 × 10^28: acima disso o próprio TryGetDecimal já limita.
        if (expoente >= 29)
            return decimal.MaxValue;

        decimal resultado = 1;
        for (var i = 0; i < expoente; i++)
            resultado *= 10;
        return resultado;
    }
}
