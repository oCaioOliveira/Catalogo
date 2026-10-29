using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Metadados;

public class TipoDadoTests
{
    [Theory]
    [InlineData("TEXTO", TipoDado.Texto)]
    [InlineData("TEXTO_LONGO", TipoDado.TextoLongo)]
    [InlineData("INTEIRO", TipoDado.Inteiro)]
    [InlineData("DECIMAL", TipoDado.Decimal)]
    [InlineData("BOOLEANO", TipoDado.Booleano)]
    [InlineData("DATA", TipoDado.Data)]
    [InlineData("DATA_HORA", TipoDado.DataHora)]
    [InlineData("LISTA", TipoDado.Lista)]
    [InlineData("REFERENCIA", TipoDado.Referencia)]
    [InlineData("URL", TipoDado.Url)]   // V007
    public void Conversor_TextoDoBanco_ConverteNosDoisSentidos(string texto, TipoDado tipo)
    {
        // Os textos são os mesmos do CHECK CK_Campo_TipoDado do script V001.
        Assert.Equal(tipo, TipoDadoConversor.DeTexto(texto));
        Assert.Equal(texto, TipoDadoConversor.ParaTexto(tipo));
    }

    [Fact]
    public void Conversor_TextoDesconhecido_LancaExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TipoDadoConversor.DeTexto("texto"));
    }

    [Theory]
    [InlineData("RESTRINGIR", AcaoExclusao.Restringir)]
    [InlineData("CASCATA", AcaoExclusao.Cascata)]
    public void AcaoExclusaoConversor_TextoDoBanco_ConverteNosDoisSentidos(string texto, AcaoExclusao acao)
    {
        Assert.Equal(acao, AcaoExclusaoConversor.DeTexto(texto));
        Assert.Equal(texto, AcaoExclusaoConversor.ParaTexto(acao));
    }

    [Fact]
    public void ValidadoresPadrao_TodoTipoDado_TemExatamenteUmValidador()
    {
        // Se este teste falhar, você criou um TipoDado sem validador (ver src/Catalogo.Domain/AGENTS.md).
        var tiposComValidador = ValidadoresPadrao.Todos().Select(v => v.Tipo).ToList();

        Assert.Equal(Enum.GetValues<TipoDado>().Order(), tiposComValidador.Order());
    }

    [Fact]
    public void ValidadorRegistro_SemValidadorParaUmTipo_FalhaNaCriacao()
    {
        var incompleto = ValidadoresPadrao.Todos().Where(v => v.Tipo != TipoDado.Lista);

        var erro = Assert.Throws<InvalidOperationException>(() => new ValidadorRegistro(incompleto));
        Assert.Contains("Lista", erro.Message);
    }
}
