using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.RestricaoUnicaCampo — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class RestricaoUnicaCampoConfiguracao : IEntityTypeConfiguration<RestricaoUnicaCampoTabela>
{
    public void Configure(EntityTypeBuilder<RestricaoUnicaCampoTabela> builder)
    {
        builder.ToTable("RestricaoUnicaCampo");

        builder.HasKey(rc => new { rc.RestricaoUnicaId, rc.CampoId }).HasName("PK_RestricaoUnicaCampo");

        builder.Property(rc => rc.RestricaoUnicaId).HasColumnType("uniqueidentifier");
        builder.Property(rc => rc.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(rc => rc.CampoId).HasColumnType("uniqueidentifier");

        // FKs compostas: restrição e campo precisam ser da mesma entidade.
        builder.HasOne<RestricaoUnicaTabela>().WithMany()
            .HasForeignKey(rc => new { rc.RestricaoUnicaId, rc.EntidadeId })
            .HasPrincipalKey(r => new { r.Id, r.EntidadeId })
            .HasConstraintName("FK_RestricaoUnicaCampo_Restricao")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(rc => new { rc.CampoId, rc.EntidadeId })
            .HasPrincipalKey(c => new { c.Id, c.EntidadeId })
            .HasConstraintName("FK_RestricaoUnicaCampo_Campo")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
