using System.Text.Json.Nodes;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Exemplos do OpenAPI para os registros, cujo formato depende dos metadados. Usam um cenário de teste da
/// listagem de Testes de Desempenho (docs/listagens/testes-desempenho.md), o mesmo de docs/modelo-de-dados.md.
/// </summary>
internal static class ExemplosOpenApi
{
    private const string Relatorio = "6f1c0d5e-2b7a-4c11-9a3e-1d2c3b4a5f60";
    private const string Endpoint = "a9e8d7c6-b5a4-4f3e-8d2c-1b0a9f8e7d6c";
    private const string Equipe = "3d2b1a09-8f7e-4d6c-9b5a-4e3f2a1b0c9d";

    /// <summary>POST: campos planos; referências como o id do registro apontado; equipeId só para o administrador.</summary>
    public static JsonObject CorpoDeCriacao() => new()
    {
        ["equipeId"] = Equipe,
        ["relatorio_teste"] = Relatorio,
        ["endpoint_servico"] = Endpoint,
        ["numero_cenario"] = 1,
        ["usuarios_virtuais"] = 100,
        ["tempo_medio_resposta_ms"] = 250,
        ["percentil_95_ms"] = 480,
        ["vazao_media_tps"] = 35.5,
        ["taxa_erro_percentual"] = 0.25,
    };

    /// <summary>PUT: o registro inteiro (RN15) e a "versao" lida no GET (RN12).</summary>
    public static JsonObject CorpoDeAtualizacao()
    {
        var corpo = CorpoDeCriacao();
        corpo.Remove("equipeId");
        corpo["versao"] = "AAAAAAAAB9I=";
        corpo["usuarios_virtuais"] = 200;
        return corpo;
    }

    /// <summary>GET: colunas de sistema fora de "dados"; referências como { id, rotulo } (RN20).</summary>
    public static JsonObject RegistroDevolvido() => new()
    {
        ["id"] = "c3a4b5d6-e7f8-4a1b-9c2d-3e4f5a6b7c01",
        ["equipeId"] = Equipe,
        ["criadoEm"] = "2026-10-21T17:30:00.1234567Z",
        ["atualizadoEm"] = null,
        ["versao"] = "AAAAAAAAB9I=",
        ["criadoPor"] = new JsonObject { ["id"] = "0e1d2c3b-4a59-4687-b7a6-958473625140", ["matricula"] = "c123456", ["nome"] = "Maria Souza" },
        ["atualizadoPor"] = null,
        ["situacaoAprovacao"] = "APROVADO",
        ["ultimaDecisao"] = null,
        ["avaliacao"] = null,
        ["dados"] = new JsonObject
        {
            ["relatorio_teste"] = new JsonObject { ["id"] = Relatorio, ["rotulo"] = "Bateria 1" },
            ["endpoint_servico"] = new JsonObject { ["id"] = Endpoint, ["rotulo"] = "consulta" },
            ["numero_cenario"] = 1,
            ["usuarios_virtuais"] = 100,
            ["tempo_medio_resposta_ms"] = 250,
            ["percentil_95_ms"] = 480,
            ["vazao_media_tps"] = 35.50,
            ["taxa_erro_percentual"] = 0.25,
        },
    };
}
