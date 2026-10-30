using System.Text.Json.Nodes;
using Catalogo.Api.Configuracao;
using Catalogo.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// Configuração do deploy da CAIXA (docs/adr/0053): seis chaves na raiz do appsettings.json, substituídas pelo deploy; a API não
/// sobe se alguma estiver vazia ou com o marcador. Estes testes NÃO usam banco: a falha acontece antes de a API abrir conexão.
/// </summary>
public class ConfiguracaoDeployTests
{
    private static readonly string[] ChavesDoDeploy = ["ENVIRONMENT", "CORS_ALLOW", "DB_DATABASE", "DB_SERVER", "DB_USER", "DB_PASSWORD"];

    private static readonly Dictionary<string, string?> Completa = new()
    {
        ["ENVIRONMENT"] = "PRD",
        ["CORS_ALLOW"] = "https://catalogo.caixa",
        ["DB_DATABASE"] = "CATDB001",
        ["DB_SERVER"] = "servidor\\instancia,1433",
        ["DB_USER"] = "usuario_app",
        ["DB_PASSWORD"] = "S3nha;com=caracteres",
    };

    [Fact]
    public void AppsettingsVersionado_TemAsSeisChavesNaRaizComOMarcadorDoDeploy_ESemSegredos()
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(PastaDaApi(), "appsettings.json")))!.AsObject();

        foreach (var chave in ChavesDoDeploy)
            Assert.Equal($"__{chave}__", json[chave]!.GetValue<string>());
        Assert.False(json.ContainsKey("ConnectionStrings"));
        Assert.False(json.ContainsKey("Cors"));
        Assert.False(json["Jwt"]!.AsObject().ContainsKey("ChaveAssinatura"));
    }

    [Fact]
    public void ExemploLocal_TemAsSeisChaves_EOArquivoLocalFicaForaDoGit()
    {
        var exemplo = JsonNode.Parse(File.ReadAllText(Path.Combine(PastaDaApi(), "appsettings.Development.json.exemplo")))!.AsObject();

        foreach (var chave in ChavesDoDeploy)
            Assert.True(exemplo.ContainsKey(chave), $"{chave} falta no appsettings.Development.json.exemplo");
        Assert.Equal("LOCAL", exemplo["ENVIRONMENT"]!.GetValue<string>());
        Assert.Contains("src/Catalogo.Api/appsettings.Development.json", File.ReadAllLines(Path.Combine(Raiz(), ".gitignore")));
    }

    [Fact]
    public void Validar_TodasPreenchidas_NaoFalha()
    {
        ConfiguracaoDeploy.Validar(Configuracao(Completa), ConfiguracaoInfraestrutura.ChavesBanco);
    }

    [Fact]
    public void Validar_VaziasOuComMarcador_FalhaListandoTodasAsQueFaltam()
    {
        var configuracao = Configuracao(new(Completa)
        {
            ["ENVIRONMENT"] = "__ENVIRONMENT__",
            ["CORS_ALLOW"] = "",
            ["DB_PASSWORD"] = "__DB_PASSWORD__",
            ["DB_USER"] = null,
        });

        var erro = Assert.Throws<InvalidOperationException>(() => ConfiguracaoDeploy.Validar(configuracao, ConfiguracaoInfraestrutura.ChavesBanco));

        Assert.Contains("ENVIRONMENT, CORS_ALLOW, DB_USER, DB_PASSWORD", erro.Message);
        Assert.DoesNotContain("DB_SERVER", erro.Message);
        Assert.DoesNotContain("DB_DATABASE", erro.Message);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("__DB_PASSWORD__", true)]
    [InlineData("____", true)]
    [InlineData("__", false)]
    [InlineData("__senha", false)]
    [InlineData("senha__", false)]
    [InlineData("S3nha", false)]
    public void ValorAusente_VazioOuMarcador(string? valor, bool ausente)
    {
        Assert.Equal(ausente, ConfiguracaoDeploy.ValorAusente(valor));
        Assert.Equal(ausente, ConfiguracaoInfraestrutura.ValorAusente(valor));
    }

    [Theory]
    [InlineData("LOCAL", true, true)]
    [InlineData("DES", true, false)]
    [InlineData("TQS", false, false)]
    [InlineData("HMP", false, false)]
    [InlineData("PRD", false, false)]
    [InlineData("prd", false, false)]
    public void Ambiente_SwaggerSoEmLocalEDes_DetalhesDoErroSoEmLocal(string valor, bool swagger, bool detalhes)
    {
        var ambiente = AmbienteCatalogo.Ler(Configuracao(new(Completa) { ["ENVIRONMENT"] = valor }));

        Assert.Equal(valor.ToUpperInvariant(), ambiente.Nome);
        Assert.Equal(swagger, ambiente.PublicaSwagger);
        Assert.Equal(detalhes, ambiente.MostraDetalhesDeErro);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("PROD")]
    public void Ambiente_ForaDosCincoValores_Falha(string valor)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => AmbienteCatalogo.Ler(Configuracao(new(Completa) { ["ENVIRONMENT"] = valor })));

        Assert.Contains("LOCAL, DES, TQS, HMP, PRD", erro.Message);
    }

    [Fact]
    public void ObterConnectionString_MontaComAsChavesDoBanco_ETrustServerCertificate()
    {
        var conexao = new SqlConnectionStringBuilder(ConfiguracaoInfraestrutura.ObterConnectionString(Configuracao(Completa)));

        Assert.Equal("servidor\\instancia,1433", conexao.DataSource);
        Assert.Equal("CATDB001", conexao.InitialCatalog);
        Assert.Equal("usuario_app", conexao.UserID);
        Assert.Equal("S3nha;com=caracteres", conexao.Password);
        Assert.True(conexao.TrustServerCertificate);
        Assert.False(conexao.IntegratedSecurity);
    }

    [Fact]
    public void ObterConnectionString_SemSenha_FalhaDizendoQualFalta()
    {
        var erro = Assert.Throws<InvalidOperationException>(() =>
            ConfiguracaoInfraestrutura.ObterConnectionString(Configuracao(new(Completa) { ["DB_PASSWORD"] = "__DB_PASSWORD__" })));

        Assert.Contains("DB_PASSWORD", erro.Message);
        Assert.DoesNotContain("DB_SERVER", erro.Message);
    }

    [Theory]
    [InlineData("https://*.empresa.com.br")]
    [InlineData("localhost:5173")]
    [InlineData("http://localhost:5173/caminho")]
    [InlineData("*, http://localhost:5173")]
    [InlineData(" , ")]
    public void LerOrigens_CuringaParcialOrigemIncompletaOuVazia_FalhaNaInicializacao(string origens)
    {
        Assert.Throws<InvalidOperationException>(() => ConfiguracaoCors.LerOrigens(Configuracao(new(Completa) { ["CORS_ALLOW"] = origens })));
    }

    [Fact]
    public void LerOrigens_ListaSeparadaPorVirgula_DevolveCadaOrigemSemEspacoNemBarra()
    {
        var origens = ConfiguracaoCors.LerOrigens(Configuracao(new(Completa) { ["CORS_ALLOW"] = " http://localhost:4200/ ,https://catalogo.caixa" }));

        Assert.False(origens.Todas);
        Assert.Equal(["http://localhost:4200", "https://catalogo.caixa"], origens.Lista);
    }

    [Fact]
    public void LerOrigens_Asterisco_LiberaTodas()
    {
        var origens = ConfiguracaoCors.LerOrigens(Configuracao(new(Completa) { ["CORS_ALLOW"] = " * " }));

        Assert.True(origens.Todas);
        Assert.Empty(origens.Lista);
    }

    [Fact]
    public void ArquivoLocal_ValeLogoDepoisDoAppsettings_EAntesDoQueVemDepois()
    {
        var pasta = Directory.CreateTempSubdirectory("catalogo-config-");
        try
        {
            File.WriteAllText(Path.Combine(pasta.FullName, "appsettings.json"), """{ "A": "base", "B": "base", "C": "base" }""");
            File.WriteAllText(Path.Combine(pasta.FullName, ConfiguracaoDeploy.ArquivoLocal), """{ "B": "local", "C": "local" }""");
            var configuracao = new ConfigurationManager();
            configuracao.SetBasePath(pasta.FullName);
            configuracao.AddJsonFile("appsettings.json", optional: false);
            // Faz o papel das variáveis de ambiente e das configurações dos testes, que vêm depois e continuam valendo.
            configuracao.AddInMemoryCollection(new Dictionary<string, string?> { ["C"] = "depois" });

            ConfiguracaoDeploy.AdicionarArquivoLocal(configuracao);

            Assert.Equal("base", configuracao["A"]);
            Assert.Equal("local", configuracao["B"]);
            Assert.Equal("depois", configuracao["C"]);
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }

    [Fact]
    public void Subida_ComChavesFaltando_NaoSobeEDizQuaisFaltam()
    {
        using var fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testes");
            // Por cima do appsettings.Development.json que o desenvolvedor tiver.
            foreach (var (chave, valor) in Completa)
                builder.UseSetting(chave, valor);
            builder.UseSetting("DB_PASSWORD", "__DB_PASSWORD__");
            builder.UseSetting("CORS_ALLOW", "");
        });

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        var mensagem = MensagemCompleta(erro);
        Assert.Contains("CORS_ALLOW, DB_PASSWORD", mensagem);
        Assert.Contains("A API não subiu", mensagem);
    }

    private static IConfiguration Configuracao(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private static string MensagemCompleta(Exception erro) =>
        erro is AggregateException agregada
            ? string.Join(" | ", agregada.Flatten().InnerExceptions.Select(MensagemCompleta))
            : erro.Message + (erro.InnerException is null ? "" : " | " + MensagemCompleta(erro.InnerException));

    private static string PastaDaApi() => Path.Combine(Raiz(), "src", "Catalogo.Api");

    private static string Raiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Catalogo.sln")))
            pasta = pasta.Parent;

        return pasta?.FullName ?? throw new InvalidOperationException("Catalogo.sln não encontrado acima de " + AppContext.BaseDirectory);
    }
}
