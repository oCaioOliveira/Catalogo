using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao;

/// <summary>
/// Estratégia de validação de UM tipo de dado (padrão Strategy): existe uma implementação
/// por <see cref="TipoDado"/> em Validacao/Tipos. O <see cref="ValidadorRegistro"/> escolhe a
/// estratégia pelo tipo do campo, então nenhum outro lugar precisa de "switch" por tipo.
/// </summary>
public interface ITipoDadoValidador
{
    /// <summary>Tipo de dado tratado por esta estratégia.</summary>
    TipoDado Tipo { get; }

    /// <summary>
    /// Valida e NORMALIZA um valor (RN04). Nunca recebe nulo nem texto em branco:
    /// valor vazio é tratado antes, pela regra de obrigatoriedade (RN03).
    /// </summary>
    ResultadoValidacaoValor Validar(Campo campo, JsonElement valor);
}

/// <summary>Resultado da validação de um valor: o valor normalizado ou a mensagem de erro.</summary>
public sealed class ResultadoValidacaoValor
{
    private ResultadoValidacaoValor(JsonNode? valorNormalizado, string? erro)
    {
        ValorNormalizado = valorNormalizado;
        Erro = erro;
    }

    /// <summary>Valor pronto para gravar (ex.: texto sem espaços nas pontas, data em ISO 8601).</summary>
    public JsonNode? ValorNormalizado { get; }

    /// <summary>Mensagem para o usuário quando o valor é inválido.</summary>
    public string? Erro { get; }

    public bool Valido => Erro is null;

    public static ResultadoValidacaoValor Sucesso(JsonNode valorNormalizado) => new(valorNormalizado, null);

    public static ResultadoValidacaoValor Falha(string erro) => new(null, erro);
}
