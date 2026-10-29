using Catalogo.Migracao.Cenarios;

namespace Catalogo.Migracao.Tests;

public class ColunasCenarioTests
{
    // Cabeçalhos reais do CSV (91 variações), com o sentido esperado.
    [Theory]
    [InlineData("Cenário", SentidoColuna.Cenario)]
    [InlineData("Nome do Serviço", SentidoColuna.Servico)]
    [InlineData("Funcionalidade", SentidoColuna.Servico)]
    [InlineData("Serviço(s)", SentidoColuna.Servico)]
    [InlineData("Método", SentidoColuna.Metodo)]
    [InlineData("Endpoint(s)", SentidoColuna.Endpoint)]
    [InlineData("Endpoints(s)", SentidoColuna.Endpoint)]
    [InlineData("Fila(s)", SentidoColuna.Endpoint)]
    [InlineData("Usuários Virtuais (Threads simultâneas)", SentidoColuna.Usuarios)]
    [InlineData("VUs", SentidoColuna.Usuarios)]
    [InlineData("Quantidade de Usuários", SentidoColuna.Usuarios)]
    [InlineData("Vazão média total (Transações/segundo)", SentidoColuna.Vazao)]
    [InlineData("Throughput (req/s)", SentidoColuna.Vazao)]
    [InlineData("TPS (Vazão)", SentidoColuna.Vazao)]
    [InlineData("Vazão (média transações /s)", SentidoColuna.Vazao)]
    [InlineData("Tempo médio de resposta (milissegundos)", SentidoColuna.TempoMedio)]
    [InlineData("Tempo Médio de Resposta (ms)", SentidoColuna.TempoMedio)]
    [InlineData("Média (ms)", SentidoColuna.TempoMedio)]
    [InlineData("Average (ms)", SentidoColuna.TempoMedio)]
    [InlineData("Percentil 95 (milissegundos)", SentidoColuna.Percentil95)]
    [InlineData("Percentil 95(milisegundos)", SentidoColuna.Percentil95)]
    [InlineData("95% (ms)", SentidoColuna.Percentil95)]
    [InlineData("p95 (ms)", SentidoColuna.Percentil95)]
    [InlineData("Linha 95% (milissegundos)", SentidoColuna.Percentil95)]
    [InlineData("Amostras", SentidoColuna.Amostras)]
    [InlineData("Erros %", SentidoColuna.Erros)]
    [InlineData("Erros (%)", SentidoColuna.Erros)]
    [InlineData("\u200bERROS%", SentidoColuna.Erros)]
    public void Reconhecer_PeloSentido(string cabecalho, SentidoColuna esperado)
    {
        var coluna = ColunasCenario.Reconhecer(cabecalho);

        Assert.NotNull(coluna);
        Assert.Equal(esperado, coluna.Sentido);
        Assert.Equal(1m, coluna.Fator);
    }

    [Fact]
    public void Reconhecer_TempoEmSegundos_ConverteParaMilissegundos()
    {
        Assert.Equal(1000m, ColunasCenario.Reconhecer("Tempo médio de resposta (segundos)")!.Fator);
        Assert.Equal(1000m, ColunasCenario.Reconhecer("Percentil 95 (segundos)")!.Fator);
        Assert.Equal(1m, ColunasCenario.Reconhecer("Percentil 95 (milissegundos)")!.Fator);
    }

    [Fact]
    public void Reconhecer_VazaoPorMinuto_ConverteParaPorSegundo()
    {
        var coluna = ColunasCenario.Reconhecer("Vazão média total (transações/minuto)")!;

        Assert.Equal(SentidoColuna.Vazao, coluna.Sentido);
        Assert.Equal(1m / 60m, coluna.Fator);
    }

    [Theory]
    [InlineData("CPU (%)")]
    [InlineData("Memória (MB)")]
    [InlineData("p99 (ms)")]
    [InlineData("Vazão máxima total (transações/segundo)")]
    [InlineData("Tempo de consumo (440 mil mensagens)")]
    [InlineData("Cenário Gestor")]
    [InlineData("")]
    public void Reconhecer_OutroSentido_NaoReconhece(string cabecalho)
    {
        Assert.Null(ColunasCenario.Reconhecer(cabecalho));
    }
}

