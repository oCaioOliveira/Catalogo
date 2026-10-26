using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.Entidade — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class EntidadeConfiguracao : IEntityTypeConfiguration<EntidadeTabela>
{
    public void Configure(EntityTypeBuilder<EntidadeTabela> builder)
    {
        builder.ToTable("Entidade");

        builder.HasKey(e => e.Id).HasName("PK_Entidade");
        builder.HasAlternateKey(e => new { e.GrupoId, e.Chave }).HasName("UQ_Entidade_Grupo_Chave");

        builder.Property(e => e.Id).HasColumnType("uniqueidentifier").HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.GrupoId).HasColumnType("uniqueidentifier");
        builder.Property(e => e.Chave).HasColumnType("varchar(100)").IsRequired();
        builder.Property(e => e.Nome).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(e => e.Descricao).HasColumnType("nvarchar(500)");
        builder.Property(e => e.NomePlural).HasColumnType("nvarchar(150)");
        builder.Property(e => e.Icone).HasColumnType("varchar(50)");
        builder.Property(e => e.CampoExibicaoId).HasColumnType("uniqueidentifier");
        // V003: 1 = registros por equipe, 0 = registros globais (RN27). SEM HasDefaultValue: com ele, o EF não enviaria
        // "false" no INSERT e o banco gravaria 1 (uma entidade global criada pela API viraria "por equipe").
        builder.Property(e => e.EscopoEquipe).HasColumnType("bit");
        builder.Property(e => e.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        // V006. Ativo também sem HasDefaultValue, pelo mesmo motivo.
        builder.Property(e => e.Ativo).HasColumnType("bit");
        builder.Property(e => e.Ordem).HasColumnType("int");
        builder.Property(e => e.AtualizadoEm).HasColumnType("datetime2");
        builder.Property(e => e.Versao).IsRowVersion();

        builder.HasOne<GrupoTabela>().WithMany()
            .HasForeignKey(e => e.GrupoId)
            .HasConstraintName("FK_Entidade_Grupo")
            .OnDelete(DeleteBehavior.NoAction);

        // FK composta: o campo de exibição precisa ser da própria entidade.
        builder.HasOne<CampoTabela>().WithMany()
            .HasForeignKey(e => new { e.CampoExibicaoId, e.Id })
            .HasPrincipalKey(c => new { c.Id, c.EntidadeId })
            .HasConstraintName("FK_Entidade_CampoExibicao")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
