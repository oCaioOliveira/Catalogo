using System.Text.Json.Nodes;
using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// Exportação da listagem (GET .../registros/exportar): mesma consulta e visibilidade da listagem (RN26, RN37),
// colunas dos metadados, leitura em lotes, limite de linhas e proteção contra fórmula.
public class ExportarRegistrosTests
{
    private readonly AmbienteDeTeste _ambiente = new();

    [Fact]
    public async Task Exportar_ColunasAtivasNaOrdemDosMetadados_MaisAsColunasDeSistema()
    {
        await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "Ágil", ["descricao"] = "Times de produto" });

        var (arquivo, planilha) = await _ambiente.Exportar("comunidade");

        // "Apelido" está inativo (RN02): não é exportado. Comunidade é global: sem coluna de equipe.
        Assert.Equal(["Nome", "Descrição", "Criado por", "Criado em", "Atualizado em"], planilha.Colunas);
        var linha = Assert.Single(planilha.Linhas);
        Assert.Equal(["Ágil", "Times de produto", "Admin"], linha.Take(3));
        // Data e hora no fuso da exportação (UTC nos testes), sem Kind: o Excel não guarda fuso.
        Assert.Equal(DateTimeKind.Unspecified, Assert.IsType<DateTime>(linha[3]).Kind);
        Assert.Null(linha[4]);
        Assert.Equal("comunidade_2026-10-21.csv", arquivo.NomeArquivo);
    }

    [Fact]
    public async Task Exportar_MaisDeUmLote_TrazTodasAsLinhasNaOrdemDaConsulta()
    {
        const int quantidade = ExportarRegistros.TamanhoLote * 2 + 1;
        for (var i = 0; i < quantidade; i++)
            await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = $"Comunidade {i:D4}" });
        var consultasAntes = _ambiente.Repositorio.ConsultasDeListagem;

        var (_, planilha) = await _ambiente.Exportar("comunidade");

        Assert.Equal(quantidade, planilha.Linhas.Count);
        Assert.Equal(Enumerable.Range(0, quantidade).Select(i => (object?)$"Comunidade {i:D4}"), planilha.Coluna("Nome"));
        // Três lotes (500 + 500 + 1): o resultado nunca é lido inteiro de uma vez.
        Assert.Equal(3, _ambiente.Repositorio.ConsultasDeListagem - consultasAntes);
    }

    [Fact]
    public async Task Exportar_AcimaDoLimite_400ExportacaoMuitoGrande()
    {
        for (var i = 0; i < 3; i++)
            await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = $"C{i}" });

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Exportar("comunidade", limiteLinhas: 2));

        Assert.Equal(CodigosErro.ExportacaoMuitoGrande, erro.Codigo);
        Assert.Contains("Use mais filtros", Assert.Single(erro.Erros["exportacao"]));
    }

    [Fact]
    public async Task Exportar_UsuarioDeA_SoExportaOsRegistrosDeA_ComONomeDaEquipe()
    {
        // RN26: o mesmo escopo de equipes da listagem.
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "De A", ["equipeId"] = _ambiente.EquipeA.Id.ToString() });
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "De B", ["equipeId"] = _ambiente.EquipeB.Id.ToString() });
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, _ambiente.EquipeA.Id);

        var (_, planilha) = await _ambiente.Exportar("projeto");

        Assert.Equal(["De A"], planilha.Coluna("Nome"));
        Assert.Equal(["Equipe A"], planilha.Coluna("Equipe"));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => _ambiente.Exportar("projeto", $"formato=csv&equipeId={_ambiente.EquipeB.Id}"));
    }

    [Fact]
    public async Task Exportar_TextoQueComecaComoFormula_RecebeApostrofo()
    {
        await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "=HYPERLINK(\"http://x\")", ["descricao"] = "-2+3" });

        var (_, planilha) = await _ambiente.Exportar("comunidade");

        Assert.Equal("'=HYPERLINK(\"http://x\")", planilha.Coluna("Nome")[0]);
        Assert.Equal("'-2+3", planilha.Coluna("Descrição")[0]);
    }

    [Theory]
    [InlineData("", "Obrigatório")]
    [InlineData("formato=pdf", "não suportado")]
    public async Task Exportar_FormatoAusenteOuInvalido_400NoParametroFormato(string parametros, string trecho)
    {
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Exportar("comunidade", parametros));

        Assert.Equal(CodigosErro.Validacao, erro.Codigo);
        Assert.Contains(trecho, Assert.Single(erro.Erros["formato"]));
    }
}
