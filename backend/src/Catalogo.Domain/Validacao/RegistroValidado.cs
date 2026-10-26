using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao;

/// <summary>
/// Dados de um registro depois de validados e normalizados, já separados em
/// campos simples (vão para Registro.Dados) e referências (vão para RegistroReferencia). RN07.
/// </summary>
public sealed class RegistroValidado
{
    public RegistroValidado(JsonObject dadosSimples, IReadOnlyList<ReferenciaValidada> referencias)
    {
        DadosSimples = dadosSimples;
        Referencias = referencias;
    }

    /// <summary>Campos simples normalizados. Chave do JSON = Campo.Chave.</summary>
    public JsonObject DadosSimples { get; }

    /// <summary>Campos REFERENCIA preenchidos.</summary>
    public IReadOnlyList<ReferenciaValidada> Referencias { get; }

    /// <summary>
    /// JSON a gravar em Registro.Dados. Acentos ficam legíveis ("Ágil" e não "Ágil"),
    /// o que facilita consultar o banco direto. O encoder "relaxado" só seria arriscado se este
    /// texto fosse colocado direto numa página HTML, o que não acontece.
    /// </summary>
    public string SerializarDadosSimples() => DadosSimples.ToJsonString(OpcoesGravacao);

    /// <summary>JSON de Registro.Dados, no mesmo formato de <see cref="SerializarDadosSimples()"/>.</summary>
    internal static string Serializar(JsonObject dados) => dados.ToJsonString(OpcoesGravacao);

    /// <summary>
    /// Como <see cref="SerializarDadosSimples"/>, acrescentando valores que não passaram pela validação mas precisam
    /// ficar gravados: os de campos INATIVOS, preservados numa atualização (RN69).
    /// </summary>
    public string SerializarDadosSimples(IEnumerable<KeyValuePair<string, JsonNode?>> preservados)
    {
        var dados = (JsonObject)DadosSimples.DeepClone();
        foreach (var (chave, valor) in preservados)
            dados[chave] = valor?.DeepClone();
        return dados.ToJsonString(OpcoesGravacao);
    }

    private static readonly JsonSerializerOptions OpcoesGravacao = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Valor normalizado de um campo, ou nulo se não foi preenchido.
    /// Para REFERENCIA devolve o GUID do registro apontado como texto ("D", minúsculas).
    /// </summary>
    public JsonNode? ObterValor(Campo campo)
    {
        if (campo.EhReferencia)
        {
            var referencia = ObterReferencia(campo);
            return referencia is null ? null : JsonValue.Create(referencia.RegistroDestinoId.ToString("D"));
        }

        return DadosSimples[campo.Chave];
    }

    public ReferenciaValidada? ObterReferencia(Campo campo) =>
        Referencias.FirstOrDefault(r => r.Campo.Id == campo.Id);
}

/// <summary>Um campo REFERENCIA preenchido: qual campo e qual registro ele aponta.</summary>
public sealed record ReferenciaValidada(Campo Campo, Guid RegistroDestinoId);
