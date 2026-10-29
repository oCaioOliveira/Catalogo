using Microsoft.Extensions.Configuration;

namespace Catalogo.Api.IntegrationTests.Apoio;

/// <summary>
/// Teste que precisa de um login com DDL no banco de TESTES (<see cref="BancoDeTestesFixture.NomeConnectionStringAdministrador"/>)
/// e de um login da aplicação SEM acesso fora do schema do Catálogo: só existe no Docker local (o sa e o listagem_app). Sem a
/// connection string de administrador (ex.: no banco da CAIXA, onde o login tem db_owner), o teste aparece como IGNORADO, com o motivo.
/// </summary>
public sealed class FactComAdministradorDeTestesAttribute : FactAttribute
{
    public FactComAdministradorDeTestesAttribute()
    {
        var configuracao = new ConfigurationBuilder()
            .AddUserSecrets<BancoDeTestesFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        if (string.IsNullOrWhiteSpace(configuracao.GetConnectionString(BancoDeTestesFixture.NomeConnectionStringAdministrador)))
        {
            Skip = $"Sem 'ConnectionStrings:{BancoDeTestesFixture.NomeConnectionStringAdministrador}' (só no Docker local: precisa " +
                   "de um login com DDL e de um login da aplicação sem acesso fora do schema do Catálogo).";
        }
    }
}
