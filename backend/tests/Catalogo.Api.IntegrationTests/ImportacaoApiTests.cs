using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Application.CasosDeUso.Importacoes;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Catalogo.Api.IntegrationTests.Apoio.PlanilhasDeTeste;

namespace Catalogo.Api.IntegrationTests;

// Importação de itens por planilha (RN97 a RN104): as mesmas regras do cadastro manual, linha a linha.
public class ImportacaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private static readonly string Relatorios = UrlEntidade(BancoDeTestesFixture.GrupoDoSeed, "relatorio_teste");
    private static readonly string Cenarios = UrlEntidade(BancoDeTestesFixture.GrupoDoSeed, "cenario_teste");
    private static readonly string Comunidades = UrlEntidade(BancoDeTestesFixture.GrupoDoSeed, "comunidade");

    private static readonly string[] CabecalhosRelatorio =
        ["projeto", "rtc", "ambiente", "status_resultado", "data_execucao", "bateria_testes", "tipo_teste", "executado_por"];

    /// <summary>Projeto "Transferências Alfa", RTC "RTC-100", ambiente "Homologação" e status "Aprovado", na equipe dos atalhos.</summary>
    private async Task<string> CriarApoioAsync(string projeto = "Transferências Alfa")
    {
        var id = await CriarProjetoComSiglaAsync(projeto, "SG-" + Guid.NewGuid().ToString("N")[..6]);
        await Criar("rtc", new JsonObject { ["rtc_externo_id"] = "RTC-100" });
        await Criar("ambiente", new JsonObject { ["nome"] = "Homologação" });
        await Criar("status_resultado", new JsonObject { ["descricao"] = "Aprovado" });
        return id;
    }

    private static object?[] LinhaRelatorio(string bateria, string projeto = "transferencias alfa", object? data = null) =>
        [projeto, "RTC-100", "HOMOLOGACAO", "Aprovado", data ?? "21/10/2026 14:30", bateria, "Carga", "Ana"];

    // ---- Modelo ----

    [Fact]
    public async Task Modelo_RelatorioDeTeste_ColunasNaOrdemDoFormularioComObrigatoriosListasEInstrucoes()
    {
        await CriarApoioAsync();

        var resposta = await Cliente.GetAsync($"{Relatorios}/importacao/modelo");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("modelo_relatorio_teste.xlsx", resposta.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
        using var pasta = new XLWorkbook(await resposta.Content.ReadAsStreamAsync());
        var dados = pasta.Worksheet(1);
        var cabecalhos = dados.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(12, cabecalhos.Count);
        Assert.All(cabecalhos.Take(8), c => Assert.EndsWith(" *", c));           // os 8 obrigatórios, na ordem do formulário
        Assert.DoesNotContain(cabecalhos, c => c.StartsWith("Equipe"));          // o administrador tem uma equipe habilitada só
        Assert.Contains(dados.DataValidations, v => v.Ranges.Any(r => r.FirstColumn().ColumnNumber() == 3)); // Ambiente: lista suspensa
        Assert.Equal(XLWorksheetVisibility.Hidden, pasta.Worksheet("Listas").Visibility);
        Assert.Equal("dd/mm/yyyy hh:mm", dados.Cell(2, 5).Style.DateFormat.Format); // data de execução
        var instrucoes = pasta.Worksheet("Instruções");
        Assert.Contains(instrucoes.CellsUsed(), c => c.GetString().Contains("pelo NOME do item"));
    }

    [Fact]
    public async Task Modelo_UsuarioComDuasEquipes_TemColunaEquipeComAListaDelas()
    {
        var outra = await CriarEquipeAsync("Equipe Beta");
        var (_, cliente) = await CriarUsuarioAsync("interno.duas", Perfil.Interno, [EquipePadrao, outra]);

        using var pasta = new XLWorkbook(await (await cliente.GetAsync($"{Relatorios}/importacao/modelo")).Content.ReadAsStreamAsync());

        Assert.Equal("Equipe", pasta.Worksheet(1).Row(1).LastCellUsed().GetString());
    }

    // ---- Fluxo completo ----

    [Fact]
    public async Task Importar_RelatoriosComReferenciasPeloNome_CriaOsValidosComHistoricoDaImportacao()
    {
        var projeto = await CriarApoioAsync();
        var arquivo = Xlsx(CabecalhosRelatorio,
            LinhaRelatorio("Bateria 1"),
            LinhaRelatorio("Bateria 2", projeto: "Projeto que não existe"),
            LinhaRelatorio("Bateria 3", data: "31/02/2026 10:00"),
            LinhaRelatorio("Bateria 4", data: new DateTime(2026, 10, 22, 9, 15, 0)));

        var (id, previa) = await Previa(Cliente, Relatorios, arquivo);

        Assert.Equal(2, previa["totais"]!["novos"]!.GetValue<int>());
        Assert.Equal(2, previa["totais"]!["comErro"]!.GetValue<int>());
        Assert.True(previa["executavel"]!.GetValue<bool>());
        Assert.Equal("NOVA", Situacao(previa, 2));
        var referencia = Linha(previa, 2)["referencias"]!.AsArray().First(r => r!["campo"]!.GetValue<string>() == "projeto")!;
        Assert.Equal(projeto, referencia["id"]!.GetValue<string>());
        Assert.Equal("Transferências Alfa", referencia["nome"]!.GetValue<string>()); // resolvido sem diferenciar maiúsculas e acentos
        Assert.Equal([0], ColunasComErro(previa, 3));                               // o erro está na célula do projeto
        Assert.Contains("Projeto que não existe", Mensagens(previa, 3)[0]);
        Assert.Equal([4], ColunasComErro(previa, 4));                               // data inválida
        Assert.Equal(0, (await ListarOk("relatorio_teste"))["total"]!.GetValue<int>()); // a prévia não grava nada

        var resumo = await OkAsync(await ExecutarAsync(Cliente, Relatorios, id));

        Assert.Equal("CONCLUIDA", resumo["situacao"]!.GetValue<string>());
        var lista = await ListarOk("relatorio_teste", "ordenacao=bateria_testes");
        Assert.Equal(["Bateria 1", "Bateria 4"], lista["itens"]!.AsArray().Select(i => i!["dados"]!["bateria_testes"]!.GetValue<string>()));
        Assert.Equal("2026-10-22T12:15:00Z", lista["itens"]![1]!["dados"]!["data_execucao"]!.GetValue<string>()); // data do Excel, fuso de negócio

        var item = lista["itens"]![0]!["id"]!.GetValue<string>();
        var historico = await LerJson(await Cliente.GetAsync($"{UrlRegistros("relatorio_teste")}/{item}/historico"));
        var linha = historico["itens"]![0]!;
        Assert.Equal("CRIACAO", linha["operacao"]!.GetValue<string>());
        Assert.Equal("IMPORTACAO", linha["origem"]!.GetValue<string>());
        var importacao = await ObterImportacaoAsync(id);
        Assert.Equal(importacao.CorrelacaoId, linha["correlacaoId"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Listar_MostraAsImportacoesComResumoESituacao()
    {
        await CriarApoioAsync();
        var (id, _) = await Previa(Cliente, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1")), "relatorios.xlsx");
        await OkAsync(await ExecutarAsync(Cliente, Relatorios, id));

        var lista = await OkAsync(await Cliente.GetAsync($"{Relatorios}/importacoes"));

        var item = lista["itens"]![0]!;
        Assert.Equal("relatorios.xlsx", item["nomeArquivo"]!.GetValue<string>());
        Assert.Equal("CONCLUIDA", item["situacao"]!.GetValue<string>());
        Assert.Equal(1, item["totais"]!["novos"]!.GetValue<int>());
        Assert.Equal("admin.testes", item["usuario"]!["matricula"]!.GetValue<string>());
        Assert.True(item["podeDesfazer"]!.GetValue<bool>());
        Assert.True(item["arquivoDisponivel"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Listar_InternoVeSoAsProprias_GestorVeAsDaEquipe()
    {
        await CriarApoioAsync();
        var (_, interno) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipePadrao]);
        var (_, outro) = await CriarUsuarioAsync("interno.b", Perfil.Interno, [EquipePadrao]);
        var (_, gestor) = await CriarUsuarioAsync("gestor.a", Perfil.Gestor, [EquipePadrao]);
        await Previa(interno, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1")));

        Assert.Equal(1, (await OkAsync(await interno.GetAsync($"{Relatorios}/importacoes")))["total"]!.GetValue<int>());
        Assert.Equal(0, (await OkAsync(await outro.GetAsync($"{Relatorios}/importacoes")))["total"]!.GetValue<int>());
        Assert.Equal(1, (await OkAsync(await gestor.GetAsync($"{Relatorios}/importacoes")))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Relatorio_TemAsLinhasOriginaisEAsColunasResultadoEMensagens()
    {
        await CriarApoioAsync();
        var (id, _) = await Previa(Cliente, Relatorios,
            Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1"), LinhaRelatorio("B2", projeto: "Nenhum")));
        await OkAsync(await ExecutarAsync(Cliente, Relatorios, id));

        var resposta = await Cliente.GetAsync($"{Relatorios}/importacoes/{id}/relatorio");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var pasta = new XLWorkbook(await resposta.Content.ReadAsStreamAsync());
        var aba = pasta.Worksheet(1);
        Assert.Equal("Resultado", aba.Cell(1, 9).GetString());
        Assert.Equal("Mensagens", aba.Cell(1, 10).GetString());
        Assert.Equal("B1", aba.Cell(2, 6).GetString());
        Assert.Equal("Criado", aba.Cell(2, 9).GetString());
        Assert.Equal("Erro", aba.Cell(3, 9).GetString());
        Assert.StartsWith("projeto: ", aba.Cell(3, 10).GetString());
    }

    // ---- Referências (RN99) ----

    [Fact]
    public async Task Referencia_NomeRepetido_ErroPedindoOId_EComAColunaIdResolve()
    {
        await CriarApoioAsync();
        var outroComMesmoNome = await CriarProjetoComSiglaAsync("Transferências Alfa", "SG-OUTRA");
        var cabecalhos = CabecalhosRelatorio.Append("projeto (ID)").ToArray();

        var (_, ambigua) = await Previa(Cliente, Relatorios, Xlsx(cabecalhos, [.. LinhaRelatorio("B1"), null]));
        var (_, porId) = await Previa(Cliente, Relatorios, Xlsx(cabecalhos, [.. LinhaRelatorio("B1"), outroComMesmoNome]));

        Assert.Equal("ERRO", Situacao(ambigua, 2));
        Assert.Contains("(ID)", Mensagens(ambigua, 2)[0]);
        Assert.Equal("NOVA", Situacao(porId, 2));
        Assert.Contains(Linha(porId, 2)["referencias"]!.AsArray(), r => r!["id"]!.GetValue<string>() == outroComMesmoNome);
    }

    [Fact]
    public async Task Referencia_ItemDeOutraEquipe_NaoEResolvido()
    {
        var outra = await CriarEquipeAsync("Equipe Beta");
        EquipeDosAtalhos = outra;
        await CriarProjetoComSiglaAsync("Projeto Beta", "SG-BETA");
        EquipeDosAtalhos = EquipePadrao;
        await CriarApoioAsync();
        var (_, interno) = await CriarUsuarioAsync("interno.padrao", Perfil.Interno, [EquipePadrao]);

        var (_, previa) = await Previa(interno, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1", projeto: "Projeto Beta")));

        Assert.Equal("ERRO", Situacao(previa, 2));
        Assert.StartsWith("Nenhum item de 'Projeto'", Mensagens(previa, 2)[0]);
    }

    [Fact]
    public async Task Referencia_PorIdDeOutraEquipe_RecusadaSemRevelarONome()
    {
        var outra = await CriarEquipeAsync("Equipe Beta");
        EquipeDosAtalhos = outra;
        var projetoBeta = await CriarProjetoComSiglaAsync("Projeto Beta", "SG-BETA");
        EquipeDosAtalhos = EquipePadrao;
        await CriarApoioAsync();
        var (_, interno) = await CriarUsuarioAsync("interno.padrao", Perfil.Interno, [EquipePadrao]);
        var cabecalhos = CabecalhosRelatorio.Append("projeto (ID)").ToArray();

        var (_, previa) = await Previa(interno, Relatorios, Xlsx(cabecalhos, [.. LinhaRelatorio("B1", projeto: ""), projetoBeta]));

        Assert.Equal("ERRO", Situacao(previa, 2));
        Assert.Equal([8], ColunasComErro(previa, 2));
        Assert.DoesNotContain(Linha(previa, 2)["referencias"]!.AsArray(), r => r!["id"]!.GetValue<string>() == projetoBeta);
    }

    [Fact]
    public async Task Cenarios_DependenciaDoEndpointPeloProjetoDoRelatorio_EscolheOCertoERecusaOErrado()
    {
        var projetoA = await CriarApoioAsync();
        var projetoB = await CriarProjetoComSiglaAsync("Projeto B", "SG-B");
        await CriarEndpoint(projetoA, "consulta");
        await CriarEndpoint(projetoB, "consulta");      // mesmo nome, outro projeto
        await CriarEndpoint(projetoB, "só do B");
        await CriarRelatorioNomeado(projetoA, "Relatório A");
        string[] cabecalhos = ["relatorio_teste", "endpoint_servico", "numero_cenario", "usuarios_virtuais", "tempo_medio_resposta_ms",
            "percentil_95_ms", "vazao_media_tps", "taxa_erro_percentual"];

        var (_, previa) = await Previa(Cliente, Cenarios, Xlsx(cabecalhos,
            ["Relatório A", "consulta", 1, 100, 250, 480, 35.5, 0.25],
            ["Relatório A", "só do B", 2, 100, 250, 480, 35.5, 0.25]));

        Assert.Equal("NOVA", Situacao(previa, 2));      // entre os dois "consulta", o do projeto do relatório
        Assert.Equal("ERRO", Situacao(previa, 3));
        Assert.Equal([1], ColunasComErro(previa, 3));
        Assert.Contains("igual ao 'Projeto' do registro escolhido em 'Relatório'", Mensagens(previa, 3)[0]);
    }

    // ---- Unicidade (RN102) ----

    [Fact]
    public async Task Duplicados_NoArquivo_ErroNasDuasLinhas()
    {
        var (relatorio, cabecalhos) = await PrepararCenariosAsync();

        var (_, previa) = await Previa(Cliente, Cenarios, Xlsx(cabecalhos, LinhaCenario(relatorio, 1), LinhaCenario(relatorio, 1)));

        Assert.Equal("ERRO", Situacao(previa, 2));
        Assert.Equal("ERRO", Situacao(previa, 3));
        Assert.Contains("linha(s) 3", Mensagens(previa, 2)[0]);
    }

    [Fact]
    public async Task Duplicados_NoBanco_PularIgnoraEAtualizarAlteraComHistorico()
    {
        var (relatorio, cabecalhos) = await PrepararCenariosAsync();
        var (idPrimeira, _) = await Previa(Cliente, Cenarios, Xlsx(cabecalhos, LinhaCenario(relatorio, 1, vazao: 10)));
        await OkAsync(await ExecutarAsync(Cliente, Cenarios, idPrimeira));
        var arquivo = Xlsx(cabecalhos, LinhaCenario(relatorio, 1, vazao: 99.5), LinhaCenario(relatorio, 2));

        var (_, pular) = await Previa(Cliente, Cenarios, arquivo);
        var (idAtualizar, atualizar) = await Previa(Cliente, Cenarios, arquivo, duplicados: "ATUALIZAR");
        await OkAsync(await ExecutarAsync(Cliente, Cenarios, idAtualizar));

        Assert.Equal("IGNORADA", Situacao(pular, 2));
        Assert.Equal("ATUALIZACAO", Situacao(atualizar, 2));
        Assert.Equal("NOVA", Situacao(atualizar, 3));
        var lista = await ListarOk("cenario_teste", "ordenacao=numero_cenario");
        Assert.Equal(2, lista["total"]!.GetValue<int>());
        Assert.Equal(99.5m, lista["itens"]![0]!["dados"]!["vazao_media_tps"]!.GetValue<decimal>());
        var historico = await LerJson(await Cliente.GetAsync($"{UrlRegistros("cenario_teste")}/{lista["itens"]![0]!["id"]}/historico"));
        Assert.Equal("ALTERACAO", historico["itens"]![0]!["operacao"]!.GetValue<string>());
        Assert.Equal("IMPORTACAO", historico["itens"]![0]!["origem"]!.GetValue<string>());
    }

    [Fact]
    public async Task Duplicados_AtualizarSoAlgumasColunas_OsCamposSemColunaFicamComoEstao()
    {
        var (relatorio, cabecalhos) = await PrepararCenariosAsync();
        var (idPrimeira, _) = await Previa(Cliente, Cenarios, Xlsx(cabecalhos, LinhaCenario(relatorio, 1, vazao: 10)));
        await OkAsync(await ExecutarAsync(Cliente, Cenarios, idPrimeira));
        var antes = (await ListarOk("cenario_teste"))["itens"]![0]!["dados"]!;

        // Só as colunas da chave (Relatório + Nº do cenário) e a vazão: o endpoint (uma referência) e os outros números ficam.
        var (id, previa) = await Previa(Cliente, Cenarios,
            Xlsx(["relatorio_teste", "numero_cenario", "vazao_media_tps"], ["Relatório A", 1, 77.25]), duplicados: "ATUALIZAR");
        await OkAsync(await ExecutarAsync(Cliente, Cenarios, id));

        Assert.Equal("ATUALIZACAO", Situacao(previa, 2));
        var depois = (await ListarOk("cenario_teste"))["itens"]![0]!["dados"]!;
        Assert.Equal(77.25m, depois["vazao_media_tps"]!.GetValue<decimal>());
        Assert.Equal(antes["endpoint_servico"]!["id"]!.GetValue<string>(), depois["endpoint_servico"]!["id"]!.GetValue<string>());
        Assert.Equal(antes["usuarios_virtuais"]!.GetValue<int>(), depois["usuarios_virtuais"]!.GetValue<int>());
    }

    [Fact]
    public async Task Duplicados_AtualizarSemPermissaoDeAlterar_ErroNaLinha()
    {
        var (relatorio, cabecalhos) = await PrepararCenariosAsync();
        await Criar("cenario_teste", DadosDoCenario(relatorio, (await ListarOk("endpoint_servico"))["itens"]![0]!["id"]!.GetValue<string>(), 1));
        await LigarAprovacaoAsync("cenario_teste");
        var (_, externo) = await CriarUsuarioAsync("externo.a", Perfil.Externo, [EquipePadrao]);

        var (_, previa) = await Previa(externo, Cenarios, Xlsx(cabecalhos, LinhaCenario(relatorio, 1)), duplicados: "ATUALIZAR");

        Assert.Equal("ERRO", Situacao(previa, 2));
    }

    // ---- Opções e execução ----

    [Fact]
    public async Task TudoOuNada_ComErro_NaoExecuta_SoValidas_ExecutaAsValidas()
    {
        await CriarApoioAsync();
        var arquivo = Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1"), LinhaRelatorio("B2", projeto: "Nenhum"));

        var (idTudo, tudo) = await Previa(Cliente, Relatorios, arquivo, linhasComErro: "TUDO_OU_NADA");
        var recusa = await ExecutarAsync(Cliente, Relatorios, idTudo);
        var (idValidas, _) = await Previa(Cliente, Relatorios, arquivo);
        await OkAsync(await ExecutarAsync(Cliente, Relatorios, idValidas));

        Assert.False(tudo["executavel"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Conflict, recusa.StatusCode);
        Assert.Equal("IMPORTACAO_SITUACAO_INVALIDA", (await LerJson(recusa))["codigo"]!.GetValue<string>());
        Assert.Equal(1, (await ListarOk("relatorio_teste"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Executar_DadosMudaramDepoisDaPrevia_409SemGravarNada()
    {
        var projeto = await CriarApoioAsync();
        var (id, _) = await Previa(Cliente, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1")));
        Assert.Equal(HttpStatusCode.NoContent, (await Excluir("projeto", projeto)).StatusCode);

        var resposta = await ExecutarAsync(Cliente, Relatorios, id);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Equal("IMPORTACAO_DADOS_MUDARAM", (await LerJson(resposta))["codigo"]!.GetValue<string>());
        Assert.Equal(0, (await ListarOk("relatorio_teste"))["total"]!.GetValue<int>());
        var previa = await OkAsync(await Cliente.GetAsync($"{Relatorios}/importacoes/{id}"));
        Assert.Equal("ERRO", Situacao(previa, 2));      // a prévia guardada já é a nova
    }

    [Fact]
    public async Task Executar_DuasVezes_SegundaRecusada()
    {
        await CriarApoioAsync();
        var (id, _) = await Previa(Cliente, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1")));
        await OkAsync(await ExecutarAsync(Cliente, Relatorios, id));

        var segunda = await ExecutarAsync(Cliente, Relatorios, id);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal(1, (await ListarOk("relatorio_teste"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Csv_PontoEVirgulaEVirgulaDecimal_Importa()
    {
        var (relatorio, cabecalhos) = await PrepararCenariosAsync();
        var csv = Csv(';', cabecalhos, ["Relatório A", "consulta", "7", "1.200", "250", "480", "1.234,56", "0,25"]);

        var (id, previa) = await Previa(Cliente, Cenarios, csv, "cenarios.csv");
        await OkAsync(await ExecutarAsync(Cliente, Cenarios, id));

        Assert.Equal("NOVA", Situacao(previa, 2));
        var cenario = (await ListarOk("cenario_teste"))["itens"]![0]!["dados"]!;
        Assert.Equal(1234.56m, cenario["vazao_media_tps"]!.GetValue<decimal>());
        Assert.Equal(0.25m, cenario["taxa_erro_percentual"]!.GetValue<decimal>());
        _ = relatorio;
    }

    // ---- Permissões (RN97) ----

    [Fact]
    public async Task Externo_SemAprovacao_403_ComAprovacao_ItensNascemPendentes()
    {
        await CriarApoioAsync();
        var (_, externo) = await CriarUsuarioAsync("externo.a", Perfil.Externo, [EquipePadrao]);
        var arquivo = Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1"));

        var semAprovacao = await EnviarAsync(externo, Relatorios, arquivo, "r.xlsx");
        var modelo = await externo.GetAsync($"{Relatorios}/importacao/modelo");
        await LigarAprovacaoAsync("relatorio_teste");
        var (id, _) = await Previa(externo, Relatorios, arquivo);
        await OkAsync(await ExecutarAsync(externo, Relatorios, id));

        Assert.Equal(HttpStatusCode.Forbidden, semAprovacao.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, modelo.StatusCode);
        var item = (await ListarOk("relatorio_teste"))["itens"]![0]!;
        Assert.Equal("PENDENTE", item["situacaoAprovacao"]!.GetValue<string>());
    }

    [Fact]
    public async Task CadastroDeApoio_SoOAdministradorImporta()
    {
        var (_, gestor) = await CriarUsuarioAsync("gestor.a", Perfil.Gestor, [EquipePadrao]);
        var arquivo = Xlsx(["nome"], ["Comunidade Ágil"], ["Comunidade Beta"]);

        var doGestor = await EnviarAsync(gestor, Comunidades, arquivo, "c.xlsx");
        var (id, _) = await Previa(Cliente, Comunidades, arquivo);
        await OkAsync(await ExecutarAsync(Cliente, Comunidades, id));

        Assert.Equal(HttpStatusCode.Forbidden, doGestor.StatusCode);
        Assert.Equal(2, (await ListarOk("comunidade"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Previa_DeImportacaoDeOutraPessoa_403ParaQuemVe_404ParaQuemNaoVe()
    {
        await CriarApoioAsync();
        var outra = await CriarEquipeAsync("Equipe Beta");
        var (_, interno) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipePadrao]);
        var (_, gestor) = await CriarUsuarioAsync("gestor.a", Perfil.Gestor, [EquipePadrao]);
        var (_, gestorBeta) = await CriarUsuarioAsync("gestor.b", Perfil.Gestor, [outra]);
        var (id, _) = await Previa(interno, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1")));

        Assert.Equal(HttpStatusCode.Forbidden, (await ExecutarAsync(gestor, Relatorios, id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await gestor.GetAsync($"{Relatorios}/importacoes/{id}")).StatusCode);
        var naoVe = await gestorBeta.GetAsync($"{Relatorios}/importacoes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, naoVe.StatusCode);
        Assert.Equal("IMPORTACAO_NAO_ENCONTRADA", (await LerJson(naoVe))["codigo"]!.GetValue<string>());
    }

    // ---- Desfazer (RN103) ----

    [Fact]
    public async Task Desfazer_SemAlteracoes_RemoveOsItensCriados()
    {
        await CriarApoioAsync();
        var (id, _) = await Previa(Cliente, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1"), LinhaRelatorio("B2")));
        await OkAsync(await ExecutarAsync(Cliente, Relatorios, id));
        var item = (await ListarOk("relatorio_teste"))["itens"]![0]!["id"]!.GetValue<string>();

        var resumo = await OkAsync(await DesfazerAsync(Cliente, Relatorios, id));

        Assert.Equal("DESFEITA", resumo["situacao"]!.GetValue<string>());
        Assert.Equal(0, (await ListarOk("relatorio_teste"))["total"]!.GetValue<int>());
        var historico = await LerJson(await Cliente.GetAsync($"{UrlRegistros("relatorio_teste")}/{item}/historico"));
        Assert.Equal("EXCLUSAO", historico["itens"]![0]!["operacao"]!.GetValue<string>());
        Assert.Equal("IMPORTACAO", historico["itens"]![0]!["origem"]!.GetValue<string>());
    }

    [Fact]
    public async Task Desfazer_ItemAlteradoDepois_409ComOItem()
    {
        await CriarApoioAsync();
        var (id, _) = await Previa(Cliente, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1")));
        await OkAsync(await ExecutarAsync(Cliente, Relatorios, id));
        var item = (await ListarOk("relatorio_teste"))["itens"]![0]!;
        var lido = await Ler("relatorio_teste", item["id"]!.GetValue<string>());
        var dados = lido["dados"]!.AsObject().ToDictionary(p => p.Key, p => p.Value is JsonObject r ? r["id"]!.DeepClone() : p.Value?.DeepClone());
        dados["tipo_teste"] = "Estresse";
        Assert.Equal(HttpStatusCode.OK, (await Atualizar("relatorio_teste", item["id"]!.GetValue<string>(), ComVersao(lido, new JsonObject(dados)))).StatusCode);

        var resposta = await DesfazerAsync(Cliente, Relatorios, id);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var corpo = await LerJson(resposta);
        Assert.Equal("IMPORTACAO_NAO_PODE_SER_DESFEITA", corpo["codigo"]!.GetValue<string>());
        var impedimento = corpo["detalhes"]!["impedimentos"]![0]!;
        Assert.Contains("alterados", impedimento["motivo"]!.GetValue<string>());
        Assert.Equal("B1", impedimento["itens"]![0]!["rotulo"]!.GetValue<string>());
        Assert.Equal(1, (await ListarOk("relatorio_teste"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Desfazer_ItemReferenciadoPorOutro_409_DepoisDeDesfazerOsFilhos_Permite()
    {
        var projeto = await CriarApoioAsync();
        var endpoint = await CriarEndpoint(projeto, "consulta");
        var (idRelatorios, _) = await Previa(Cliente, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("Relatório A")));
        await OkAsync(await ExecutarAsync(Cliente, Relatorios, idRelatorios));
        var relatorio = (await ListarOk("relatorio_teste"))["itens"]![0]!["id"]!.GetValue<string>();
        var (idCenarios, _) = await Previa(Cliente, Cenarios, Xlsx(CabecalhosCenario, LinhaCenario(relatorio, 1)));
        await OkAsync(await ExecutarAsync(Cliente, Cenarios, idCenarios));
        _ = endpoint;

        var bloqueado = await DesfazerAsync(Cliente, Relatorios, idRelatorios);
        await OkAsync(await DesfazerAsync(Cliente, Cenarios, idCenarios));
        var depois = await DesfazerAsync(Cliente, Relatorios, idRelatorios);

        Assert.Equal(HttpStatusCode.Conflict, bloqueado.StatusCode);
        Assert.Contains("apontam", (await LerJson(bloqueado))["detalhes"]!["impedimentos"]![0]!["motivo"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, depois.StatusCode);
        Assert.Equal(0, (await ListarOk("relatorio_teste"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Desfazer_Interno_403()
    {
        await CriarApoioAsync();
        var (_, interno) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipePadrao]);
        var (id, _) = await Previa(interno, Relatorios, Xlsx(CabecalhosRelatorio, LinhaRelatorio("B1")));
        await OkAsync(await ExecutarAsync(interno, Relatorios, id));

        Assert.Equal(HttpStatusCode.Forbidden, (await DesfazerAsync(interno, Relatorios, id)).StatusCode);
    }

    // ---- Arquivo: limites e tipo real (RN98) ----

    [Fact]
    public async Task Arquivo_AcimaDe5MB_400ComMensagemClara()
    {
        var grande = new byte[(5 * 1024 * 1024) + 10];
        grande[0] = (byte)'a';

        var resposta = await EnviarAsync(Cliente, Relatorios, grande, "grande.csv");

        await ConferirErroDoArquivoAsync(resposta, "máximo é 5 MB");
    }

    [Fact]
    public async Task Arquivo_AcimaDe5000Linhas_400()
    {
        var linhas = Enumerable.Range(1, 5001).Select(i => new[] { $"Comunidade {i}" }).ToArray();

        var resposta = await EnviarAsync(Cliente, Comunidades, Csv(';', ["nome"], linhas), "muitas.csv");

        await ConferirErroDoArquivoAsync(resposta, "5.000");
    }

    [Theory]
    [InlineData("texto qualquer", "planilha.xlsx", "não é uma planilha do Excel")]
    [InlineData("nome\nA", "planilha.txt", ".xlsx")]
    public async Task Arquivo_TipoRealDiferenteDaExtensao_400(string conteudo, string nome, string trecho)
    {
        var resposta = await EnviarAsync(Cliente, Comunidades, System.Text.Encoding.UTF8.GetBytes(conteudo), nome);

        await ConferirErroDoArquivoAsync(resposta, trecho);
    }

    [Fact]
    public async Task Arquivo_XlsComoXlsx_400()
    {
        byte[] ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];

        var resposta = await EnviarAsync(Cliente, Comunidades, ole, "antigo.xlsx");

        await ConferirErroDoArquivoAsync(resposta, ".xls");
    }

    [Fact]
    public async Task Arquivo_XlsxQueDescompactaDemais_400()
    {
        using var memoria = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(memoria, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("[Content_Types].xml");
            zip.CreateEntry("xl/workbook.xml");
            var bomba = zip.CreateEntry("xl/worksheets/sheet1.xml", System.IO.Compression.CompressionLevel.SmallestSize);
            using var escrita = bomba.Open();
            var zeros = new byte[1024 * 1024];
            for (var i = 0; i < 110; i++)
                escrita.Write(zeros);
        }

        var resposta = await EnviarAsync(Cliente, Comunidades, memoria.ToArray(), "bomba.xlsx");

        await ConferirErroDoArquivoAsync(resposta, "descompactada");
    }

    // ---- Limpeza (RN104) ----

    [Fact]
    public async Task Limpeza_ArquivoComMaisDe7Dias_ApagadoEImportacaoNuncaExecutadaExpira()
    {
        var (id, _) = await Previa(Cliente, Comunidades, Xlsx(["nome"], ["Comunidade Velha"]));
        using (var escopo = Banco.Fabrica.Services.CreateScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            await contexto.Importacoes.Where(i => i.Id == Guid.Parse(id))
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.CriadoEm, DateTime.UtcNow.AddDays(-8)));
            var (arquivos, expiradas) = await escopo.ServiceProvider.GetRequiredService<LimparImportacoes>().ExecutarAsync(default);
            Assert.Equal(1, arquivos);
            Assert.Equal(1, expiradas);
        }

        var relatorio = await Cliente.GetAsync($"{Comunidades}/importacoes/{id}/relatorio");
        var lista = await OkAsync(await Cliente.GetAsync($"{Comunidades}/importacoes"));

        Assert.Equal(HttpStatusCode.Conflict, relatorio.StatusCode);
        Assert.Equal("IMPORTACAO_ARQUIVO_EXPIRADO", (await LerJson(relatorio))["codigo"]!.GetValue<string>());
        Assert.Equal("EXPIRADA", lista["itens"]![0]!["situacao"]!.GetValue<string>());
        Assert.False(lista["itens"]![0]!["arquivoDisponivel"]!.GetValue<bool>());
    }

    // ---- Apoio ----

    /// <summary>
    /// Envia e faz a prévia com a sugestão de mapeamento. Nos tipos de item por equipe, a equipe padrão é a <see cref="TesteDeApi.EquipePadrao"/>:
    /// o administrador sempre informa a equipe (RN27), e a tela já vem com ela escolhida quando há uma só.
    /// </summary>
    private Task<(string Id, JsonNode Previa)> Previa(
        HttpClient cliente, string url, byte[] arquivo, string nome = "itens.xlsx", string duplicados = "PULAR", string linhasComErro = "SO_VALIDAS") =>
        EnviarEValidarAsync(cliente, url, arquivo, nome, duplicados, linhasComErro, url == Comunidades ? null : EquipePadrao.Id);

    private static readonly string[] CabecalhosCenario =
        ["relatorio_teste", "endpoint_servico", "numero_cenario", "usuarios_virtuais", "tempo_medio_resposta_ms", "percentil_95_ms",
         "vazao_media_tps", "taxa_erro_percentual"];

    private static object?[] LinhaCenario(string relatorio, int numero, double vazao = 35.5) =>
        ["Relatório A", "consulta", numero, 100, 250, 480, vazao, 0.25];

    /// <summary>Um relatório "Relatório A" com um endpoint "consulta" no mesmo projeto. Devolve o id do relatório.</summary>
    private async Task<(string Relatorio, string[] Cabecalhos)> PrepararCenariosAsync()
    {
        var projeto = await CriarApoioAsync();
        await CriarEndpoint(projeto, "consulta");
        return (await CriarRelatorioNomeado(projeto, "Relatório A"), CabecalhosCenario);
    }

    private async Task<string> CriarRelatorioNomeado(string projeto, string bateria)
    {
        var rtc = (await ListarOk("rtc"))["itens"]![0]!["id"]!.GetValue<string>();
        var ambiente = (await ListarOk("ambiente"))["itens"]![0]!["id"]!.GetValue<string>();
        var status = (await ListarOk("status_resultado"))["itens"]![0]!["id"]!.GetValue<string>();
        return await Criar("relatorio_teste", new JsonObject
        {
            ["projeto"] = projeto, ["rtc"] = rtc, ["ambiente"] = ambiente, ["status_resultado"] = status,
            ["data_execucao"] = "2026-10-21T14:30:00-03:00", ["bateria_testes"] = bateria, ["tipo_teste"] = "Carga", ["executado_por"] = "Ana",
        });
    }

    private async Task<string> CriarProjetoComSiglaAsync(string nome, string sigla)
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade " + sigla });
        var idSigla = await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = sigla });
        return await Criar("projeto", new JsonObject { ["sigla"] = idSigla, ["nome"] = nome });
    }

    private async Task LigarAprovacaoAsync(string entidade)
    {
        var url = $"/api/v1/equipes/{EquipePadrao.Id}/configuracoes/grupos/{BancoDeTestesFixture.GrupoDoSeed}/entidades/{entidade}/aprovacao";
        await OkAsync(await Cliente.PutAsJsonAsync(url, new JsonObject { ["ativa"] = true }));
    }

    private async Task<Domain.Importacoes.Importacao> ObterImportacaoAsync(string id)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>().Importacoes.AsNoTracking().SingleAsync(i => i.Id == Guid.Parse(id));
    }

    private static async Task ConferirErroDoArquivoAsync(HttpResponseMessage resposta, string trecho)
    {
        var corpo = await LerJson(resposta);
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(trecho, corpo["errors"]!["arquivo"]![0]!.GetValue<string>());
    }
}
