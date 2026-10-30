using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>cat.CATTB025_CHAVE_JWT — espelha database/scripts/V018__chave_jwt.sql.</summary>
internal sealed class ChaveJwtConfiguracao : IEntityTypeConfiguration<ChaveJwtTabela>
{
    public void Configure(EntityTypeBuilder<ChaveJwtTabela> builder)
    {
        builder.ToTable("ChaveJwt");

        builder.HasKey(c => c.Id).HasName("PK_ChaveJwt");

        builder.Property(c => c.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
        builder.Property(c => c.Chave).HasColumnType("varchar(200)").IsUnicode(false).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);
    }
}
