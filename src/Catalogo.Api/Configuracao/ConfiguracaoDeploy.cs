using Microsoft.Extensions.Configuration.Json;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Configuração do deploy da CAIXA (docs/adr/0053). O deploy só substitui seis marcadores na raiz do appsettings.json
/// (ENVIRONMENT, CORS_ALLOW, DB_DATABASE, DB_SERVER, DB_USER e DB_PASSWORD), e a API sobe só com eles, sem outras variáveis de
/// ambiente: o resto da configuração fica fixo no arquivo e a chave do JWT fica no banco (cat.CATTB025_CHAVE_JWT).
/// No computador do desenvolvedor, os mesmos valores vêm do appsettings.Development.json (fora do git; modelo no .exemplo).
/// </summary>
public static class ConfiguracaoDeploy
{
    public const string ChaveAmbiente = "ENVIRONMENT";
    public const string ChaveCors = "CORS_ALLOW";

    /// <summary>Arquivo local do desenvolvedor, fora do git e fora do publish (Catalogo.Api.csproj).</summary>
    public const string ArquivoLocal = "appsettings.Development.json";

    /// <summary>
    /// Inclui o <see cref="ArquivoLocal"/>, se existir, logo depois do appsettings.json: assim ele vale sem ASPNETCORE_ENVIRONMENT, e
    /// variáveis de ambiente, argumentos e as configurações dos testes de integração continuam valendo por cima dele.
    /// </summary>
    public static void AdicionarArquivoLocal(ConfigurationManager configuracao)
    {
        var fontes = configuracao.Sources;
        var appsettings = fontes.OfType<JsonConfigurationSource>()
            .LastOrDefault(f => string.Equals(f.Path, "appsettings.json", StringComparison.OrdinalIgnoreCase));
        if (fontes.OfType<JsonConfigurationSource>().Any(f => string.Equals(f.Path, ArquivoLocal, StringComparison.OrdinalIgnoreCase)))
            return;

        var local = new JsonConfigurationSource { Path = ArquivoLocal, Optional = true, ReloadOnChange = false };
        fontes.Insert(appsettings is null ? 0 : fontes.IndexOf(appsettings) + 1, local);
    }

    /// <summary>
    /// Confere as chaves do deploy e falha, ANTES de a API subir, com a lista de TODAS as que estão vazias ou ainda com o marcador
    /// (ex.: "__DB_PASSWORD__", o deploy não substituiu). <paramref name="chavesBanco"/> são as do banco, da Infrastructure.
    /// </summary>
    public static void Validar(IConfiguration configuracao, IEnumerable<string> chavesBanco)
    {
        var faltando = new[] { ChaveAmbiente, ChaveCors }.Concat(chavesBanco)
            .Where(chave => ValorAusente(configuracao[chave]))
            .ToList();

        if (faltando.Count > 0)
        {
            throw new InvalidOperationException(
                $"A API não subiu: configuração incompleta. Falta(m) {string.Join(", ", faltando)} (vazia(s) ou com o marcador __...__ " +
                "que o deploy deveria ter substituído). Preencha na raiz do appsettings.json (servidores) ou no " +
                $"{ArquivoLocal} (local, a partir do {ArquivoLocal}.exemplo).");
        }
    }

    /// <summary>Vazio ou ainda com o marcador do deploy (começa e termina com "__").</summary>
    public static bool ValorAusente(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
        || (valor.Length >= 4 && valor.StartsWith("__", StringComparison.Ordinal) && valor.EndsWith("__", StringComparison.Ordinal));
}
