using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>TEXTO: texto curto com limite de caracteres (Campo.TamanhoMaximo). RN04.</summary>
public sealed class TextoValidador : ITipoDadoValidador
{
    public TipoDado Tipo => TipoDado.Texto;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.String)
            return ResultadoValidacaoValor.Falha("Deve ser um texto.");

        // Espaços nas pontas não contam e não são gravados.
        var texto = valor.GetString()!.Trim();

        if (campo.TamanhoMaximo is int tamanhoMaximo && texto.Length > tamanhoMaximo)
            return ResultadoValidacaoValor.Falha(
                $"Deve ter no máximo {tamanhoMaximo} caracteres (foram informados {texto.Length}).");

        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(texto));
    }
}
