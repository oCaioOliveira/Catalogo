using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// Desempenho da agregação do painel (docs/adr/0031) com 20 mil relatórios de teste (e um cenário por relatório), SEM índices
/// novos. Fora da execução padrão: <c>RODAR_TESTES_LENTOS=1 dotnet test --filter "FullyQualifiedName~DesempenhoPainelTests"
/// --logger "console;verbosity=detailed"</c>. Os tempos saem na saída do teste. Se passarem de 2 s, há uma proposta de índices
/// em database/propostas/ (não aplicada).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class DesempenhoPainelTests(BancoDeTestesFixture banco, ITestOutputHelper saida) : TesteDeApi(banco)
{
    private const int Quantidade = 20_000;
    private const int Lote = 2_000;
    private const string UrlPainel = "/api/v1/painel/grupos/testes_desempenho/entidades";

    [FatoLento]
    public async Task Agregacao_DuasDimensoesEDuasMetricas_Com20MilRegistros()
    {
        await GerarDadosAsync();

        await Medir("relatorio_teste", "agruparPor=ambiente,data_execucao:mes&metricas=contagem,media_avaliacao:qualidade");
        await Medir("cenario_teste", "agruparPor=relatorio_teste.ambiente,relatorio_teste.data_execucao:mes&metricas=media:vazao_media_tps,maximo:percentil_95_ms");
        await Medir("relatorio_teste", "agruparPor=status_resultado,criadoEm:mes&metricas=contagem&titulo.contem=1");
    }

    private async Task Medir(string entidade, string consulta)
    {
        var url = $"{UrlPainel}/{entidade}/indicadores?{consulta}&limite=500";
        var tempos = new List<double>();
        JsonNode? corpo = null;
        for (var i = 0; i < 4; i++)   // a primeira é aquecimento (compilação da consulta, cache do SQL Server)
        {
            var relogio = Stopwatch.StartNew();
            var resposta = await Cliente.GetAsync(url);
            relogio.Stop();
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            corpo = await LerJson(resposta);
            if (i > 0)
                tempos.Add(relogio.Elapsed.TotalMilliseconds);
        }

        var linhas = corpo!["linhas"]!.AsArray();
        saida.WriteLine($"{entidade}?{consulta}: {linhas.Count} linhas, soma das contagens {linhas.Sum(l => l!["contagem"]!.GetValue<int>())}; " +
                        $"tempos (ms) {string.Join(" / ", tempos.Select(t => t.ToString("F0")))}; mediana {tempos.Order().ElementAt(tempos.Count / 2):F0} ms");
        Assert.True(tempos.Min() < 30_000, "Agregação absurdamente lenta.");
    }

    /// <summary>
    /// Os registros vão DIRETO para as tabelas (Registro, RegistroValor, RegistroReferencia), em lotes, sem passar pela API:
    /// pela API seriam 40 mil POSTs. Sem chaves únicas: o teste mede só a leitura.
    /// </summary>
    private async Task GerarDadosAsync()
    {
        var ambientes = new List<Guid>();
        for (var i = 1; i <= 5; i++)
            ambientes.Add(Guid.Parse(await Criar("ambiente", new JsonObject { ["nome"] = $"Ambiente {i}" })));
        var status = new List<Guid>();
        foreach (var descricao in new[] { "Sucesso", "Falha", "Parcial" })
            status.Add(Guid.Parse(await Criar("status_resultado", new JsonObject { ["descricao"] = descricao })));
        var projeto = Guid.Parse(await CriarProjeto("Desempenho"));
        var rtc = Guid.Parse(await CriarRtc());
        var endpoint = Guid.Parse(await CriarEndpoint(projeto.ToString(), "carga"));
        // Modelo de avaliação ativo (para "media_avaliacao:qualidade" ser aceita); sem notas, a média sai nula, mas o JOIN é feito.
        var modelo = await Cliente.PostAsJsonAsync(
            $"/api/v1/equipes/{EquipePadrao.Id}/configuracoes/grupos/testes_desempenho/entidades/relatorio_teste/avaliacao", new JsonObject
            {
                ["nome"] = "Avaliação",
                ["criterios"] = new JsonArray(new JsonObject { ["chave"] = "qualidade", ["nome"] = "Qualidade", ["tipo"] = "ESTRELAS" }),
            });
        Assert.Equal(HttpStatusCode.Created, modelo.StatusCode);

        Grupo grupo;
        using (var escopo = Banco.Fabrica.Services.CreateScope())
            grupo = (await escopo.ServiceProvider.GetRequiredService<IMetadadosProvider>().ObterGrupoAsync(BancoDeTestesFixture.GrupoDoSeed, default))!;
        var relatorio = grupo.ObterEntidade("relatorio_teste")!;
        var cenario = grupo.ObterEntidade("cenario_teste")!;
        var ambienteDe = new Dictionary<string, Guid> { ["ambiente"] = grupo.ObterEntidade("ambiente")!.Id, ["status_resultado"] = grupo.ObterEntidade("status_resultado")!.Id };

        var aleatorio = new Random(42);
        var cronometro = Stopwatch.StartNew();
        for (var inicio = 0; inicio < Quantidade; inicio += Lote)
        {
            using var escopo = Banco.Fabrica.Services.CreateScope();
            var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            contexto.ChangeTracker.AutoDetectChangesEnabled = false;

            for (var i = inicio; i < inicio + Lote; i++)
            {
                var criadoEm = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddHours(aleatorio.Next(0, 24 * 600));
                var execucao = criadoEm.AddHours(-aleatorio.Next(1, 48));
                var r = NovoRegistro(relatorio, criadoEm);
                Referencia(r, relatorio, "projeto", projeto, grupo.ObterEntidade("projeto")!.Id);
                Referencia(r, relatorio, "rtc", rtc, grupo.ObterEntidade("rtc")!.Id);
                Referencia(r, relatorio, "ambiente", ambientes[aleatorio.Next(ambientes.Count)], ambienteDe["ambiente"]);
                Referencia(r, relatorio, "status_resultado", status[aleatorio.Next(status.Count)], ambienteDe["status_resultado"]);
                Valor(r, relatorio, "data_execucao", data: execucao);
                Valor(r, relatorio, "titulo", texto: $"Bateria {i % 200}");
                Valor(r, relatorio, "bateria_testes", numero: i % 200 + 1);
                Valor(r, relatorio, "tipo_teste", texto: "API");
                Valor(r, relatorio, "executado_por", texto: "Gerador");

                var c = NovoRegistro(cenario, criadoEm);
                Referencia(c, cenario, "relatorio_teste", r.Id, relatorio.Id);
                Referencia(c, cenario, "endpoint_servico", endpoint, grupo.ObterEntidade("endpoint_servico")!.Id);
                Valor(c, cenario, "numero_cenario", numero: 1);
                Valor(c, cenario, "usuarios_virtuais", numero: aleatorio.Next(10, 500));
                Valor(c, cenario, "tempo_medio_resposta_ms", numero: aleatorio.Next(50, 900));
                Valor(c, cenario, "percentil_95_ms", numero: aleatorio.Next(100, 2_000));
                Valor(c, cenario, "vazao_media_tps", numero: Math.Round((decimal)(aleatorio.NextDouble() * 200), 2));
                Valor(c, cenario, "taxa_erro_percentual", numero: Math.Round((decimal)(aleatorio.NextDouble() * 5), 2));

                contexto.Registros.AddRange(r, c);
            }

            await contexto.SaveChangesAsync();
        }

        saida.WriteLine($"Gerados {Quantidade} relatórios e {Quantidade} cenários em {cronometro.Elapsed.TotalSeconds:F1} s.");

        Registro NovoRegistro(Entidade entidade, DateTime criadoEm) => new()
        {
            Id = Guid.NewGuid(),
            EntidadeId = entidade.Id,
            EquipeId = EquipePadrao.Id,
            CriadoPorId = Administrador.Id,
            SituacaoAprovacao = SituacaoAprovacao.Aprovado,
            CriadoEm = criadoEm,
        };

        void Referencia(Registro origem, Entidade entidade, string campo, Guid destino, Guid entidadeDestino) =>
            origem.Referencias.Add(new RegistroReferencia
            {
                RegistroOrigemId = origem.Id, EntidadeOrigemId = entidade.Id, CampoId = entidade.ObterCampo(campo)!.Id,
                RegistroDestinoId = destino, EntidadeDestinoId = entidadeDestino,
            });

        void Valor(Registro registro, Entidade entidade, string campo, string? texto = null, decimal? numero = null, DateTime? data = null) =>
            registro.Valores.Add(new RegistroValor
            {
                RegistroId = registro.Id, EntidadeId = entidade.Id, CampoId = entidade.ObterCampo(campo)!.Id,
                ValorTexto = texto, ValorNumero = numero, ValorData = data,
            });
    }
}
