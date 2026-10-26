using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.RestricaoUnica — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class RestricaoUnicaConfiguracao : IEntityTypeConfiguration<RestricaoUnicaTabela>
{
    public void Configure(EntityTypeBuilder<RestricaoUnicaTabela> builder)
    {
        builder.ToTable("RestricaoUnica");

        builder.HasKey(r => r.Id).HasName("PK_RestricaoUnica");
        builder.HasAlternateKey(r => new { r.EntidadeId, r.Nome }).HasName("UQ_RestricaoUnica_Entidade_Nome");
        // Alvo das FKs compostas de RestricaoUnicaCampo e RegistroChaveUnica.
        builder.HasAlternateKey(r => new { r.Id, r.EntidadeId }).HasName("UQ_RestricaoUnica_Id_Entidade");

        builder.Property(r => r.Id).HasColumnType("uniqueidentifier").HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(r => r.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.Nome).HasColumnType("varchar(100)").IsRequired();

        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(r => r.EntidadeId)
            .HasConstraintName("FK_RestricaoUnica_Entidade")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
