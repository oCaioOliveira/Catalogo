namespace Catalogo.Api.Configuracao;

/// <summary>
/// O ambiente da aplicação, da chave ENVIRONMENT do appsettings.json (LOCAL, DES, TQS, HMP ou PRD). Substitui o
/// ASPNETCORE_ENVIRONMENT: nada na API decide pelo ambiente do ASP.NET (IHostEnvironment), só por esta classe (docs/adr/0053).
/// </summary>
public sealed class AmbienteCatalogo
{
    public const string Local = "LOCAL";
    public const string Desenvolvimento = "DES";
    public const string Testes = "TQS";
    public const string Homologacao = "HMP";
    public const string Producao = "PRD";

    public static readonly IReadOnlyList<string> Validos = [Local, Desenvolvimento, Testes, Homologacao, Producao];

    private AmbienteCatalogo(string nome) => Nome = nome;

    public string Nome { get; }

    /// <summary>Swagger UI e /swagger/v1/swagger.json: só em LOCAL e DES.</summary>
    public bool PublicaSwagger => Nome is Local or Desenvolvimento;

    /// <summary>Mensagem e stack trace da exceção no corpo do 500: só em LOCAL (fora dele podem expor informação interna).</summary>
    public bool MostraDetalhesDeErro => Nome == Local;

    /// <summary>Lê e valida ENVIRONMENT. Aceita minúsculas; falha com mensagem clara fora dos cinco valores.</summary>
    public static AmbienteCatalogo Ler(IConfiguration configuracao)
    {
        var valor = configuracao[ConfiguracaoDeploy.ChaveAmbiente]?.Trim().ToUpperInvariant();
        if (valor is null || !Validos.Contains(valor))
        {
            throw new InvalidOperationException(
                $"{ConfiguracaoDeploy.ChaveAmbiente} inválido: '{configuracao[ConfiguracaoDeploy.ChaveAmbiente]}'. " +
                $"Use um destes: {string.Join(", ", Validos)}.");
        }

        return new AmbienteCatalogo(valor);
    }

    public override string ToString() => Nome;
}
