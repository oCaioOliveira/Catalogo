using Catalogo.Domain.Acesso;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.Equipe — espelha database/scripts/V003__usuarios_equipes_historico.sql.</summary>
internal sealed class EquipeConfiguracao : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        builder.ToTable("Equipe");

        builder.HasKey(e => e.Id).HasName("PK_Equipe");
        // UNIQUE do nome como ÍNDICE único, não chave alternativa: renomear a equipe altera o Nome no lugar, e o EF não deixa
        // alterar coluna de chave alternativa (docs/modelo-de-dados.md, "Mapeamento no EF Core").
        builder.HasIndex(e => e.Nome).IsUnique().HasDatabaseName("UQ_Equipe_Nome");

        // Gerado pelo EF ao adicionar (GUID sequencial), para o histórico usar o Id antes do SaveChanges.
        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();
        builder.Property(e => e.Nome).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(e => e.Ativa).HasColumnType("bit").HasDefaultValue(true);
        builder.Property(e => e.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);
    }
}
