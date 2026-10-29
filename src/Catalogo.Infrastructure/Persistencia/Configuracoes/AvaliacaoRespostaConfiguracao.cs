using Catalogo.Domain.Avaliacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.AvaliacaoResposta — espelha database/scripts/V005__avaliacao_itens.sql.</summary>
internal sealed class AvaliacaoRespostaConfiguracao : IEntityTypeConfiguration<AvaliacaoResposta>
{
    public void Configure(EntityTypeBuilder<AvaliacaoResposta> builder)
    {
        builder.ToTable("AvaliacaoResposta", tabela =>
            tabela.HasCheckConstraint("CK_AvaliacaoResposta_Estrelas", "Estrelas IS NULL OR Estrelas BETWEEN 1 AND 5"));

        builder.HasKey(r => new { r.AvaliacaoId, r.CriterioAvaliacaoId }).HasName("PK_AvaliacaoResposta");
        builder.HasIndex(r => r.CriterioAvaliacaoId).HasDatabaseName("IX_AvaliacaoResposta_Criterio");

        builder.Property(r => r.AvaliacaoId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.CriterioAvaliacaoId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.Estrelas).HasColumnType("tinyint");
        builder.Property(r => r.Texto).HasColumnType("nvarchar(max)");

        builder.HasOne<CriterioAvaliacao>().WithMany()
            .HasForeignKey(r => r.CriterioAvaliacaoId)
            .HasConstraintName("FK_AvaliacaoResposta_Criterio")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
