using System.Text.Json.Nodes;
using Catalogo.Api.Configuracao;
using Catalogo.Api.IntegrationTests.Apoio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// O contrato da API (docs/adr/0024): o documento OpenAPI gerado tem de ser IGUAL ao salvo em backend/openapi/v1.json.
/// Assim toda mudança de contrato aparece no PR, no diff desse arquivo.
///
/// Mudou o contrato de propósito? Regenere o arquivo e confira o diff antes de commitar:
///   ATUALIZAR_OPENAPI=1 dotnet test --filter "FullyQualifiedName~ContratoOpenApiTests"
/// Mudança que QUEBRA o contrato (remover ou renomear campo, mudar tipo ou status) só numa v2 (docs/adr/0022).
/// </summary>
public class ContratoOpenApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string VariavelAtualizar = "ATUALIZAR_OPENAPI";

    [Fact]
    public async Task DocumentoGerado_IgualAoContratoSalvo()
    {
        var gerado = await GerarAsync();
        var arquivo = CaminhoDoContrato();

        if (Environment.GetEnvironmentVariable(VariavelAtualizar) == "1")
        {
            await File.WriteAllTextAsync(arquivo, gerado);
            return;
        }

        var salvo = File.Exists(arquivo) ? Normalizar(await File.ReadAllTextAsync(arquivo)) : "";
        Assert.True(salvo == gerado,
            $"O contrato da API mudou: o OpenAPI gerado é diferente de {Path.GetRelativePath(Repositorio(), arquivo)}.\n" +
            "Se a mudança foi intencional, regenere o arquivo e revise o diff (ele vai no PR):\n" +
            $"  {VariavelAtualizar}=1 dotnet test --filter \"FullyQualifiedName~ContratoOpenApiTests\"\n" +
            "Mudança que quebra o contrato (remover ou renomear campo, mudar tipo ou status HTTP) só numa v2 (docs/adr/0022).\n" +
            $"Primeira diferença: {PrimeiraDiferenca(salvo, gerado)}");
    }

    [Fact]
    public async Task TodoEndpoint_DeclaraSucessoEErrosComProblemDetails()
    {
        var documento = JsonNode.Parse(await GerarAsync())!;
        var problemas = new List<string>();

        foreach (var (caminho, metodos) in documento["paths"]!.AsObject())
        {
            if (!caminho.StartsWith("/api/v1/", StringComparison.Ordinal))
                problemas.Add($"{caminho}: fora do prefixo /api/v1");

            foreach (var (metodo, operacao) in metodos!.AsObject())
            {
                var respostas = operacao!["responses"]!.AsObject();
                var nome = $"{metodo.ToUpperInvariant()} {caminho}";

                if (!respostas.Any(r => r.Key.StartsWith('2')))
                    problemas.Add($"{nome}: sem resposta de sucesso");
                var parametros = operacao["parameters"]?.AsArray().Select(p => p!["name"]!.GetValue<string>()).ToHashSet() ?? [];
                var obrigatorias = new List<string> { "401", "500" };
                // Corpo malformado ou parâmetro de página inválido: 400. Recurso da rota que não existe: 404.
                if (operacao["requestBody"] is not null || parametros.Contains("pagina"))
                    obrigatorias.Add("400");
                if (caminho.Contains('{'))
                    obrigatorias.Add("404");
                foreach (var obrigatoria in obrigatorias.Where(o => !respostas.ContainsKey(o)))
                    problemas.Add($"{nome}: não declara {obrigatoria}");

                foreach (var (status, resposta) in respostas.Where(r => int.Parse(r.Key) >= 400))
                {
                    var esquema = resposta!["content"]?["application/problem+json"]?["schema"]?["$ref"]?.GetValue<string>();
                    if (esquema is not ("#/components/schemas/ProblemDetails" or "#/components/schemas/ValidationProblemDetails"))
                        problemas.Add($"{nome} {status}: o erro não é um ProblemDetails em application/problem+json");
                }
            }
        }

        Assert.True(problemas.Count == 0, string.Join("\n", problemas));
    }

    private async Task<string> GerarAsync()
    {
        var documento = Banco.Fabrica.Services.GetRequiredService<ISwaggerProvider>().GetSwagger(ConfiguracaoSwagger.Documento);
        return Normalizar(await documento.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0));
    }

    /// <summary>Quebras de linha "\n" e uma no fim: o arquivo não muda conforme o sistema operacional.</summary>
    private static string Normalizar(string json) => json.Replace("\r\n", "\n").TrimEnd() + "\n";

    private static string CaminhoDoContrato() => Path.Combine(Repositorio(), "openapi", "v1.json");

    /// <summary>A pasta backend/ (onde está o Catalogo.sln).</summary>
    private static string Repositorio()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Catalogo.sln")))
            pasta = pasta.Parent;

        return pasta?.FullName ?? throw new InvalidOperationException("Catalogo.sln não encontrado acima de " + AppContext.BaseDirectory);
    }

    private static string PrimeiraDiferenca(string salvo, string gerado)
    {
        var linhasSalvas = salvo.Split('\n');
        var linhasGeradas = gerado.Split('\n');
        for (var i = 0; i < Math.Max(linhasSalvas.Length, linhasGeradas.Length); i++)
        {
            var antes = i < linhasSalvas.Length ? linhasSalvas[i] : "(fim do arquivo)";
            var depois = i < linhasGeradas.Length ? linhasGeradas[i] : "(fim do arquivo)";
            if (antes != depois)
                return $"linha {i + 1}\n  salvo:  {antes.Trim()}\n  gerado: {depois.Trim()}";
        }

        return "nenhuma";
    }
}
