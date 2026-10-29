using Catalogo.Domain.Acesso;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Registros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.Avaliacao — espelha database/scripts/V005__avaliacao_itens.sql.</summary>
internal sealed class AvaliacaoConfiguracao : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("Avaliacao");

        builder.HasKey(a => a.Id).HasName("PK_Avaliacao");
        // RN45: uma avaliação por usuário, item e modelo. No banco é UNIQUE constraint; aqui, índice único
        // (o EF não cria schema; o tradutor de erros reconhece as duas formas).
        builder.HasIndex(a => new { a.RegistroId, a.ModeloAvaliacaoId, a.UsuarioId })
            .IsUnique()
            .HasDatabaseName("UQ_Avaliacao_Registro_Modelo_Usuario");
        builder.HasIndex(a => a.ModeloAvaliacaoId).HasDatabaseName("IX_Avaliacao_Modelo");

        builder.Property(a => a.Id).HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();
        builder.Property(a => a.RegistroId).HasColumnType("uniqueidentifier");
        builder.Property(a => a.ModeloAvaliacaoId).HasColumnType("uniqueidentifier");
        builder.Property(a => a.UsuarioId).HasColumnType("uniqueidentifier");
        builder.Property(a => a.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);
        builder.Property(a => a.AtualizadoEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.DataOpcional);
        builder.Property(a => a.Versao).HasColumnType("rowversion").IsRowVersion();

        // Respostas saem junto com a avaliação (CASCADE no banco e no EF).
        builder.HasMany(a => a.Respostas).WithOne()
            .HasForeignKey(r => r.AvaliacaoId)
            .HasConstraintName("FK_AvaliacaoResposta_Avaliacao")
            .OnDelete(DeleteBehavior.Cascade);

        // Excluir o registro exclui as avaliações (CASCADE no banco, RN45).
        builder.HasOne<Registro>().WithMany()
            .HasForeignKey(a => a.RegistroId)
            .HasConstraintName("FK_Avaliacao_Registro")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ModeloAvaliacao>().WithMany()
            .HasForeignKey(a => a.ModeloAvaliacaoId)
            .HasConstraintName("FK_Avaliacao_Modelo")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .HasConstraintName("FK_Avaliacao_Usuario")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