public class NumeroBrasileiroTests
{
    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1132", 1132)]
    [InlineData("0,00", 0)]
    [InlineData("645,1", 645.1)]
    [InlineData("1.178", 1178)]               // ponto sozinho com 3 dígitos: milhar (padrão brasileiro)
    [InlineData("1.234.567", 1234567)]
    [InlineData("0.25", 0.25)]                // "0." é sempre decimal
    [InlineData("591.5", 591.5)]
    [InlineData("20 VUs", 20)]
    [InlineData("17 ms", 17)]
    [InlineData("0,25%", 0.25)]
    [InlineData("1.700 TPS (Total)", 1700)]
    [InlineData("50 (compartilhado)", 50)]
    [InlineData("50 (compartilhados", 50)]
    [InlineData("16% *", 16)]
    [InlineData("9.7 mil TPS", 9700)]
    [InlineData("12,5 mil TPS", 12500)]
    [InlineData("720.7/sec", 720.7)]
    [InlineData("10UV", 10)]
    [InlineData("376 TPS OKD", 376)]
    public void Ler_NumerosComUnidadesENotas(string celula, double esperado)
    {
        var leitura = NumeroBrasileiro.Ler(celula, EstiloPonto.Milhar);

        Assert.True(leitura.Valida, leitura.Problema);
        Assert.Equal((decimal)esperado, leitura.Valor);
    }

    [Fact]
    public void Ler_NotaVaiParaAObservacao()
    {
        Assert.Contains("compartilhados", NumeroBrasileiro.Ler("3000 (compartilhados)", EstiloPonto.Milhar).Observacao);
    }

    [Fact]
    public void Ler_PorMinuto_TrazAUnidade()
    {
        Assert.Equal("/min", NumeroBrasileiro.Ler("12.2/min", EstiloPonto.Decimal).Unidade);
        Assert.Equal("/s", NumeroBrasileiro.Ler("5,1/seg", EstiloPonto.Milhar).Unidade);
    }

    [Fact]
    public void Ler_PontoComTresDigitos_NaColunaComPontoDecimal_EhDecimalEFicaRegistrado()
    {
        var leitura = NumeroBrasileiro.Ler("1.178", EstiloPonto.Decimal);

        Assert.Equal(1.178m, leitura.Valor);
        Assert.Contains("ponto decimal", leitura.Observacao);
    }

    [Fact]
    public void EstiloDaColuna_PelosOutrosValores()
    {
        // Mesma tabela real: a vazão usa ponto decimal ("119.3"), o percentil 95 usa ponto de milhar ("1.178").
        Assert.Equal(EstiloPonto.Decimal, NumeroBrasileiro.EstiloDaColuna(["119.3", "98.0", "72.9"]));
        Assert.Equal(EstiloPonto.Milhar, NumeroBrasileiro.EstiloDaColuna(["602", "1.178", "2.984"]));
        Assert.Equal(EstiloPonto.Milhar, NumeroBrasileiro.EstiloDaColuna(["645,1", "1.132"]));
    }

    [Theory]
    [InlineData("10 a 100")]
    [InlineData("75–84")]
    [InlineData("Injetora#1 500")]
    [InlineData("1169,5 + 1169,5")]
    public void Ler_FaixasETextos_NaoSaoNumeros(string celula)
    {
        var leitura = NumeroBrasileiro.Ler(celula, EstiloPonto.Milhar);

        Assert.False(leitura.Valida);
        Assert.False(leitura.Vazia);
        Assert.NotNull(leitura.Problema);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("N/A")]
    [InlineData("n/d")]
    public void Ler_Vazios(string celula)
    {
        Assert.True(NumeroBrasileiro.Ler(celula, EstiloPonto.Milhar).Vazia);
    }
}

public class ExtratorCenariosTests
{
    // Tabela no modelo padrão do SharePoint: cabeçalho em 2 linhas (nome e unidade, com rowspan), "1." como número do cenário.
    private const string TabelaPadrao = """
        <table>
          <tr><td rowspan="2">Cenário</td><td rowspan="2">Nome do Serviço</td><td rowspan="2">Método</td><td rowspan="2">Endpoint(s)</td>
              <td>Usuários Virtuais</td><td>Vazão média total</td><td>Tempo médio de resposta</td><td>Percentil 95</td><td rowspan="2">Erros %</td></tr>
          <tr><td>(Threads simultâneas)</td><td>(Transações/segundo)</td><td>(milissegundos)</td><td>(milissegundos)</td></tr>
          <tr><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr>
          <tr><td>1.</td><td>Consultar Campanha</td><td>GET</td><td>/v2/campanha</td><td>20</td><td>1.132</td><td>17</td><td>21</td><td>0,00</td></tr>
          <tr><td>2.</td><td>Consultar Adesão</td><td>GET</td><td>/v1/campanha-adesao-conta/{nuConta}</td><td>20</td><td>590,2</td><td>33</td><td>49</td><td>0,25</td></tr>
        </table>
        """;

