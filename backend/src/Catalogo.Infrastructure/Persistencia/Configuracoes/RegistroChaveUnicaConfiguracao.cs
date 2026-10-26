using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.RegistroChaveUnica — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class RegistroChaveUnicaConfiguracao : IEntityTypeConfiguration<RegistroChaveUnica>
{
    public void Configure(EntityTypeBuilder<RegistroChaveUnica> builder)
    {
        builder.ToTable("RegistroChaveUnica");

        builder.HasKey(c => new { c.RestricaoUnicaId, c.RegistroId }).HasName("PK_RegistroChaveUnica");
        // É esta constraint que barra duplicados no banco (RN09). O nome é usado pelo
        // TradutorErrosSqlServer para descobrir qual restrição foi violada.
        // No banco é UNIQUE constraint; aqui fica como índice único porque o EF não deixa alterar
        // coluna de chave alternativa, e o PUT altera o ValorHash no lugar (RN15).
        builder.HasIndex(c => new { c.RestricaoUnicaId, c.ValorHash })
            .IsUnique()
            .HasDatabaseName(TradutorErrosSqlServer.ConstraintValorHash);

        builder.Property(c => c.RestricaoUnicaId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.RegistroId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.ValorHash).HasColumnType("binary(32)").IsFixedLength().HasMaxLength(32);

        builder.HasOne<RestricaoUnicaTabela>().WithMany()
            .HasForeignKey(c => new { c.RestricaoUnicaId, c.EntidadeId })
            .HasPrincipalKey(r => new { r.Id, r.EntidadeId })
            .HasConstraintName("FK_RegistroChaveUnica_Restricao")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Registro>().WithMany(r => r.ChavesUnicas)
            .HasForeignKey(c => new { c.RegistroId, c.EntidadeId })
            .HasPrincipalKey(r => new { r.Id, r.EntidadeId })
            .HasConstraintName("FK_RegistroChaveUnica_Registro")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
