using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>INTEIRO: número inteiro de 32 bits (int). RN04.</summary>
public sealed class InteiroValidador : ITipoDadoValidador
{
    public TipoDado Tipo => TipoDado.Inteiro;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        // Número em texto ("42") é recusado: o cliente deve mandar o tipo JSON certo.
        // TryGetInt32 também recusa casas decimais (1.5) e valores fora do intervalo do int.
        if (valor.ValueKind != JsonValueKind.Number || !valor.TryGetInt32(out var numero))
            return ResultadoValidacaoValor.Falha(
                $"Deve ser um número inteiro entre {int.MinValue} e {int.MaxValue}.");

        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(numero));
    }
}
