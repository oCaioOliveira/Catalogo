using Catalogo.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;

namespace Catalogo.Api.IntegrationTests.Apoio;

/// <summary>
/// Sobe a API em memória apontando para o banco de TESTES (ListagemGenerica_Testes, criado pelo
/// db-init em database/docker) e prepara o Respawn para limpar os dados entre os testes.
/// É criado uma vez para a coleção "Api" inteira.
/// </summary>
public sealed class BancoDeTestesFixture : IAsyncLifetime
{
    /// <summary>Chave da connection string do banco de testes (user-secrets deste projeto ou variável de ambiente).</summary>
    public const string NomeConnectionString = "CatalogoTestes";

    /// <summary>
    /// Connection string de um login com DDL no banco de testes (o sa local), só para os testes que simulam OUTRO sistema no
    /// mesmo banco (<c>OutroSistemaNoMesmoBancoTests</c>): o login da aplicação não cria tabelas nem lê nada fora do schema do Catálogo.
    /// </summary>
    public const string NomeConnectionStringAdministrador = "CatalogoTestesAdmin";

    /// <summary>
    /// Schema das tabelas de teste (user-secrets ou variável de ambiente <c>SchemaTestes</c>). Padrão: o mesmo da aplicação (cat),
    /// num banco de testes (Docker local). No banco da CAIXA, que não permite criar outro banco, é um schema próprio (cat_testes),
    /// montado por database/ferramentas/estrutura_padrao_caixa.ps1 -Modo SchemaTestes.
    /// </summary>
    public const string ChaveSchemaTestes = "SchemaTestes";

    // Só os DADOS são apagados (registros, usuários, equipes e histórico).
    // Metadados (Grupo, Entidade, Campo...) vêm do seed dos scripts e ficam.
    private static readonly string[] TabelasDeDados =
    [
        "AvaliacaoResposta", "Avaliacao", "CriterioAvaliacao", "ModeloAvaliacao", "RegistroChaveUnica", "RegistroReferencia",
        "RegistroValor", "Registro", "HistoricoAlteracao", "ImportacaoArquivo", "Importacao", "ConfiguracaoEntidadeEquipe",
        "GrupoEquipe", "UsuarioEquipe", "Usuario", "Equipe",
    ];

    /// <summary>Chave do JWT só dos testes (aleatória a cada execução).</summary>
    private static readonly string ChaveJwtDosTestes = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

    private Respawner _respawner = null!;

    public string ConnectionString { get; } = LerConnectionString(NomeConnectionString);

    /// <summary>O schema das tabelas de teste (ver <see cref="ChaveSchemaTestes"/>).</summary>
    public string Schema { get; } = LerSchema();

    /// <summary>Nome físico qualificado de uma tabela lógica, para SQL escrito nos testes (ex.: "Registro" → cat.CATTB011_REGISTRO).</summary>
    public string Tabela(string tabelaLogica) => $"{Schema}.{NomesBanco.Tabela(tabelaLogica)}";

    /// <summary>Lida só quando um teste pede (ver <see cref="NomeConnectionStringAdministrador"/>).</summary>
    public string ConnectionStringAdministrador => LerConnectionString(NomeConnectionStringAdministrador);

