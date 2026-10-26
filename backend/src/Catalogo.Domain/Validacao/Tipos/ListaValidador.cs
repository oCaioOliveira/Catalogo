using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>
/// LISTA: o valor precisa ser exatamente uma das Campo.Opcoes (diferencia maiúsculas,
/// para o valor gravado ser sempre o mesmo texto cadastrado). RN04.
/// </summary>
public sealed class ListaValidador : ITipoDadoValidador
{
    public TipoDado Tipo => TipoDado.Lista;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString()!.Trim() : null;

        if (texto is null || !campo.Opcoes.Contains(texto, StringComparer.Ordinal))
            return ResultadoValidacaoValor.Falha($"Deve ser uma das opções: {string.Join(", ", campo.Opcoes)}.");

        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(texto));
    }
}
