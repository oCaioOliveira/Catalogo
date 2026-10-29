using System.Text.Json.Nodes;
using Catalogo.Domain.Historico;

namespace Catalogo.Domain.Tests.Historico;

// RN32 — mudanças por campo a partir do antes e do depois.
public class ComparadorAlteracoesTests
{
    [Fact]
    public void Comparar_Criacao_TodosOsCamposVemDeNuloParaOValor()
    {
        var mudancas = ComparadorAlteracoes.Comparar(null, new JsonObject { ["nome"] = "A", ["ativo"] = true });

        Assert.Equal(["nome", "ativo"], mudancas.Select(m => m.Campo));
        Assert.All(mudancas, m => Assert.Null(m.De));
        Assert.Equal("A", mudancas[0].Para!.GetValue<string>());
    }

    [Fact]
    public void Comparar_Exclusao_TodosOsCamposVaoParaNulo()
    {
        var mudancas = ComparadorAlteracoes.Comparar(new JsonObject { ["nome"] = "A" }, null);

        var mudanca = Assert.Single(mudancas);
        Assert.Equal("A", mudanca.De!.GetValue<string>());
        Assert.Null(mudanca.Para);
    }

    [Fact]
    public void Comparar_Alteracao_SoTrazOQueMudouIncluiuOuRemoveu()
    {
        var antes = new JsonObject { ["nome"] = "A", ["numero"] = 1, ["removido"] = "x" };
        var depois = new JsonObject { ["nome"] = "A", ["numero"] = 2, ["novo"] = "y" };

        var mudancas = ComparadorAlteracoes.Comparar(antes, depois);

        Assert.Equal(["numero", "removido", "novo"], mudancas.Select(m => m.Campo));
        Assert.Equal(1, mudancas[0].De!.GetValue<int>());
        Assert.Equal(2, mudancas[0].Para!.GetValue<int>());
        Assert.Null(mudancas[1].Para);
        Assert.Null(mudancas[2].De);
    }

    [Fact]
    public void Comparar_NuloEAusente_SaoAMesmaCoisa()
    {
        Assert.Empty(ComparadorAlteracoes.Comparar(new JsonObject { ["a"] = null }, new JsonObject()));
    }

    [Fact]
    public void Comparar_ListasEObjetos_ComparaPeloConteudo()
    {
        var antes = new JsonObject { ["equipes"] = new JsonArray("a", "b") };

        Assert.Empty(ComparadorAlteracoes.Comparar(antes, new JsonObject { ["equipes"] = new JsonArray("a", "b") }));
        Assert.Single(ComparadorAlteracoes.Comparar(antes, new JsonObject { ["equipes"] = new JsonArray("a") }));
    }

    [Fact]
    public void Comparar_NaoAlteraOsObjetosOriginais()
    {
        var antes = new JsonObject { ["nome"] = "A" };
        var depois = new JsonObject { ["nome"] = "B" };

        var mudanca = Assert.Single(ComparadorAlteracoes.Comparar(antes, depois));

        // Os valores devolvidos são cópias: podem ir para outro JSON sem "já tem pai".
        _ = new JsonObject { ["x"] = mudanca.De, ["y"] = mudanca.Para };
        Assert.Equal("A", antes["nome"]!.GetValue<string>());
    }
}
