using Catalogo.Application.Abstracoes;
using Catalogo.Infrastructure.Acesso;
using Catalogo.Infrastructure.Exportacao;
using Catalogo.Infrastructure.Importacoes;
using Catalogo.Infrastructure.Metadados;
using Catalogo.Infrastructure.Painel;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Infrastructure;

/// <summary>Registro da Infrastructure na injeção de dependência. Chamado só pelo Program.cs da Api.</summary>
public static class ConfiguracaoInfraestrutura
{
    /// <summary>Nome em ConnectionStrings. Em desenvolvimento vem do user-secrets (ver README.md).</summary>
    public const string NomeConnectionString = "Catalogo";

    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services)
    {
        // A connection string é lida só quando o DbContext é criado, depois de toda a configuração
        // carregada (inclusive a dos testes de integração).
        services.AddDbContext<CatalogoDbContext>((provedor, opcoes) =>
            opcoes.UseSqlServer(ObterConnectionString(provedor.GetRequiredService<IConfiguration>())));

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

    public static string ObterConnectionString(IConfiguration configuracao)
    {
        var connectionString = configuracao.GetConnectionString(NomeConnectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{NomeConnectionString}' não configurada. " +
                "Em desenvolvimento use o user-secrets; nos servidores, a variável de ambiente " +
                $"ConnectionStrings__{NomeConnectionString}. Ver README.md.");
        }

        return connectionString;
    }
}