    public WebApplicationFactory<Program> Fabrica { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Ambiente próprio: não carrega o user-secrets da Api (que aponta para o banco de desenvolvimento).
            builder.UseEnvironment("Testes");
            builder.UseSetting("ConnectionStrings:Catalogo", ConnectionString);
            builder.UseSetting(SchemaBanco.ChaveConfiguracao, Schema);
            builder.UseSetting("Jwt:ChaveAssinatura", ChaveJwtDosTestes);
            // Os testes fazem muitos logins seguidos do mesmo "endereço": o limite de produção atrapalharia.
            builder.UseSetting("Seguranca:LoginLimitePorMinuto", "100000");
            // A limpeza das importações (RN104) não roda sozinha nos testes: eles a chamam quando precisam.
            builder.UseSetting("Importacao:IntervaloLimpezaMinutos", "0");
        });

        await using var conexao = new SqlConnection(ConnectionString);
        await conexao.OpenAsync();
        _respawner = await Respawner.CreateAsync(conexao, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToInclude = TabelasDeDados.Select(t => new Table(Schema, NomesBanco.Tabela(t))).ToArray(),
        });
    }

    /// <summary>Grupo do seed (V001): os testes contam com ele, e ele nunca é apagado.</summary>
    public const string GrupoDoSeed = "testes_desempenho";

    /// <summary>
    /// Apaga os dados e os metadados criados pelos testes (os grupos que não são o do seed). Chamado antes de cada teste.
    /// Os testes da administração de metadados (RN50) criam os PRÓPRIOS grupos: assim o seed nunca é alterado.
    /// </summary>
    public async Task LimparDadosAsync()
    {
        await using (var conexao = new SqlConnection(ConnectionString))
        {
            await conexao.OpenAsync();
            await _respawner.ResetAsync(conexao);
        }

        await ApagarMetadadosDosTestesAsync();
    }

    private async Task ApagarMetadadosDosTestesAsync()
    {
        using var escopo = Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();

        var grupos = contexto.Grupos.Where(g => g.Chave != GrupoDoSeed).Select(g => g.Id);
        var entidades = contexto.Entidades.Where(e => grupos.Contains(e.GrupoId)).Select(e => e.Id);
        var campos = contexto.Campos.Where(c => entidades.Contains(c.EntidadeId)).Select(c => c.Id);

        // Na ordem das FKs (os dados já saíram pelo Respawn).
        await contexto.Entidades.Where(e => entidades.Contains(e.Id)).ExecuteUpdateAsync(s => s.SetProperty(e => e.CampoExibicaoId, (Guid?)null));
        await contexto.CamposDependencias.Where(d => campos.Contains(d.CampoId)).ExecuteDeleteAsync();
        await contexto.RestricoesUnicasCampos.Where(rc => entidades.Contains(rc.EntidadeId)).ExecuteDeleteAsync();
        await contexto.RestricoesUnicas.Where(r => entidades.Contains(r.EntidadeId)).ExecuteDeleteAsync();
        await contexto.Campos.Where(c => entidades.Contains(c.EntidadeId)).ExecuteDeleteAsync();
        await contexto.Entidades.Where(e => grupos.Contains(e.GrupoId)).ExecuteDeleteAsync();
        await contexto.Grupos.Where(g => g.Chave != GrupoDoSeed).ExecuteDeleteAsync();

        // O cache de metadados é do processo da API: sem isto, um grupo apagado continuaria "existindo" para os próximos testes.
        if (escopo.ServiceProvider.GetRequiredService<IMemoryCache>() is MemoryCache cache)
            cache.Compact(1.0);
    }

    public async Task DisposeAsync() => await Fabrica.DisposeAsync();

    private static IConfiguration Configuracao() => new ConfigurationBuilder()
        .AddUserSecrets<BancoDeTestesFixture>(optional: true)
        .AddEnvironmentVariables()
        .Build();

    private static string LerSchema()
    {
        var schema = Configuracao()[ChaveSchemaTestes];
        return string.IsNullOrWhiteSpace(schema) ? SchemaBanco.Nome : schema;
    }

    private static string LerConnectionString(string nome)
    {
        var connectionString = Configuracao().GetConnectionString(nome);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Configure 'ConnectionStrings:{nome}' (user-secrets do projeto de testes de " +
                $"integração ou variável de ambiente ConnectionStrings__{nome}). Ver README.md.");
        }

        // Proteção: o Respawn APAGA dados. Nunca rode contra um banco ou schema que não seja o de testes.
        var banco = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        var schema = LerSchema();
        if (!banco.EndsWith("_Testes", StringComparison.OrdinalIgnoreCase) && !schema.EndsWith("_testes", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Os testes de integração apagam dados e só rodam num banco terminado em '_Testes' ou num schema terminado em " +
                $"'_testes' (configurado: banco '{banco}', schema '{schema}').");
        }

        return connectionString;
    }
}

[CollectionDefinition(Nome)]
public sealed class ColecaoApi : ICollectionFixture<BancoDeTestesFixture>
{
    // Todos os testes da coleção compartilham a API e o banco, e rodam um de cada vez.
    public const string Nome = "Api";
}
