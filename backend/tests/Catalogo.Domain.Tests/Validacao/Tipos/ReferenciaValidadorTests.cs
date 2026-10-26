using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — REFERENCIA (só o formato; a existência do registro é RN06, no caso de uso)
public class ReferenciaValidadorTests
{
    private readonly ReferenciaValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("projeto", TipoDado.Referencia, entidadeReferenciadaId: Guid.NewGuid());

    [Theory]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("3F2504E0-4F89-11D3-9A0C-0305E82C3301")]
    public void Validar_GuidFormatoD_DevolveEmMinusculas(string guid)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json($"\"{guid}\""));

        Assert.True(resultado.Valido);
        Assert.Equal("3f2504e0-4f89-11d3-9a0c-0305e82c3301", resultado.ValorNormalizado!.GetValue<string>());
    }

    [Theory]
    [InlineData("\"3f2504e04f8911d39a0c0305e82c3301\"")]      // sem hífens (formato N)
    [InlineData("\"{3f2504e0-4f89-11d3-9a0c-0305e82c3301}\"")] // com chaves (formato B)
    [InlineData("\"nao-e-guid\"")]
    [InlineData("123")]
    public void Validar_ForaDoFormatoD_DevolveErro(string json)
    {
        Assert.False(_validador.Validar(_campo, Metadado.Json(json)).Valido);
    }
}
