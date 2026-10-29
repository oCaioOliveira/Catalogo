using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.ConfiguracaoEntidadeEquipe — espelha database/scripts/V004__aprovacao_registros.sql.</summary>
internal sealed class ConfiguracaoEntidadeEquipeConfiguracao : IEntityTypeConfiguration<ConfiguracaoEntidadeEquipe>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoEntidadeEquipe> builder)
    {
        builder.ToTable("ConfiguracaoEntidadeEquipe");

        builder.HasKey(c => new { c.EntidadeId, c.EquipeId }).HasName("PK_ConfiguracaoEntidadeEquipe");

        builder.Property(c => c.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.EquipeId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.RequerAprovacao).HasColumnType("bit");
        builder.Property(c => c.AtualizadoPorId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.AtualizadoEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.Data);

        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(c => c.EntidadeId)
            .HasConstraintName("FK_ConfiguracaoEntidadeEquipe_Entidade")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Equipe>().WithMany()
            .HasForeignKey(c => c.EquipeId)
            .HasConstraintName("FK_ConfiguracaoEntidadeEquipe_Equipe")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(c => c.AtualizadoPorId)
            .HasConstraintName("FK_ConfiguracaoEntidadeEquipe_AtualizadoPor")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
