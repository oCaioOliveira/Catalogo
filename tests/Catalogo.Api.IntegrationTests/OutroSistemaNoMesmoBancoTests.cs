using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// O banco de produção é compartilhado com outros sistemas, que têm tabelas de nomes genéricos em dbo (V012). Aqui um "outro
/// sistema" cria dbo.Usuario e dbo.Registro, com dados (inclusive uma matrícula igual à de um usuário do Catálogo), e o Catálogo
/// precisa funcionar sem ler nem alterar nada delas.
/// </summary>
public sealed class OutroSistemaNoMesmoBancoTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string CriarOutroSistema = """
        IF OBJECT_ID(N'dbo.Registro', N'U') IS NOT NULL DROP TABLE dbo.Registro;
        IF OBJECT_ID(N'dbo.Usuario', N'U') IS NOT NULL DROP TABLE dbo.Usuario;
        CREATE TABLE dbo.Usuario (Id int NOT NULL PRIMARY KEY, Matricula varchar(20) NOT NULL, Nome nvarchar(150) NOT NULL,
                                  SenhaHash nvarchar(500) NOT NULL);
        CREATE TABLE dbo.Registro (Id int NOT NULL PRIMARY KEY, EntidadeId uniqueidentifier NULL, Dados nvarchar(max) NOT NULL);
        INSERT dbo.Usuario VALUES (1, 'maria', N'Maria do Outro Sistema', N'hash-de-outro-sistema'),
                                  (2, 'joao', N'João do Outro Sistema', N'hash-de-outro-sistema');
        INSERT dbo.Registro VALUES (1, NULL, N'{"nome":"Comunidade do outro sistema"}'), (2, NEWID(), N'{}');
        """;

    private const string ApagarOutroSistema = """
        IF OBJECT_ID(N'dbo.Registro', N'U') IS NOT NULL DROP TABLE dbo.Registro;
        IF OBJECT_ID(N'dbo.Usuario', N'U') IS NOT NULL DROP TABLE dbo.Usuario;
        """;

    [FactComAdministradorDeTestes]
    public async Task TabelasDeMesmoNomeEmDbo_CatalogoFuncionaSemLerNemAlterarElas()
    {
        await using var administrador = new SqlConnection(Banco.ConnectionStringAdministrador);
        await administrador.OpenAsync();
        await Executar(administrador, CriarOutroSistema);
        try
        {
            var antes = await LerOutroSistema(administrador);

            // Login, cadastro, listagem e exclusão pelo Catálogo, com a mesma matrícula que o outro sistema tem em dbo.Usuario.
            await CriarUsuarioAsync("maria", Perfil.Interno, [EquipePadrao]);
            var login = await ClienteAnonimo.PostAsJsonAsync("/api/v1/auth/login", new { matricula = "maria", senha = SenhaPadrao });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Equal("Usuário maria", (await LerJson(login))["usuario"]!["nome"]!.GetValue<string>());

            var equipe = await Cliente.PostAsJsonAsync("/api/v1/equipes", new { nome = "Equipe do Catálogo" });
            Assert.Equal(HttpStatusCode.Created, equipe.StatusCode);

            var id = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade do outro sistema" });
            var lista = await ListarOk("comunidade");
            Assert.Equal(id, lista["itens"]![0]!["id"]!.GetValue<string>());
            Assert.Equal(HttpStatusCode.NoContent, (await Excluir("comunidade", id)).StatusCode);

            Assert.Equal(antes, await LerOutroSistema(administrador));

            // E o login da aplicação nem enxerga o que está fora do schema do Catálogo.
            await using var aplicacao = new SqlConnection(Banco.ConnectionString);
            await aplicacao.OpenAsync();
            var negado = await Assert.ThrowsAsync<SqlException>(() => Executar(aplicacao, "SELECT COUNT(*) FROM dbo.Usuario;"));
            Assert.Equal(229, negado.Number); // permissão negada (SELECT)
            await Executar(aplicacao, $"SELECT COUNT(*) FROM {Banco.Tabela("Usuario")};");
        }
        finally
        {
            await Executar(administrador, ApagarOutroSistema);
        }
    }

    private static async Task Executar(SqlConnection conexao, string sql)
    {
        await using var comando = new SqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>Todas as linhas das duas tabelas do outro sistema, como texto, para comparar antes e depois.</summary>
    private static async Task<List<string>> LerOutroSistema(SqlConnection conexao)
    {
        await using var comando = new SqlCommand("""
            SELECT CONCAT('Usuario|', Id, '|', Matricula, '|', Nome, '|', SenhaHash) FROM dbo.Usuario
            UNION ALL
            SELECT CONCAT('Registro|', Id, '|', EntidadeId, '|', Dados) FROM dbo.Registro
            ORDER BY 1;
            """, conexao);
        await using var leitor = await comando.ExecuteReaderAsync();
        var linhas = new List<string>();
        while (await leitor.ReadAsync())
            linhas.Add(leitor.GetString(0));
        Assert.Equal(4, linhas.Count);
        return linhas;
    }
}
