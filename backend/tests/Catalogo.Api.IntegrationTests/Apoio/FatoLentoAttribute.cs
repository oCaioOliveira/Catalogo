namespace Catalogo.Api.IntegrationTests.Apoio;

/// <summary>
/// Teste LENTO (ex.: desempenho com milhares de registros): fica fora do "dotnet test" padrão e só roda com a variável de
/// ambiente RODAR_TESTES_LENTOS=1 (ver tests/AGENTS.md).
/// </summary>
public sealed class FatoLentoAttribute : FactAttribute
{
    public const string Variavel = "RODAR_TESTES_LENTOS";

    public FatoLentoAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variavel) != "1")
            Skip = $"Teste lento: rode com {Variavel}=1 (ver tests/AGENTS.md).";
    }
}
