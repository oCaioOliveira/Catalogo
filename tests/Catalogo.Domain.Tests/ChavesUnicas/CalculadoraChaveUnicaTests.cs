using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.ChavesUnicas;

// RN09 e RN10
public class CalculadoraChaveUnicaTests
{
    private readonly CalculadoraChaveUnica _calculadora = new();
    private readonly ValidadorRegistro _validador = new(ValidadoresPadrao.Todos());

    private readonly Campo _nome = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 100, ordem: 1);

    [Theory]
    [InlineData("Comunidade Ágil", "comunidade agil")]
    [InlineData("SÃO PAULO", "sao paulo")]
    [InlineData("  Ação  ", "ACAO")]
    [InlineData("Pão de Açúcar", "PAO DE ACUCAR")]
    public void Calcular_TextosQueDiferemSoEmAcentoMaiusculaOuEspaco_GeramOMesmoHash(string valor1, string valor2)
    {
        // RN09: coerente com a collation Latin1_General_CI_AI.
        Assert.Equal(HashDoNome(valor1), HashDoNome(valor2));
    }

    [Fact]
    public void Calcular_TextosDiferentes_GeramHashesDiferentes()
    {
        Assert.NotEqual(HashDoNome("Comunidade A"), HashDoNome("Comunidade B"));
    }

    [Fact]
    public void NormalizarTexto_RemoveAcentosEPoeEmMaiusculas()
    {
        Assert.Equal("CORACAO E CAFE", CalculadoraChaveUnica.NormalizarTexto("  coração é café "));
    }

    [Fact]
    public void Calcular_HashEhSha256DosValoresUnidosPeloSeparador()
    {
        // Documenta o formato exato: mudar isto invalida os hashes já gravados.
        var projeto = Guid.Parse("3F2504E0-4F89-11D3-9A0C-0305E82C3301");
        var campoProjeto = Metadado.Campo("projeto", TipoDado.Referencia, entidadeReferenciadaId: Guid.NewGuid(), ordem: 1);
        var campoNome = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 50, ordem: 2);
        var restricao = Metadado.Restricao("uq", campoProjeto, campoNome);
        var registro = new RegistroValidado(new JsonObject { ["nome"] = "Endpoint Ágil" },
            [new ReferenciaValidada(campoProjeto, projeto)]);

        var hash = _calculadora.CalcularHash(restricao, registro.ObterValor);

        var esperado = SHA256.HashData(Encoding.UTF8.GetBytes("3f2504e0-4f89-11d3-9a0c-0305e82c3301\u001fENDPOINT AGIL"));
        Assert.Equal(esperado, hash);
    }

    [Fact]
    public void Calcular_MesmoValorEmEquipesDiferentes_GeraChavesDiferentes()
    {
        // RN09: nas entidades por equipe, a unicidade vale dentro de cada equipe.
        var entidade = Metadado.Entidade([_nome], [Metadado.Restricao("uq_nome", _nome)]);
        var registro = _validador.Validar(entidade, new JsonObject { ["nome"] = "Portal" }).Registro!;

        var equipeA = _calculadora.Calcular(entidade, registro, Guid.NewGuid()).Single().ValorHash;
        var equipeB = _calculadora.Calcular(entidade, registro, Guid.NewGuid()).Single().ValorHash;
        var global = _calculadora.Calcular(entidade, registro).Single().ValorHash;

        Assert.NotEqual(equipeA, equipeB);
        Assert.NotEqual(equipeA, global);
    }

    [Fact]
    public void Calcular_ComEquipe_AEquipeEntraComoPrimeiraParte()
    {
        var equipe = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var restricao = Metadado.Restricao("uq", _nome);

        var hash = _calculadora.CalcularHash(restricao, _ => JsonValue.Create("Portal"), equipe);

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("11111111-2222-3333-4444-555555555555\u001fPORTAL")), hash);
    }

    [Fact]
    public void Calcular_ValorNuloNaRestricao_NaoGeraChave()
    {
        // RN10: nulo não conflita, igual ao UNIQUE do SQL.
        var rtc = Metadado.Campo("rtc", TipoDado.Referencia, entidadeReferenciadaId: Guid.NewGuid(), ordem: 1);
        var entidade = Metadado.Entidade([rtc, _nome], [Metadado.Restricao("uq_link_rtc", rtc)]);
        var registro = new RegistroValidado(new JsonObject { ["nome"] = "x" }, []);

        Assert.Empty(_calculadora.Calcular(entidade, registro));
    }

    [Fact]
    public void Calcular_RestricaoCompostaComUmValorNulo_NaoGeraChave()
    {
        // RN10
        var numero = Metadado.Campo("numero", TipoDado.Inteiro, ordem: 2);
        var restricao = Metadado.Restricao("uq", _nome, numero);
        var registro = new RegistroValidado(new JsonObject { ["nome"] = "x" }, []);

        Assert.Null(_calculadora.CalcularHash(restricao, registro.ObterValor));
    }

    [Fact]
    public void Calcular_OrdemDosCampos_UsaAOrdemDaRestricaoENaoADoJson()
    {
        var a = Metadado.Campo("a", TipoDado.Texto, tamanhoMaximo: 10, ordem: 1);
        var b = Metadado.Campo("b", TipoDado.Texto, tamanhoMaximo: 10, ordem: 2);
        var restricao = Metadado.Restricao("uq_ab", a, b);

        var hashJsonAB = _calculadora.CalcularHash(restricao,
            new RegistroValidado(new JsonObject { ["a"] = "1", ["b"] = "2" }, []).ObterValor);
        var hashJsonBA = _calculadora.CalcularHash(restricao,
            new RegistroValidado(new JsonObject { ["b"] = "2", ["a"] = "1" }, []).ObterValor);
        var hashValoresTrocados = _calculadora.CalcularHash(restricao,
            new RegistroValidado(new JsonObject { ["a"] = "2", ["b"] = "1" }, []).ObterValor);

        // A ordem das propriedades no JSON não importa...
        Assert.Equal(hashJsonAB, hashJsonBA);
        // ...mas trocar os valores entre os campos gera outra chave.
        Assert.NotEqual(hashJsonAB, hashValoresTrocados);
    }

    [Fact]
    public void Calcular_SeparadorEvitaColisaoAoJuntarValores()
    {
        // Sem separador, "ab"+"c" e "a"+"bc" virariam o mesmo texto "abc".
        var a = Metadado.Campo("a", TipoDado.Texto, tamanhoMaximo: 10, ordem: 1);
        var b = Metadado.Campo("b", TipoDado.Texto, tamanhoMaximo: 10, ordem: 2);
        var restricao = Metadado.Restricao("uq_ab", a, b);

        var hash1 = _calculadora.CalcularHash(restricao, new RegistroValidado(new JsonObject { ["a"] = "ab", ["b"] = "c" }, []).ObterValor);
        var hash2 = _calculadora.CalcularHash(restricao, new RegistroValidado(new JsonObject { ["a"] = "a", ["b"] = "bc" }, []).ObterValor);

        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData(TipoDado.Decimal, "12.5", "12.50")]
    [InlineData(TipoDado.Inteiro, "-42", "-42")]
    [InlineData(TipoDado.Booleano, "true", "true")]
    [InlineData(TipoDado.Data, "\"2026-10-21\"", "2026-10-21")]
    [InlineData(TipoDado.DataHora, "\"2026-10-21T14:30:00-03:00\"", "2026-10-21T17:30:00Z")]
    [InlineData(TipoDado.Lista, "\"Médio\"", "MEDIO")]
    [InlineData(TipoDado.Url, "\" HTTPS://Exemplo.COM:8443/Caminho?Q=A#Topo \"", "https://exemplo.com:8443/Caminho?Q=A#Topo")]
    public void Normalizar_CadaTipo_GeraTextoInvariante(TipoDado tipo, string jsonEnviado, string esperado)
    {
        // O valor passa pelo validador antes, como no fluxo real.
        var campo = Metadado.Campo("campo", tipo, precisao: tipo == TipoDado.Decimal ? (byte)5 : null,
            escala: tipo == TipoDado.Decimal ? (byte)2 : null, opcoes: tipo == TipoDado.Lista ? ["Médio"] : null);
        var resultado = _validador.Validar(Metadado.Entidade([campo]), new JsonObject { ["campo"] = JsonNode.Parse(jsonEnviado) });

        var normalizado = CalculadoraChaveUnica.Normalizar(campo, resultado.Registro!.ObterValor(campo)!);

        Assert.Equal(esperado, normalizado);
    }

    [Fact]
    public void Normalizar_DecimalEmCulturaComVirgula_UsaPonto()
    {
        var culturaOriginal = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");
            var campo = Metadado.Campo("valor", TipoDado.Decimal, precisao: 18, escala: 2);

            Assert.Equal("1234.50", CalculadoraChaveUnica.Normalizar(campo, JsonValue.Create(1234.5m)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culturaOriginal;
        }
    }

    private byte[] HashDoNome(string valor)
    {
        var entidade = Metadado.Entidade([_nome], [Metadado.Restricao("uq_nome", _nome)]);
        var registro = _validador.Validar(entidade, new JsonObject { ["nome"] = valor }).Registro!;

        return Assert.Single(_calculadora.Calcular(entidade, registro)).ValorHash;
    }

    [Theory]
    [InlineData("HTTPS://Exemplo.com.br/relatorio", "https://exemplo.com.br/relatorio", true)]    // esquema e host: tanto faz
    [InlineData("https://exemplo.com.br/Relatorio", "https://exemplo.com.br/relatorio", false)]   // caminho: diferencia
    [InlineData("https://exemplo.com.br?a=B", "https://exemplo.com.br?a=b", false)]               // consulta: diferencia
    public void Calcular_Url_SoEsquemaEHostNaoDiferenciamMaiusculas(string a, string b, bool igual)
    {
        // RN09 (ADR 0042)
        var url = Metadado.Campo("url", TipoDado.Url, ordem: 1);
        var restricao = Metadado.Restricao("uq_url", url);

        var hashA = _calculadora.CalcularHash(restricao, new RegistroValidado(new JsonObject { ["url"] = a }, []).ObterValor);
        var hashB = _calculadora.CalcularHash(restricao, new RegistroValidado(new JsonObject { ["url"] = b }, []).ObterValor);

        Assert.Equal(igual, hashA!.SequenceEqual(hashB!));
    }
}