    [Fact]
    public void Extrair_TabelaPadrao_UmCenarioPorLinha()
    {
        var resultado = ExtratorCenarios.Extrair(TabelaPadrao, "/v2/campanha\n/v1/campanha-adesao-conta/{nuConta}");

        Assert.Equal(2, resultado.Cenarios.Count);
        var primeiro = resultado.Cenarios[0];
        Assert.Equal((1, "GET", "/v2/campanha", "Consultar Campanha"), (primeiro.Numero, primeiro.Endpoint.Metodo, primeiro.Endpoint.Endpoint, primeiro.Endpoint.Nome));
        Assert.Equal((20, 1132m, 17m, 21m, 0m), (primeiro.Usuarios, primeiro.Vazao, primeiro.TempoMedio, primeiro.Percentil95, primeiro.Erros));
        Assert.Equal(0.25m, resultado.Cenarios[1].Erros);
        Assert.Null(resultado.TextoObservacoes);
    }

    [Theory]
    [InlineData("1191 seg", "não é um tempo de resposta plausível")] // relatório real: média de 274 ms e p95 "1191 seg"
    [InlineData("14 seg ms", "duas unidades de tempo")]
    public void Extrair_PercentilEmSegundosAmbiguo_FicaVazioEVaiParaAsObservacoes(string celula, string motivo)
    {
        var html = TabelaPadrao.Replace("<td>17</td><td>21</td>", $"<td>274 ms</td><td>{celula}</td>");

        var resultado = ExtratorCenarios.Extrair(html, "");

        Assert.Equal(2, resultado.Cenarios.Count);
        Assert.Equal(274m, resultado.Cenarios[0].TempoMedio);
        Assert.Null(resultado.Cenarios[0].Percentil95);
        Assert.Contains(celula, resultado.TextoObservacoes);
        Assert.Contains(resultado.NumerosAmbiguos, n => n.Contains(motivo, StringComparison.Ordinal));
    }

    [Fact]
    public void Extrair_PercentilEmSegundosPlausivel_ConverteParaMilissegundos()
    {
        var resultado = ExtratorCenarios.Extrair(TabelaPadrao.Replace("<td>17</td><td>21</td>", "<td>709 ms</td><td>5,6 seg</td>"), "");

        Assert.Equal(5600m, resultado.Cenarios[0].Percentil95);
    }

    [Fact]
    public void Extrair_TempoMedioEmSegundosAmbiguo_LinhaNaoViraCenario()
    {
        var resultado = ExtratorCenarios.Extrair(TabelaPadrao.Replace("<td>17</td><td>21</td>", "<td>1191 seg</td><td>21</td>"), "");

        Assert.Single(resultado.Cenarios);
        Assert.Contains("1191 seg", resultado.TextoObservacoes);
    }

    [Fact]
    public void DividirEndpoints_RotulosAntesDoCaminho_ViramONomeDoEndpoint()
    {
        // Relatório real (SIIFX): "CANCELAMENTO:" e "ETAPA 1:" são rótulos do caminho, não endpoints.
        var (endpoints, nota) = ExtratorCenarios.DividirEndpoints("", "", "CANCELAMENTO:\nETAPA 1:\n/pub/investimentos/v1/contrato?", null, []);

        var endpoint = Assert.Single(endpoints);
        Assert.Equal(("CANCELAMENTO – ETAPA 1", "/pub/investimentos/v1/contrato?"), (endpoint.Nome, endpoint.Endpoint));
        Assert.Null(nota);
    }

