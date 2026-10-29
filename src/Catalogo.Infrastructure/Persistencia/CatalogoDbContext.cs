using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Catalogo.Infrastructure.Persistencia;

/// <summary>
/// Contexto do EF Core. ATENÇÃO: o EF só MAPEIA tabelas que já existem. O schema é criado
/// pelos scripts de database/scripts/ (ver docs/adr/0003). Nunca use migrations,
/// EnsureCreated ou EnsureDeleted neste projeto.
/// Cada tabela tem a sua classe de configuração em Persistencia/Configuracoes. Todas ficam no schema <see cref="SchemaBanco.Nome"/>.
/// </summary>
public sealed class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options, OpcoesBanco opcoesBanco) : DbContext(options)
{
    /// <summary>O schema deste contexto (parte da chave do modelo em cache: ver <see cref="ChaveModeloPorSchema"/>).</summary>
    public string Schema => opcoesBanco.Schema;

    // Metadados (somente leitura pela aplicação)
    public DbSet<GrupoTabela> Grupos => Set<GrupoTabela>();
    public DbSet<GrupoEquipeTabela> GruposEquipes => Set<GrupoEquipeTabela>();
    public DbSet<EntidadeTabela> Entidades => Set<EntidadeTabela>();
    public DbSet<CampoTabela> Campos => Set<CampoTabela>();
    public DbSet<CampoDependenciaTabela> CamposDependencias => Set<CampoDependenciaTabela>();
    public DbSet<RestricaoUnicaTabela> RestricoesUnicas => Set<RestricaoUnicaTabela>();
    public DbSet<RestricaoUnicaCampoTabela> RestricoesUnicasCampos => Set<RestricaoUnicaCampoTabela>();

    // Dados
    public DbSet<Registro> Registros => Set<Registro>();
    public DbSet<RegistroReferencia> RegistrosReferencias => Set<RegistroReferencia>();
    public DbSet<RegistroChaveUnica> RegistrosChavesUnicas => Set<RegistroChaveUnica>();
    public DbSet<RegistroValor> RegistrosValores => Set<RegistroValor>();

    // Acesso e histórico
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Equipe> Equipes => Set<Equipe>();
    public DbSet<UsuarioEquipe> UsuariosEquipes => Set<UsuarioEquipe>();
    public DbSet<HistoricoAlteracao> HistoricosAlteracao => Set<HistoricoAlteracao>();

    // Importação de planilhas (V011).
    public DbSet<Importacao> Importacoes => Set<Importacao>();
    public DbSet<ImportacaoArquivo> ImportacoesArquivos => Set<ImportacaoArquivo>();

    // Aprovação
    public DbSet<ConfiguracaoEntidadeEquipe> ConfiguracoesEntidadesEquipes => Set<ConfiguracaoEntidadeEquipe>();

    // Avaliação
    public DbSet<ModeloAvaliacao> ModelosAvaliacao => Set<ModeloAvaliacao>();
    public DbSet<CriterioAvaliacao> CriteriosAvaliacao => Set<CriterioAvaliacao>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<AvaliacaoResposta> AvaliacoesRespostas => Set<AvaliacaoResposta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogoDbContext).Assembly);
        AplicarNomesFisicos(modelBuilder);
    }

    /// <summary>
    /// Troca os nomes lógicos das configurações (Grupo, Registro.Dados...) pelos físicos do padrão da CAIXA (<see cref="NomesBanco"/>).
    /// Os nomes de constraints e índices (PK_, UQ_, FK_, IX_) não mudam: o banco tem os mesmos (V013).
    /// </summary>
    private static void AplicarNomesFisicos(ModelBuilder modelBuilder)
    {
        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            var tabelaLogica = tipo.GetTableName();
            if (tabelaLogica is null)
                continue;

            foreach (var propriedade in tipo.GetProperties())
            {
                var colunaLogica = propriedade.GetColumnName();
                propriedade.SetColumnName(NomesBanco.Coluna(tabelaLogica, colunaLogica));
            }

            tipo.SetTableName(NomesBanco.Tabela(tabelaLogica));
        }
    }
}

/// <summary>O modelo do EF fica em cache por tipo de contexto E por schema (os testes de integração usam outro schema).</summary>
internal sealed class ChaveModeloPorSchema : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), (context as CatalogoDbContext)?.Schema, designTime);
}
