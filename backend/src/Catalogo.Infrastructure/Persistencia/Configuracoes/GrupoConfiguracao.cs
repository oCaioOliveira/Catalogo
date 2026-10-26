using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.Grupo — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class GrupoConfiguracao : IEntityTypeConfiguration<GrupoTabela>
{
    public void Configure(EntityTypeBuilder<GrupoTabela> builder)
    {
        builder.ToTable("Grupo");

        builder.HasKey(g => g.Id).HasName("PK_Grupo");
        builder.HasAlternateKey(g => g.Chave).HasName("UQ_Grupo_Chave");

        builder.Property(g => g.Id).HasColumnType("uniqueidentifier").HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(g => g.Chave).HasColumnType("varchar(100)").IsRequired();
        builder.Property(g => g.Nome).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(g => g.Descricao).HasColumnType("nvarchar(500)");
        builder.Property(g => g.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        // V006. Ativo SEM HasDefaultValue: com ele, o EF deixaria de enviar "false" no INSERT e o banco gravaria 1.
        builder.Property(g => g.Ativo).HasColumnType("bit");
        builder.Property(g => g.AtualizadoEm).HasColumnType("datetime2");
        builder.Property(g => g.Versao).IsRowVersion();
    }
}