    [Theory]
    [InlineData("/api/v1/clientes?\ncampos=enderecos&classe=01", "/api/v1/clientes?campos=enderecos&classe=01")]
    [InlineData("/api/v1/cartoes?agencia=1\n&contaCartao=000200000113", "/api/v1/cartoes?agencia=1&contaCartao=000200000113")]
    [InlineData("/api/v1/\nrestricao?documento={documento}", "/api/v1/restricao?documento={documento}")]
    [InlineData("https://portal/\n${link}", "https://portal/${link}")]
    public void DividirEndpoints_CaminhoQuebradoEmLinha_EhUmEndpointSo(string celula, string esperado)
    {
        var (endpoints, nota) = ExtratorCenarios.DividirEndpoints("", "GET", celula, null, []);

        Assert.Equal(esperado, Assert.Single(endpoints).Endpoint);
        Assert.Contains("continuação do caminho anterior", nota);
    }

    [Fact]
    public void DividirEndpoints_FraseSemCaminho_VaiParaANota()
    {
        var (endpoints, nota) = ExtratorCenarios.DividirEndpoints("", "", "/api/v1/pix\nSem controle de vazão", null, []);

        Assert.Equal("/api/v1/pix", Assert.Single(endpoints).Endpoint);
        Assert.Contains("\"Sem controle de vazão\" é uma nota", nota);
    }

    [Fact]
    public void DividirEndpoints_CelulaSemCaminhos_CadaLinhaContinuaUmEndpoint()
    {
        // Filas MQ e transações WEB não têm "/": nada muda para elas.
        var (endpoints, _) = ExtratorCenarios.DividirEndpoints("", "", "QL.REQ.PIX.ENVIO\nTransação Web", null, []);

        Assert.Equal(["QL.REQ.PIX.ENVIO", "Transação Web"], endpoints.Select(e => e.Endpoint));
    }

    [Fact]
    public void Extrair_LinhaDeTituloETextoForaDaTabela_VaoParaAsObservacoes()
    {
        var html = "<p>Resultados abaixo.</p>" + TabelaPadrao.Replace("<table>", "<table><tr><td colspan=\"9\">TABELA COMPARATIVA DE EXECUÇÃO</td></tr>");

        var resultado = ExtratorCenarios.Extrair(html, "");

        Assert.Equal(2, resultado.Cenarios.Count);
        Assert.Contains("Resultados abaixo.", resultado.TextoObservacoes);
        Assert.Contains("TABELA COMPARATIVA DE EXECUÇÃO", resultado.TextoObservacoes);
    }

    [Fact]
    public void Extrair_CelulaMescladaComUmValorPorCenario_CadaCenarioComOSeu()
    {
        // Caso real (relatório 22): a vazão e o tempo de 2 cenários numa célula mesclada, um valor por linha, com uma linha
        // "espaçadora" sem identificação.
        var html = """
            <table>
              <tr><td>Cenário</td><td>Nome do Serviço</td><td>Método</td><td>Endpoint(s)</td><td>Usuários Virtuais</td><td>Vazão média total (Transações/segundo)</td>
                  <td>Tempo médio de resposta (milissegundos)</td><td>Erros %</td></tr>
              <tr><td>1.</td><td>Solicitar Template</td><td>POST</td><td>/v1/solicitar-template</td><td rowspan="3">70</td><td rowspan="3">176,5<br>178</td>
                  <td rowspan="3">372<br>10</td><td rowspan="3">0,00</td></tr>
              <tr><td></td><td></td><td></td><td></td></tr>
              <tr><td>2.</td><td>Gerar retorno sensor</td><td>GET</td><td>/v1/gerar-retorno-sensor</td></tr>
            </table>
            """;

        var resultado = ExtratorCenarios.Extrair(html, "");

        Assert.Equal([(1, 176.5m, 372m), (2, 178m, 10m)], resultado.Cenarios.Select(c => (c.Numero, c.Vazao, c.TempoMedio)));
    }

    [Fact]
    public void Extrair_VariosEndpointsNaCelulaComUmValorPorLinha_UmCenarioPorEndpoint()
    {
        var html = """
            <table>
              <tr><td>Cenário</td><td>Endpoint(s)</td><td>VUs</td><td>Vazão (TPS)</td><td>Média (ms)</td><td>Erros %</td></tr>
              <tr><td>1</td><td>GET<br>/a<br>POST<br>/b</td><td>50 (compartilhados)</td><td>7050,7<br>7555,9</td><td>12<br>14</td><td>0,00<br>0,00</td></tr>
            </table>
            """;

        var resultado = ExtratorCenarios.Extrair(html, "");

        Assert.Equal([("GET", "/a", 7050.7m), ("POST", "/b", 7555.9m)], resultado.Cenarios.Select(c => (c.Endpoint.Metodo, c.Endpoint.Endpoint, c.Vazao)));
        Assert.All(resultado.Cenarios, c => Assert.Equal(50, c.Usuarios));
        Assert.Contains("compartilhados", resultado.TextoObservacoes);
    }

