using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.GrupoEquipe — espelha database/scripts/V009__grupo_equipe.sql.</summary>
internal sealed class GrupoEquipeConfiguracao : IEntityTypeConfiguration<GrupoEquipeTabela>
{
    public void Configure(EntityTypeBuilder<GrupoEquipeTabela> builder)
    {
        builder.ToTable("GrupoEquipe");

        builder.HasKey(v => new { v.GrupoId, v.EquipeId }).HasName("PK_GrupoEquipe");

        builder.Property(v => v.GrupoId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.EquipeId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.CriadoPorId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);

        builder.HasOne<GrupoTabela>().WithMany()
            .HasForeignKey(v => v.GrupoId)
            .HasConstraintName("FK_GrupoEquipe_Grupo")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Equipe>().WithMany()
            .HasForeignKey(v => v.EquipeId)
            .HasConstraintName("FK_GrupoEquipe_Equipe")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(v => v.CriadoPorId)
            .HasConstraintName("FK_GrupoEquipe_CriadoPor")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(v => v.EquipeId).HasDatabaseName("IX_GrupoEquipe_EquipeId");
    }
}
