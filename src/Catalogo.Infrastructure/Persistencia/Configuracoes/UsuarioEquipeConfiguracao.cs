using Catalogo.Domain.Acesso;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.UsuarioEquipe — espelha database/scripts/V003__usuarios_equipes_historico.sql.</summary>
internal sealed class UsuarioEquipeConfiguracao : IEntityTypeConfiguration<UsuarioEquipe>
{
    public void Configure(EntityTypeBuilder<UsuarioEquipe> builder)
    {
        builder.ToTable("UsuarioEquipe");

        builder.HasKey(v => new { v.UsuarioId, v.EquipeId }).HasName("PK_UsuarioEquipe");

        builder.Property(v => v.UsuarioId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.EquipeId).HasColumnType("uniqueidentifier");
        builder.Property(v => v.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);

        // Remover o vínculo da lista Usuario.Equipes apaga a linha.
        builder.HasOne<Usuario>().WithMany(u => u.Equipes)
            .HasForeignKey(v => v.UsuarioId)
            .HasConstraintName("FK_UsuarioEquipe_Usuario")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Equipe>().WithMany()
            .HasForeignKey(v => v.EquipeId)
            .HasConstraintName("FK_UsuarioEquipe_Equipe")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
