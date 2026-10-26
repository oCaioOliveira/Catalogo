using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.RegistroReferencia — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class RegistroReferenciaConfiguracao : IEntityTypeConfiguration<RegistroReferencia>
{
    public void Configure(EntityTypeBuilder<RegistroReferencia> builder)
    {
        builder.ToTable("RegistroReferencia");

        // Cada campo REFERENCIA aponta para no máximo um registro.
        builder.HasKey(r => new { r.RegistroOrigemId, r.CampoId }).HasName("PK_RegistroReferencia");
        builder.HasIndex(r => new { r.RegistroDestinoId, r.CampoId }).HasDatabaseName("IX_RegistroReferencia_Destino");

        builder.Property(r => r.RegistroOrigemId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.EntidadeOrigemId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.CampoId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.RegistroDestinoId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.EntidadeDestinoId).HasColumnType("uniqueidentifier");

        // Origem: o registro que "tem" o campo. Excluir o registro exclui as suas referências.
        builder.HasOne<Registro>().WithMany(r => r.Referencias)
            .HasForeignKey(r => new { r.RegistroOrigemId, r.EntidadeOrigemId })
            .HasPrincipalKey(r => new { r.Id, r.EntidadeId })
            .HasConstraintName("FK_RegistroReferencia_Origem")
            .OnDelete(DeleteBehavior.Cascade);

        // O campo precisa ser da entidade do registro de origem.
        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(r => new { r.CampoId, r.EntidadeOrigemId })
            .HasPrincipalKey(c => new { c.Id, c.EntidadeId })
            .HasConstraintName("FK_RegistroReferencia_CampoOrigem")
            .OnDelete(DeleteBehavior.NoAction);

        // Destino: sem cascade. Excluir um registro apontado é bloqueado pelo banco (RESTRINGIR);
        // CASCATA é feita pela aplicação, excluindo os filhos antes (RN11).
        builder.HasOne<Registro>().WithMany()
            .HasForeignKey(r => new { r.RegistroDestinoId, r.EntidadeDestinoId })
            .HasPrincipalKey(r => new { r.Id, r.EntidadeId })
            .HasConstraintName("FK_RegistroReferencia_Destino")
            .OnDelete(DeleteBehavior.NoAction);

        // FK_RegistroReferencia_CampoDestino (CampoId, EntidadeDestinoId) → Campo (Id, EntidadeReferenciadaId)
        // existe no banco mas NÃO é mapeada: o EF exige chave alternativa sem colunas anuláveis.
        // O banco continua garantindo a regra; a aplicação confere antes (RN06).
    }
}
