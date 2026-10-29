using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Validacao;

public class ValidadorRegistroTests
{
    private static readonly Guid ComunidadeId = Guid.NewGuid();

    private readonly ValidadorRegistro _validador = new(ValidadoresPadrao.Todos());

    // Parecida com a entidade "sigla" do seed, com um campo opcional e um inativo a mais.
    private readonly Entidade _sigla = Metadado.Entidade(
    [
        Metadado.Campo("comunidade", TipoDado.Referencia, obrigatorio: true, entidadeReferenciadaId: ComunidadeId, ordem: 1),
        Metadado.Campo("codigo_sigla", TipoDado.Texto, obrigatorio: true, tamanhoMaximo: 20, ordem: 2),
        Metadado.Campo("observacao", TipoDado.TextoLongo, ordem: 3),
        Metadado.Campo("campo_antigo", TipoDado.Texto, tamanhoMaximo: 10, ordem: 4, ativo: false),
    ]);

    [Fact]
    public void Validar_DadosValidos_SeparaCamposSimplesDasReferencias()
    {
        // RN07
        var comunidade = Guid.NewGuid();
        var dados = new JsonObject { ["comunidade"] = comunidade.ToString(), ["codigo_sigla"] = " ABC " };

        var resultado = _validador.Validar(_sigla, dados);

        Assert.True(resultado.Valido);
        Assert.Empty(resultado.Erros);
        Assert.Equal("{\"codigo_sigla\":\"ABC\"}", resultado.Registro!.DadosSimples.ToJsonString());
        var referencia = Assert.Single(resultado.Registro.Referencias);
        Assert.Equal("comunidade", referencia.Campo.Chave);
        Assert.Equal(comunidade, referencia.RegistroDestinoId);
    }

    [Fact]
    public void Validar_CampoDesconhecido_DevolveErroNaChaveEnviada()
    {
        // RN01
        var dados = DadosValidos();
        dados["nao_existe"] = 1;

        var resultado = _validador.Validar(_sigla, dados);

        Assert.False(resultado.Valido);
        Assert.Contains("desconhecido", Assert.Single(resultado.Erros["nao_existe"]));
    }

    [Fact]
    public void Validar_CamposDesconhecidoEInativo_ListadosComoDesatualizados_OsOutrosErrosNao()
    {
        // RN01 e RN02: o front usa essa lista (detalhes.camposDesatualizados) para recarregar os metadados.
        var dados = DadosValidos();
        dados["nao_existe"] = 1;
        dados["campo_antigo"] = "valor";
        dados["codigo_sigla"] = null;   // obrigatório vazio: erro comum, não é "desatualizado"

        var resultado = _validador.Validar(_sigla, dados);

        Assert.Equal(["campo_antigo", "nao_existe"], resultado.CamposDesatualizados.Order());
        Assert.True(resultado.Erros.ContainsKey("codigo_sigla"));
    }

    [Fact]
    public void Validar_ChaveComMaiusculasDiferentes_EhCampoDesconhecido()
    {
        // RN01: a chave do JSON precisa ser idêntica à do campo.
        var dados = DadosValidos();
        dados["Observacao"] = "x";

        Assert.True(_validador.Validar(_sigla, dados).Erros.ContainsKey("Observacao"));
    }

    [Fact]
    public void Validar_CampoInativo_DevolveErro()
    {
        // RN02
        var dados = DadosValidos();
        dados["campo_antigo"] = "valor";

        var resultado = _validador.Validar(_sigla, dados);

        Assert.Contains("inativo", Assert.Single(resultado.Erros["campo_antigo"]));
    }

    [Theory]
    [InlineData(null)]       // ausente
    [InlineData("null")]     // null explícito
    [InlineData("\"\"")]     // texto vazio
    [InlineData("\"   \"")]  // só espaços
    public void Validar_ObrigatorioAusenteOuVazio_DevolveCampoObrigatorio(string? jsonDoValor)
    {
        // RN03
        var dados = DadosValidos();
        dados.Remove("codigo_sigla");
        if (jsonDoValor is not null)
            dados["codigo_sigla"] = JsonNode.Parse(jsonDoValor);

        var resultado = _validador.Validar(_sigla, dados);

        Assert.Equal(["Campo obrigatório."], resultado.Erros["codigo_sigla"]);
    }

    [Fact]
    public void Validar_OpcionalVazio_EhIgnoradoENaoEhGravado()
    {
        // RN03
        var dados = DadosValidos();
        dados["observacao"] = "  ";

        var resultado = _validador.Validar(_sigla, dados);

        Assert.True(resultado.Valido);
        Assert.False(resultado.Registro!.DadosSimples.ContainsKey("observacao"));
    }

    [Fact]
    public void Validar_VariosProblemas_DevolveTodosOsErrosDeUmaVez()
    {
        // RN05: o mesmo cenário da validação manual (sigla com 30 caracteres, campo inexistente e sem comunidade).
        var dados = new JsonObject
        {
            ["codigo_sigla"] = new string('A', 30),
            ["campo_inexistente"] = "x",
        };

        var resultado = _validador.Validar(_sigla, dados);

        Assert.False(resultado.Valido);
        Assert.Equal(["campo_inexistente", "codigo_sigla", "comunidade"], resultado.Erros.Keys.Order());
        Assert.Contains("20 caracteres", resultado.Erros["codigo_sigla"][0]);
        Assert.Equal("Campo obrigatório.", resultado.Erros["comunidade"][0]);
    }

    [Fact]
    public void Validar_TipoErrado_UsaAMensagemDoValidadorDoTipo()
    {
        // RN04
        var dados = DadosValidos();
        dados["comunidade"] = "nao-e-guid";

        var resultado = _validador.Validar(_sigla, dados);

        Assert.Contains("GUID", Assert.Single(resultado.Erros["comunidade"]));
    }

    private static JsonObject DadosValidos() => new()
    {
        ["comunidade"] = Guid.NewGuid().ToString(),
        ["codigo_sigla"] = "ABC",
    };
}
