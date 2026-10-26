using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.RegistroValor — espelha database/scripts/V002__registro_valor.sql.</summary>
internal sealed class RegistroValorConfiguracao : IEntityTypeConfiguration<RegistroValor>
{
    public void Configure(EntityTypeBuilder<RegistroValor> builder)
    {
        builder.ToTable("RegistroValor", tabela => tabela.HasCheckConstraint(
            "CK_RegistroValor_UmValor",
            "CASE WHEN ValorTexto IS NULL THEN 0 ELSE 1 END + CASE WHEN ValorNumero IS NULL THEN 0 ELSE 1 END" +
            " + CASE WHEN ValorData IS NULL THEN 0 ELSE 1 END + CASE WHEN ValorBooleano IS NULL THEN 0 ELSE 1 END = 1"));

        builder.HasKey(v => new { v.RegistroId, v.CampoId }).HasName("PK_RegistroValor");

        builder.Property(v => v.RegistroId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.CampoId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.ValorTexto).HasColumnType("nvarchar(max)");
        builder.Property(v => v.ValorNumero).HasColumnType("decimal(38,10)").HasPrecision(38, 10);
        builder.Property(v => v.ValorData).HasColumnType("datetime2");
        builder.Property(v => v.ValorBooleano).HasColumnType("bit");

        // O valor some junto com o registro.
        builder.HasOne<Registro>().WithMany(r => r.Valores)
            .HasForeignKey(v => new { v.RegistroId, v.EntidadeId })
            .HasPrincipalKey(r => new { r.Id, r.EntidadeId })
            .HasConstraintName("FK_RegistroValor_Registro")
            .OnDelete(DeleteBehavior.Cascade);

        // O campo precisa ser da entidade do registro.
        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(v => new { v.CampoId, v.EntidadeId })
            .HasPrincipalKey(c => new { c.Id, c.EntidadeId })
            .HasConstraintName("FK_RegistroValor_Campo")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
