using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.CampoDependencia — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class CampoDependenciaConfiguracao : IEntityTypeConfiguration<CampoDependenciaTabela>
{
    public void Configure(EntityTypeBuilder<CampoDependenciaTabela> builder)
    {
        builder.ToTable("CampoDependencia");

        // Um campo tem no máximo uma dependência.
        builder.HasKey(d => d.CampoId).HasName("PK_CampoDependencia");

        builder.Property(d => d.CampoId).HasColumnType("uniqueidentifier");
        builder.Property(d => d.CampoViaId).HasColumnType("uniqueidentifier");
        builder.Property(d => d.CampoOrigemId).HasColumnType("uniqueidentifier");
        builder.Property(d => d.CampoAlvoId).HasColumnType("uniqueidentifier");

        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(d => d.CampoId)
            .HasConstraintName("FK_CampoDependencia_Campo")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(d => d.CampoViaId)
            .HasConstraintName("FK_CampoDependencia_Via")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(d => d.CampoOrigemId)
            .HasConstraintName("FK_CampoDependencia_Origem")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(d => d.CampoAlvoId)
            .HasConstraintName("FK_CampoDependencia_Alvo")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
