using Catalogo.Domain.Acesso;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.ModeloAvaliacao — espelha database/scripts/V005__avaliacao_itens.sql.</summary>
internal sealed class ModeloAvaliacaoConfiguracao : IEntityTypeConfiguration<ModeloAvaliacao>
{
    public void Configure(EntityTypeBuilder<ModeloAvaliacao> builder)
    {
        builder.ToTable("ModeloAvaliacao");

        builder.HasKey(m => m.Id).HasName("PK_ModeloAvaliacao");
        // RN41: um modelo ATIVO por entidade e equipe (índice único filtrado).
        builder.HasIndex(m => new { m.EntidadeId, m.EquipeId })
            .IsUnique()
            .HasFilter("[Ativo] = 1")
            .HasDatabaseName("UX_ModeloAvaliacao_Ativo");

        // Gerado pelo EF ao adicionar (GUID sequencial), para o histórico usar o Id antes do SaveChanges.
        builder.Property(m => m.Id).HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();
        builder.Property(m => m.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(m => m.EquipeId).HasColumnType("uniqueidentifier");
        builder.Property(m => m.Nome).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(m => m.Descricao).HasColumnType("nvarchar(500)");
        // SEM HasDefaultValue: com padrão true no EF, o valor false deixaria de ser enviado no INSERT.
        builder.Property(m => m.Ativo).HasColumnType("bit");
        builder.Property(m => m.CriadoPorId).HasColumnType("uniqueidentifier");
        builder.Property(m => m.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);
        builder.Property(m => m.AtualizadoPorId).HasColumnType("uniqueidentifier");
        builder.Property(m => m.AtualizadoEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.DataOpcional);
        // rowversion: concorrência do PUT (RN12).
        builder.Property(m => m.Versao).HasColumnType("rowversion").IsRowVersion();

        // Propriedade calculada (filtro de Criterios): sem isto o EF a trataria como uma segunda navegação.
        builder.Ignore(m => m.CriteriosAtivos);

        // ClientCascade: critério tirado da lista (sem respostas, RN43) é excluído pelo EF. No banco a FK não
        // tem CASCADE (o modelo nunca é excluído).
        builder.HasMany(m => m.Criterios).WithOne()
            .HasForeignKey(c => c.ModeloAvaliacaoId)
            .HasConstraintName("FK_CriterioAvaliacao_Modelo")
            .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(m => m.EntidadeId)
            .HasConstraintName("FK_ModeloAvaliacao_Entidade")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Equipe>().WithMany()
            .HasForeignKey(m => m.EquipeId)
            .HasConstraintName("FK_ModeloAvaliacao_Equipe")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(m => m.CriadoPorId)
            .HasConstraintName("FK_ModeloAvaliacao_CriadoPor")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(m => m.AtualizadoPorId)
            .HasConstraintName("FK_ModeloAvaliacao_AtualizadoPor")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
