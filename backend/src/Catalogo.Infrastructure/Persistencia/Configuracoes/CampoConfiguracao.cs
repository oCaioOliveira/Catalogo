using Catalogo.Domain.Metadados;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// catalogo.Campo — espelha database/scripts/V001__listagem_generica.sql.
/// Os CHECKs (CK_Campo_*) não são mapeados: o EF não cria schema, e quem garante as regras
/// é o banco. Eles estão descritos em docs/modelo-de-dados.md.
/// </summary>
internal sealed class CampoConfiguracao : IEntityTypeConfiguration<CampoTabela>
{
    public void Configure(EntityTypeBuilder<CampoTabela> builder)
    {
        builder.ToTable("Campo");

        builder.HasKey(c => c.Id).HasName("PK_Campo");
        builder.HasAlternateKey(c => new { c.EntidadeId, c.Chave }).HasName("UQ_Campo_Entidade_Chave");
        // Alvo das FKs compostas de RestricaoUnicaCampo, RegistroReferencia e Entidade.CampoExibicao.
        builder.HasAlternateKey(c => new { c.Id, c.EntidadeId }).HasName("UQ_Campo_Id_Entidade");
        // No banco é uma UNIQUE constraint, alvo da FK_RegistroReferencia_CampoDestino. No EF fica como
        // índice único porque chave alternativa não pode ter coluna anulável (EntidadeReferenciadaId).
        // Por isso a FK_RegistroReferencia_CampoDestino também não é mapeada (ver RegistroReferenciaConfiguracao).
        builder.HasIndex(c => new { c.Id, c.EntidadeReferenciadaId })
            .IsUnique()
            .HasDatabaseName("UQ_Campo_Id_EntidadeReferenciada");

        builder.Property(c => c.Id).HasColumnType("uniqueidentifier").HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(c => c.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.Chave).HasColumnType("varchar(100)").IsRequired();
        builder.Property(c => c.Nome).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(c => c.TipoDado)
            .HasColumnType("varchar(20)")
            .HasConversion(tipo => TipoDadoConversor.ParaTexto(tipo), texto => TipoDadoConversor.DeTexto(texto))
            .IsRequired();
        // Sem HasDefaultValue nos bool: o EF deixaria de enviar o valor padrão do C# (false) no INSERT.
        builder.Property(c => c.Obrigatorio).HasColumnType("bit");
        builder.Property(c => c.TamanhoMaximo).HasColumnType("int");
        builder.Property(c => c.Precisao).HasColumnType("tinyint");
        builder.Property(c => c.Escala).HasColumnType("tinyint");
        builder.Property(c => c.Opcoes).HasColumnType("nvarchar(max)");
        builder.Property(c => c.EntidadeReferenciadaId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.AcaoExclusao)
            .HasColumnType("varchar(20)")
            .HasConversion(
                acao => acao == null ? null : AcaoExclusaoConversor.ParaTexto(acao.Value),
                texto => texto == null ? null : AcaoExclusaoConversor.DeTexto(texto));
        builder.Property(c => c.Ordem).HasColumnType("int");
        builder.Property(c => c.Ativo).HasColumnType("bit");
        builder.Property(c => c.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(c => c.EntidadeId)
            .HasConstraintName("FK_Campo_Entidade")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(c => c.EntidadeReferenciadaId)
            .HasConstraintName("FK_Campo_EntidadeReferenciada")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
