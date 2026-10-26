using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>BOOLEANO: true ou false do JSON (não aceita "true" em texto, 0 ou 1). RN04.</summary>
public sealed class BooleanoValidador : ITipoDadoValidador
{
    public TipoDado Tipo => TipoDado.Booleano;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.True => ResultadoValidacaoValor.Sucesso(JsonValue.Create(true)),
        JsonValueKind.False => ResultadoValidacaoValor.Sucesso(JsonValue.Create(false)),
        _ => ResultadoValidacaoValor.Falha("Deve ser true ou false."),
    };
}
