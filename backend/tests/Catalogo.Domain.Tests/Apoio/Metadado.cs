using System.Text.Json;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Tests.Apoio;

/// <summary>Atalhos para montar metadados nos testes sem repetir todas as propriedades.</summary>
internal static class Metadado
{
    public static readonly Guid EntidadeId = Guid.Parse("00000000-0000-0000-0000-0000000000e1");

    public static Campo Campo(
        string chave,
        TipoDado tipo,
        bool obrigatorio = false,
        int? tamanhoMaximo = null,
        byte? precisao = null,
        byte? escala = null,
        IReadOnlyList<string>? opcoes = null,
        Guid? entidadeReferenciadaId = null,
        int ordem = 0,
        bool ativo = true,
        Guid? entidadeId = null) => new()
    {
        Id = Guid.NewGuid(),
        EntidadeId = entidadeId ?? EntidadeId,
        Chave = chave,
        Nome = chave,
        TipoDado = tipo,
        Obrigatorio = obrigatorio,
        TamanhoMaximo = tamanhoMaximo,
        Precisao = precisao,
        Escala = escala,
        Opcoes = opcoes ?? [],
        EntidadeReferenciadaId = entidadeReferenciadaId,
        AcaoExclusao = tipo == TipoDado.Referencia ? Domain.Metadados.AcaoExclusao.Restringir : null,
        Ordem = ordem,
        Ativo = ativo,
    };

    public static Entidade Entidade(
        IReadOnlyList<Campo> campos,
        IReadOnlyList<RestricaoUnica>? restricoes = null,
        IReadOnlyList<CampoDependencia>? dependencias = null,
        Guid? id = null) => new()
    {
        Id = id ?? EntidadeId,
        GrupoId = Guid.Empty,
        Chave = "entidade_teste",
        Nome = "Entidade de Teste",
        Campos = campos,
        RestricoesUnicas = restricoes ?? [],
        Dependencias = dependencias ?? [],
    };

    public static RestricaoUnica Restricao(string nome, params Campo[] campos) => new()
    {
        Id = Guid.NewGuid(),
        EntidadeId = EntidadeId,
        Nome = nome,
        Campos = campos,
    };

    /// <summary>Converte um trecho JSON (ex.: "\"abc\"", "12.5", "true") em JsonElement.</summary>
    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
