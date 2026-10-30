using Catalogo.Application.Abstracoes;
using Catalogo.Infrastructure.Acesso;
using Catalogo.Infrastructure.Exportacao;
using Catalogo.Infrastructure.Importacoes;
using Catalogo.Infrastructure.Metadados;
using Catalogo.Infrastructure.Painel;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Repositorios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Infrastructure;

/// <summary>Registro da Infrastructure na injeção de dependência. Chamado só pelo Program.cs da Api.</summary>
public static class ConfiguracaoInfraestrutura
{
    // Chaves do banco na raiz do appsettings.json, substituídas pelo deploy (__DB_SERVER__...). Local: appsettings.Development.json.
    public const string ChaveServidor = "DB_SERVER";
    public const string ChaveBanco = "DB_DATABASE";
    public const string ChaveUsuario = "DB_USER";
    public const string ChaveSenha = "DB_PASSWORD";

    /// <summary>As chaves de que a connection string é montada (conferidas na subida da API, junto com as outras do deploy).</summary>
    public static readonly IReadOnlyList<string> ChavesBanco = [ChaveBanco, ChaveServidor, ChaveUsuario, ChaveSenha];

    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services)
    {
        // A connection string é lida só quando o DbContext é criado, depois de toda a configuração
        // carregada (inclusive a dos testes de integração).
        // Nível de compatibilidade 120 (SQL Server 2014, o do CATDB001): o EF não usa OPENJSON (ex.: Contains numa lista vira IN).
        services.AddSingleton(provedor => new OpcoesBanco(ObterSchema(provedor.GetRequiredService<IConfiguration>())));
        services.AddDbContext<CatalogoDbContext>((provedor, opcoes) => opcoes
            .UseSqlServer(ObterConnectionString(provedor.GetRequiredService<IConfiguration>()), sql => sql.UseCompatibilityLevel(120))
            .ReplaceService<IModelCacheKeyFactory, ChaveModeloPorSchema>());

        services.AddMemoryCache();
        services.AddScoped<IMetadadosProvider, MetadadosProvider>();
        services.AddScoped<IRegistroRepository, RegistroRepository>();
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();

        // Acesso e histórico
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IEquipeRepository, EquipeRepository>();
        services.AddScoped<IHistoricoRepository, HistoricoRepository>();
        services.AddScoped<IConfiguracaoRepository, ConfiguracaoRepository>();
        services.AddScoped<IModeloAvaliacaoRepository, ModeloAvaliacaoRepository>();
        services.AddScoped<IAvaliacaoRepository, AvaliacaoRepository>();
        services.AddScoped<ISituacaoUsuarios, SituacaoUsuarios>();

        // Administração de metadados (RN50 a RN77): escrita, verificações contra os dados e escritas em massa.
        services.AddScoped<IMetadadosAdminRepository, MetadadosAdminRepository>();
        services.AddScoped<IVerificadorDadosMetadados, VerificadorDadosMetadados>();
        services.AddScoped<IEscritorRegistrosMetadados, EscritorRegistrosMetadados>();

        // Painel gerencial (agregações em LINQ, docs/adr/0031).
        services.AddScoped<IPainelRepository, PainelRepository>();
        services.AddSingleton<IHasherSenha, HasherSenha>();

        // Exportação da listagem: um gerador por formato (o caso de uso escolhe pelo parâmetro "formato").
        services.AddSingleton<IGeradorArquivoExportacao, GeradorCsv>();
        services.AddSingleton<IGeradorArquivoExportacao, GeradorXlsx>();

        // Importação de planilhas (RN97 a RN104): o arquivo no banco, as consultas em lote e a leitura e geração das planilhas.
        services.AddScoped<IImportacaoRepository, ImportacaoRepository>();
        services.AddScoped<IConsultasImportacao, ConsultasImportacao>();
        services.AddSingleton<IArquivosImportacao, ArquivosImportacao>();

        // /health: confere se a aplicação consegue abrir conexão com o banco.
        services.AddHealthChecks().AddDbContextCheck<CatalogoDbContext>("banco-de-dados");

        return services;
    }

    /// <summary>
    /// Monta a connection string a partir de DB_SERVER, DB_DATABASE, DB_USER e DB_PASSWORD, com TrustServerCertificate (o certificado
    /// do SQL Server 2014 da CAIXA não é de uma autoridade conhecida). Falha com mensagem clara se alguma faltar.
    /// </summary>
    public static string ObterConnectionString(IConfiguration configuracao)
    {
        var faltando = ChavesBanco.Where(chave => ValorAusente(configuracao[chave])).ToList();
        if (faltando.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuração do banco incompleta: {string.Join(", ", faltando)} vazia(s) ou com o marcador do deploy (__...__). " +
                "Preencha na raiz do appsettings.json (servidores) ou no appsettings.Development.json (local). Ver README.md.");
        }

        return new SqlConnectionStringBuilder
        {
            DataSource = configuracao[ChaveServidor],
            InitialCatalog = configuracao[ChaveBanco],
            UserID = configuracao[ChaveUsuario],
            Password = configuracao[ChaveSenha],
            TrustServerCertificate = true,
        }.ConnectionString;
    }

    /// <summary>Vazio ou ainda com o marcador que o deploy deveria ter substituído (ex.: "__DB_PASSWORD__").</summary>
    public static bool ValorAusente(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || (valor.Length >= 4 && valor.StartsWith("__", StringComparison.Ordinal) && valor.EndsWith("__", StringComparison.Ordinal));

    /// <summary>
    /// A chave de assinatura do JWT guardada no banco (cat.CATTB025_CHAVE_JWT). Se ainda não houver, grava a de
    /// <paramref name="gerarNova"/> e devolve a que ficou no banco (docs/adr/0053). Chamado uma vez, na subida da API.
    /// </summary>
    public static async Task<string> ObterOuCriarChaveJwtAsync(IServiceProvider servicos, Func<string> gerarNova, CancellationToken cancellationToken = default)
    {
        using var escopo = servicos.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        return await ArmazemChaveJwt.ObterOuCriarAsync(contexto, gerarNova, cancellationToken);
    }

    /// <summary>O schema do banco (<see cref="SchemaBanco.ChaveConfiguracao"/>), ou "cat". Só letras, números e "_".</summary>
    public static string ObterSchema(IConfiguration configuracao)
    {
        var schema = configuracao[SchemaBanco.ChaveConfiguracao];
        if (string.IsNullOrWhiteSpace(schema))
            return SchemaBanco.Nome;

        if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^[A-Za-z_][A-Za-z0-9_]{0,127}$"))
            throw new InvalidOperationException($"'{SchemaBanco.ChaveConfiguracao}' inválido: '{schema}' (só letras, números e \"_\").");

        return schema;
    }
}
