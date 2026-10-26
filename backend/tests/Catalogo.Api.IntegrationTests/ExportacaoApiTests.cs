using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using ClosedXML.Excel;

namespace Catalogo.Api.IntegrationTests;

// Exportação da listagem em CSV e XLSX: mesmos filtros, ordenação e visibilidade da listagem (RN26, RN37).
public class ExportacaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    [Fact]
    public async Task Csv_ComFiltroEOrdenacao_Utf8ComBomPontoEVirgulaEVirgulaDecimal()
    {
        var (relatorio, endpoint) = await CriarRelatorioEEndpointAsync("Bateria Ação Çã");
        foreach (var numero in new[] { 1, 2, 3 })
            await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, numero));

        var resposta = await Cliente.GetAsync($"{UrlRegistros("cenario_teste")}/exportar?formato=csv&numero_cenario.de=2&ordenacao=-numero_cenario");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/csv", resposta.Content.Headers.ContentType!.MediaType);
        Assert.Equal($"cenario_teste_{Hoje()}.csv", resposta.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        Assert.Equal(Bom, bytes[..3]);

        var linhas = LerCsv(bytes);
        Assert.Equal(
            ["Relatório", "Endpoint", "Nº do cenário", "Usuários virtuais", "Tempo médio (ms)", "Percentil 95 (ms)", "Vazão média (TPS)",
             "Taxa de erro (%)", "Equipe", "Criado por", "Criado em", "Atualizado em"],
            linhas[0]);
        Assert.Equal(["3", "2"], linhas.Skip(1).Select(l => l[2]));
        var cenario = linhas[1];
        Assert.Equal("Bateria Ação Çã", cenario[0]);                  // referência pelo rótulo, com os acentos certos
        Assert.Equal("consulta", cenario[1]);
        Assert.Equal("35,50", cenario[6]);                             // vírgula decimal, com a escala do campo
        Assert.Equal(EquipePadrao.Nome, cenario[8]);
        Assert.Matches(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}$", cenario[10]);
        Assert.Equal("", cenario[11]);
    }

    [Fact]
    public async Task Xlsx_ComFiltro_CabecalhoEmNegritoFiltroAutomaticoENumerosEDatasTipados()
    {
        var (relatorio, endpoint) = await CriarRelatorioEEndpointAsync("Bateria 1");
        foreach (var numero in new[] { 1, 2, 3 })
            await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, numero));

        var cenarios = await ExportarXlsx("cenario_teste", "numero_cenario.ate=2&ordenacao=numero_cenario");
        var relatorios = await ExportarXlsx("relatorio_teste", "bateria_testes=Bateria 1");

        using (cenarios)
        using (relatorios)
        {
            var aba = cenarios.Worksheet(1);
            Assert.Equal("Cenário de Teste", aba.Name);
            Assert.True(aba.Cell(1, 1).Style.Font.Bold);
            Assert.True(aba.AutoFilter.IsEnabled);
            Assert.Equal(3, aba.LastRowUsed()!.RowNumber());   // cabeçalho + 2 cenários
            Assert.Equal(XLDataType.Number, aba.Cell(2, 3).DataType);
            Assert.Equal([1d, 2d], new[] { aba.Cell(2, 3).GetDouble(), aba.Cell(3, 3).GetDouble() });
            Assert.Equal(35.5, aba.Cell(2, 7).GetDouble());
            Assert.Equal(XLDataType.DateTime, aba.Cell(2, 11).DataType);   // Criado em

            // DATA_HORA: 14:30 em -03:00 sai como 14:30 no fuso da exportação (America/Sao_Paulo).
            var abaRelatorios = relatorios.Worksheet(1);
            var coluna = abaRelatorios.Row(1).CellsUsed().Single(c => c.GetString() == "Data de execução").Address.ColumnNumber;
            Assert.Equal(XLDataType.DateTime, abaRelatorios.Cell(2, coluna).DataType);
            Assert.Equal(new DateTime(2026, 10, 21, 14, 30, 0), abaRelatorios.Cell(2, coluna).GetDateTime());
        }
    }

    [Fact]
    public async Task UsuarioDeA_NaoExportaRegistrosDeB()
    {
        // RN26: o mesmo escopo de equipes da listagem.
        var equipeB = await CriarEquipeAsync("Equipe B");
        await CriarProjeto("Projeto de A");
        EquipeDosAtalhos = equipeB;
        await CriarProjeto("Projeto de B");
        var (_, internoA) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipePadrao]);

        var csv = LerCsv(await (await internoA.GetAsync($"{UrlRegistros("projeto")}/exportar?formato=csv")).Content.ReadAsByteArrayAsync());
        var deB = await internoA.GetAsync($"{UrlRegistros("projeto")}/exportar?formato=csv&equipeId={equipeB.Id}");

        Assert.Equal(["Projeto de A"], csv.Skip(1).Select(l => l[1]));
        Assert.Equal(HttpStatusCode.Forbidden, deB.StatusCode);
        Assert.Equal("SEM_PERMISSAO", (await LerJson(deB))["codigo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Externo_NaoExportaPendentesDeOutros_EVeASituacaoDosSeus()
    {
        // RN37: com aprovação ativa, o EXTERNO vê os APROVADOS e os seus.
        var projeto = await CriarProjeto("Projeto");
        var rtc = await CriarRtc();
        var ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação" });
        var status = await Criar("status_resultado", new JsonObject { ["descricao"] = "Concluído" });
        await Cliente.PutAsJsonAsync(
            $"/api/v1/equipes/{EquipePadrao.Id}/configuracoes/grupos/testes_desempenho/entidades/relatorio_teste/aprovacao", new { ativa = true });
        var (_, autor) = await CriarUsuarioAsync("externo.autor", Perfil.Externo, [EquipePadrao]);
        var (_, outro) = await CriarUsuarioAsync("externo.outro", Perfil.Externo, [EquipePadrao]);
        var pendente = await Postar(autor, "relatorio_teste", new JsonObject
        {
            ["projeto"] = projeto, ["rtc"] = rtc, ["ambiente"] = ambiente, ["status_resultado"] = status,
            ["data_execucao"] = "2026-10-21T14:30:00-03:00", ["bateria_testes"] = "Pendente do autor", ["tipo_teste"] = "Carga",
            ["executado_por"] = "Fornecedor",
        });
        Assert.Equal(HttpStatusCode.Created, pendente.StatusCode);
        await CriarRelatorio(projeto, bateria: "Aprovado");

        var doOutro = LerCsv(await (await outro.GetAsync($"{UrlRegistros("relatorio_teste")}/exportar?formato=csv")).Content.ReadAsByteArrayAsync());
        var doAutor = LerCsv(await (await autor.GetAsync($"{UrlRegistros("relatorio_teste")}/exportar?formato=csv&ordenacao=bateria_testes")).Content.ReadAsByteArrayAsync());

        var bateria = Array.IndexOf(doOutro[0], "Bateria de testes");
        Assert.Equal(["Aprovado"], doOutro.Skip(1).Select(l => l[bateria]));
        var situacao = Array.IndexOf(doAutor[0], "Situação de aprovação");
        Assert.Equal([("Aprovado", "APROVADO"), ("Pendente do autor", "PENDENTE")], doAutor.Skip(1).Select(l => (l[bateria], l[situacao])));
    }

    [Fact]
    public async Task ComModeloDeAvaliacaoAtivo_ExportaAMediaDeCadaCriterio()
    {
        // RN48: a média que o registro mostra, uma coluna por critério ESTRELAS ativo.
        var relatorio = await CriarRelatorio(await CriarProjeto("Projeto"));
        await Cliente.PostAsJsonAsync($"/api/v1/equipes/{EquipePadrao.Id}/configuracoes/grupos/testes_desempenho/entidades/relatorio_teste/avaliacao",
            new { nome = "Avaliação", criterios = new[] { new { chave = "qualidade", nome = "Qualidade", tipo = "ESTRELAS" } } });
        await Cliente.PutAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}/avaliacoes/minha",
            new { respostas = new { qualidade = new { estrelas = 4 } } });

        var csv = LerCsv(await (await Cliente.GetAsync($"{UrlRegistros("relatorio_teste")}/exportar?formato=csv")).Content.ReadAsByteArrayAsync());

        Assert.Equal("4,0", csv[1][Array.IndexOf(csv[0], "Média: Qualidade")]);
    }

    [Fact]
    public async Task TextoQueComecaComoFormula_NeutralizadoNoCsvENoXlsx()
    {
        await Criar("comunidade", new JsonObject { ["nome"] = "=HYPERLINK(\"http://malicioso\",\"clique\")" });
        await Criar("comunidade", new JsonObject { ["nome"] = "@SOMA(1)" });

        var csv = Encoding.UTF8.GetString((await (await Cliente.GetAsync($"{UrlRegistros("comunidade")}/exportar?formato=csv&ordenacao=nome")).Content.ReadAsByteArrayAsync())[3..]);
        using var xlsx = await ExportarXlsx("comunidade", "");

        Assert.Contains("\"'=HYPERLINK(\"\"http://malicioso\"\",\"\"clique\"\")\"", csv);   // entre aspas por causa das aspas internas
        Assert.Contains("\r\n'@SOMA(1);", csv);
        var nomes = xlsx.Worksheet(1).Column(1).CellsUsed().Skip(1).ToList();
        Assert.Equal(["'=HYPERLINK(\"http://malicioso\",\"clique\")", "'@SOMA(1)"], nomes.Select(c => c.GetString()).Order(StringComparer.Ordinal));
        Assert.All(nomes, c => Assert.False(c.HasFormula));
    }

    [Fact]
    public async Task Url_HiperlinkNoXlsxETextoOriginalNoCsv()
    {
        // ADR 0042: na exportação, a URL também é "clicável".
        var relatorio = await CriarRelatorio(await CriarProjeto("Portal"));
        const string url = "https://grafana.exemplo.com/d/Abc?var-servico=pix%20qr";
        await Criar("link", new JsonObject { ["relatorio_teste"] = relatorio, ["texto_exibicao"] = "Grafana", ["url"] = url });

        using var xlsx = await ExportarXlsx("link", "");
        var csv = LerCsv(await (await Cliente.GetAsync($"{UrlRegistros("link")}/exportar?formato=csv")).Content.ReadAsByteArrayAsync());

        var aba = xlsx.Worksheet(1);
        var coluna = aba.Row(1).CellsUsed().Single(c => c.GetString() == "URL").Address.ColumnNumber;
        var celula = aba.Cell(2, coluna);
        Assert.Equal(url, celula.GetString());
        Assert.True(celula.HasHyperlink);
        Assert.Equal(url, celula.GetHyperlink().ExternalAddress.OriginalString);
        Assert.Equal(url, csv[1][Array.IndexOf(csv[0], "URL")]);
    }

    [Fact]
    public async Task AcimaDoLimite_400ExportacaoMuitoGrande()
    {
        var fabrica = FabricaCom(new Dictionary<string, string> { ["Exportacao:LimiteLinhas"] = "2" });
        foreach (var nome in new[] { "A", "B", "C" })
            await Criar("comunidade", new JsonObject { ["nome"] = nome });

        var resposta = await ClienteDe(Administrador, fabrica).GetAsync($"{UrlRegistros("comunidade")}/exportar?formato=xlsx");
        var filtrada = await ClienteDe(Administrador, fabrica).GetAsync($"{UrlRegistros("comunidade")}/exportar?formato=xlsx&nome=A");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await LerJson(resposta);
        Assert.Equal("EXPORTACAO_MUITO_GRANDE", problema["codigo"]!.GetValue<string>());
        Assert.Contains("Use mais filtros", problema["errors"]!["exportacao"]![0]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, filtrada.StatusCode);   // com filtro cabe no limite
    }

    [Fact]
    public async Task FormatoInvalido_400NoParametroFormato()
    {
        var resposta = await Cliente.GetAsync($"{UrlRegistros("comunidade")}/exportar?formato=pdf");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.True((await LerJson(resposta))["errors"]!.AsObject().ContainsKey("formato"));
    }

    private async Task<(string Relatorio, string Endpoint)> CriarRelatorioEEndpointAsync(string bateria)
    {
        var projeto = await CriarProjeto("Projeto");
        return (await CriarRelatorio(projeto, bateria: bateria), await CriarEndpoint(projeto, "consulta"));
    }

    private async Task<XLWorkbook> ExportarXlsx(string entidade, string filtros)
    {
        var resposta = await Cliente.GetAsync($"{UrlRegistros(entidade)}/exportar?formato=xlsx&{filtros}");
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        Assert.Equal($"{entidade}_{Hoje()}.xlsx", resposta.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
        return new XLWorkbook(new MemoryStream(await resposta.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Linhas e campos do CSV (os dados destes testes não têm ";" nem quebra de linha dentro dos valores).</summary>
    private static List<string[]> LerCsv(byte[] bytes) =>
        Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split(';')).ToList();

    private static string Hoje() =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).ToString("yyyy-MM-dd");
}
