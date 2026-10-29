using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>
/// REFERENCIA: o id (GUID) do registro apontado, no formato "D"
/// (ex.: "3f2504e0-4f89-11d3-9a0c-0305e82c3301"). Aqui só se confere o FORMATO;
/// se o registro existe e é da entidade certa é conferido no caso de uso (RN06). RN04.
/// </summary>
public sealed class ReferenciaValidador : ITipoDadoValidador
{
    public TipoDado Tipo => TipoDado.Referencia;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.String
            || !Guid.TryParseExact(valor.GetString()!.Trim(), "D", out var id))
        {
            return ResultadoValidacaoValor.Falha(
                "Deve ser o id (GUID) de um registro, ex.: 3f2504e0-4f89-11d3-9a0c-0305e82c3301.");
        }

        // "D" do .NET sempre gera minúsculas.
        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(id.ToString("D")));
    }
}
