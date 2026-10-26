using System.Text.Json.Nodes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.CasosDeUso.Metadados;

/// <summary>Passos comuns aos casos de uso de campo.</summary>
internal static class CampoComum
{
    public static DefinicaoCampo Definicao(CampoEntrada entrada) => new(
        entrada.Chave, entrada.Nome, entrada.TipoDado, entrada.Obrigatorio, entrada.TamanhoMaximo, entrada.Precisao, entrada.Escala,
        entrada.Opcoes, entrada.EntidadeReferenciada, entrada.AcaoExclusao, entrada.Ordem);

    /// <summary>
    /// RN60 e RN61: o valor padrão só vale ao criar um campo obrigatório ou torná-lo obrigatório, e tem de ser um valor válido
    /// do tipo do campo (o MESMO validador dos registros, RN04). Devolve o valor normalizado, ou nulo se não veio ou é inválido.
    /// </summary>
    public static JsonNode? ValidarValorPadrao(
        ValidadorRegistro validador, Entidade entidade, Campo? atual, Campo novo, JsonNode? valorPadrao, ErrosPorCampo erros)
    {
        var antes = erros.ParaDicionario().Count;
        RegrasMetadados.ValidarValorPadrao(entidade, atual, novo, valorPadrao is not null, erros);
        if (valorPadrao is null || erros.ParaDicionario().Count > antes)
            return null;

        var resultado = validador.ValidarValor(novo, valorPadrao);
        if (!resultado.Valido)
        {
            erros.Adicionar("valorPadrao", resultado.Erro!);
            return null;
        }

        return resultado.ValorNormalizado;
    }
}
