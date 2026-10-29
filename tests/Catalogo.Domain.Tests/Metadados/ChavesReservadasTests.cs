using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Metadados;

// RN14
public class ChavesReservadasTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("versao")]
    [InlineData("criadoEm")]
    [InlineData("atualizadoEm")]
    [InlineData("ATUALIZADOEM")]  // não diferencia maiúsculas, como a collation de Campo.Chave
    [InlineData("pagina")]
    [InlineData("ordenacao")]
    [InlineData("dependeDe")]
    [InlineData("equipeId")]
    [InlineData("criadoPor")]
    [InlineData("atualizadoPor")]
    [InlineData("situacaoAprovacao")]
    [InlineData("ultimaDecisao")]
    [InlineData("avaliacao")]     // RN48
    [InlineData("formato")]       // parâmetro da exportação
    public void ValidarCampos_CampoComChaveReservada_FalhaComMensagemClara(string chave)
    {
        var grupo = GrupoCom(Metadado.Campo(chave, TipoDado.Texto, tamanhoMaximo: 10));

        var erro = Assert.Throws<InvalidOperationException>(() => ChavesReservadas.ValidarCampos(grupo));

        Assert.Contains($"entidade_teste.{chave}", erro.Message);
        Assert.Contains("RN14", erro.Message);
    }

    [Fact]
    public void ValidarCampos_SemChavesReservadas_NaoFalha()
    {
        ChavesReservadas.ValidarCampos(GrupoCom(Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 10)));
    }

    [Fact]
    public void ValidadorRegistro_ColunasDeSistemaNoCorpo_SaoIgnoradas()
    {
        var entidade = Metadado.Entidade([Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 10)]);
        var dados = new JsonObject
        {
            ["id"] = "x",
            ["versao"] = "AAAA",
            ["criadoEm"] = 1,
            ["atualizadoEm"] = null,
            ["nome"] = "Ana",
        };

        var resultado = new ValidadorRegistro(ValidadoresPadrao.Todos()).Validar(entidade, dados);

        Assert.True(resultado.Valido);
        Assert.Equal("{\"nome\":\"Ana\"}", resultado.Registro!.DadosSimples.ToJsonString());
    }

    private static Grupo GrupoCom(Campo campo) => new()
    {
        Id = Guid.NewGuid(),
        Chave = "grupo_teste",
        Nome = "Grupo",
        Entidades = [Metadado.Entidade([campo])],
    };
}