    [Fact]
    public void Extrair_EndpointSoComMetodoENome_ProcuradoNaColunaEndpointsFila()
    {
        // Caso real (relatório 677): "POST - Envia Notificações" na tabela; o caminho está na coluna "Endpoints / Fila".
        var html = """
            <table>
              <tr><td>Cenário</td><td>Endpoint(s)</td><td>Usuários Virtuais</td><td>Vazão média total (Transações/segundo)</td><td>Tempo médio de resposta (milissegundos)</td><td>Erros %</td></tr>
              <tr><td>1</td><td>POST - Envia Notificações</td><td>200</td><td>523</td><td>318</td><td>0</td></tr>
              <tr><td>2</td><td>GET - Cosulta Notifcação</td><td>200</td><td>437</td><td>380</td><td>0</td></tr>
            </table>
            """;
        var endpointsFila = "POST Envia Notificação:/push/v3/enviar\nGET Consulta Notificação:/push/v3/mensagens/{identificador}\nPUT Atualiza Notificação:/push/v3/mensagens/{identificador}";

        var resultado = ExtratorCenarios.Extrair(html, endpointsFila);

        Assert.Equal(["/push/v3/enviar", "/push/v3/mensagens/{identificador}"], resultado.Cenarios.Select(c => c.Endpoint.Endpoint));
        Assert.Equal(["POST", "GET"], resultado.Cenarios.Select(c => c.Endpoint.Metodo));
    }

    [Fact]
    public void Extrair_TabelaSemCabecalho_ContinuaAAnterior()
    {
        var segunda = "<table><tr><td>3.</td><td>Detalhar</td><td>GET</td><td>/api/detalhe</td><td>20</td><td>591.5</td><td>33</td><td>46</td><td>0,30</td></tr></table>";

        var resultado = ExtratorCenarios.Extrair(TabelaPadrao + segunda, "");

        Assert.Equal(3, resultado.Cenarios.Count);
        Assert.Equal(591.5m, resultado.Cenarios[2].Vazao);
        Assert.Contains(resultado.Anomalias, a => a.Contains("não tem cabeçalho"));
    }

    [Fact]
    public void Extrair_TabelaDeOutroSentido_InteiraNasObservacoes()
    {
        var html = "<table><tr><td>Experimento</td><td>Comportamento observado</td><td>Erros %</td></tr><tr><td>AKS Pod Fail</td><td>100% erros 503</td><td>100%</td></tr></table>";

        var resultado = ExtratorCenarios.Extrair(html, "");

        Assert.Empty(resultado.Cenarios);
        Assert.Equal(1, resultado.TabelasNaoReconhecidas);
        Assert.Contains("AKS Pod Fail | 100% erros 503 | 100%", resultado.TextoObservacoes);
    }

    [Fact]
    public void Extrair_ValorObrigatorioAmbiguo_LinhaNaoImportadaENasObservacoes()
    {
        var html = TabelaPadrao.Replace("<td>20</td><td>590,2</td>", "<td>10 a 100</td><td>590,2</td>");

        var resultado = ExtratorCenarios.Extrair(html, "");

        Assert.Single(resultado.Cenarios);
        Assert.Equal(1, resultado.LinhasNaoImportadas);
        Assert.Contains("10 a 100", resultado.TextoObservacoes);
    }

    [Fact]
    public void Extrair_TextoSemTabela_TudoNasObservacoes()
    {
        var resultado = ExtratorCenarios.Extrair("<div>N/D</div>", "");

        Assert.Empty(resultado.Cenarios);
        Assert.Equal("N/D", resultado.TextoObservacoes);
    }

    [Theory]
    [InlineData("1.", 1, null)]
    [InlineData("3º", 3, null)]
    [InlineData("Cenário 2", 2, null)]
    [InlineData("2 - Pico", 2, "2 - Pico")]
    [InlineData("A *", 7, "A *")]
    public void NumeroCenario(string celula, int numero, string? descricao)
    {
        Assert.Equal((numero, descricao), ExtratorCenarios.NumeroCenario(celula, 7, temColuna: true));
    }
}
