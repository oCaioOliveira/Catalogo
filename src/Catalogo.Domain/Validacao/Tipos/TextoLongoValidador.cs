using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>TEXTO_LONGO: texto sem limite de tamanho (descrições, observações; endereços são URL). RN04.</summary>
public sealed class TextoLongoValidador : ITipoDadoValidador
{
    public TipoDado Tipo => TipoDado.TextoLongo;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.String)
            return ResultadoValidacaoValor.Falha("Deve ser um texto.");

        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(valor.GetString()!.Trim()));
    }
}
