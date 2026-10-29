using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Xunit.Abstractions;
using static Catalogo.Api.IntegrationTests.Apoio.PlanilhasDeTeste;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// Desempenho da importação (RN97 a RN102) com o limite de 5.000 linhas: relatórios de teste com 4 referências pelo nome (uma consulta
/// por tipo de item referenciado) e cenários com a dependência do endpoint e a unicidade relatório + número. Fora da execução padrão:
/// <c>RODAR_TESTES_LENTOS=1 dotnet test --filter "FullyQualifiedName~DesempenhoImportacaoTests" --logger "console;verbosity=detailed"</c>.
/// Os tempos (envio, prévia e execução) saem na saída do teste.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class DesempenhoImportacaoTests(BancoDeTestesFixture banco, ITestOutputHelper saida) : TesteDeApi(banco)
{
    private const int Linhas = 5_000;
    private static readonly string Relatorios = UrlEntidade(BancoDeTestesFixture.GrupoDoSeed, "relatorio_teste");
    private static readonly string Cenarios = UrlEntidade(BancoDeTestesFixture.GrupoDoSeed, "cenario_teste");

    [FatoLento]
    public async Task Importar_5000Relatorios_E_5000Cenarios()
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade" });
        var sigla = await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "SG" });
        var projeto = await Criar("projeto", new JsonObject { ["sigla"] = sigla, ["nome"] = "Transferências Alfa" });
        await CriarEndpoint(projeto, "consulta");
        for (var i = 1; i <= 20; i++)
            await Criar("rtc", new JsonObject { ["rtc_externo_id"] = $"RTC-{i}" });
        await Criar("ambiente", new JsonObject { ["nome"] = "Homologação" });
        await Criar("status_resultado", new JsonObject { ["descricao"] = "Aprovado" });

        var relatorios = Xlsx(
            ["titulo", "Projeto *", "RTC", "Ambiente *", "Status *", "Data de execução *", "bateria_testes", "tipo_teste", "executado_por"],
            Enumerable.Range(1, Linhas)
                .Select(i => new object?[] { $"Bateria {i:D5}", "transferencias alfa", $"RTC-{(i % 20) + 1}", "homologacao", "Aprovado",
                    new DateTime(2026, 10, 1).AddMinutes(i), 1, "API", "Ana" })
                .ToArray());
        await MedirAsync("relatórios", Relatorios, relatorios);

        // Os cenários apontam todos para o mesmo relatório (unicidade relatório + número + endpoint, 5.000 números diferentes).
        var cenarios = Xlsx(
            ["relatorio_teste", "endpoint_servico", "numero_cenario", "usuarios_virtuais", "tempo_medio_resposta_ms", "percentil_95_ms",
             "vazao_media_tps", "taxa_erro_percentual"],
            Enumerable.Range(1, Linhas).Select(i => new object?[] { "Bateria 00001", "consulta", i, 100, 250, 480, 35.5, 0.25 }).ToArray());
        await MedirAsync("cenários", Cenarios, cenarios);
    }

    private async Task MedirAsync(string nome, string url, byte[] arquivo)
    {
        var relogio = Stopwatch.StartNew();
        var enviada = await OkAsync(await EnviarAsync(Cliente, url, arquivo, $"{nome}.xlsx"), HttpStatusCode.Created);
        var envio = relogio.Elapsed;

        var mapeamento = new JsonArray(enviada["colunas"]!.AsArray()
            .Select(c => (JsonNode)new JsonObject { ["indice"] = c!["indice"]!.GetValue<int>(), ["destino"] = c["destino"]!.DeepClone() })
            .ToArray());
        relogio.Restart();
        var previa = await ValidarAsync(Cliente, url, enviada["id"]!.GetValue<string>(), mapeamento, equipePadrao: EquipePadrao.Id);
        var validacao = relogio.Elapsed;
        Assert.Equal(Linhas, previa["totais"]!["novos"]!.GetValue<int>());

        relogio.Restart();
        await OkAsync(await ExecutarAsync(Cliente, url, enviada["id"]!.GetValue<string>()));
        var execucao = relogio.Elapsed;

        saida.WriteLine($"{Linhas} {nome} ({arquivo.Length / 1024} KB): envio {envio.TotalSeconds:F2} s, prévia {validacao.TotalSeconds:F2} s, " +
                        $"execução {execucao.TotalSeconds:F2} s");
    }
}
