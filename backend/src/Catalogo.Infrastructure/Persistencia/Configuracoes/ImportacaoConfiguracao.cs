using Catalogo.Domain.Acesso;
using Catalogo.Domain.Importacoes;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.Importacao — espelha database/scripts/V011__importacao.sql.</summary>
internal sealed class ImportacaoConfiguracao : IEntityTypeConfiguration<Importacao>
{
    public void Configure(EntityTypeBuilder<Importacao> builder)
    {
        builder.ToTable("Importacao", tabela =>
        {
            tabela.HasCheckConstraint("CK_Importacao_Formato", "Formato IN ('XLSX','CSV')");
            tabela.HasCheckConstraint("CK_Importacao_Situacao", "Situacao IN ('ENVIADA','VALIDADA','CONCLUIDA','DESFEITA','EXPIRADA')");
            tabela.HasCheckConstraint("CK_Importacao_Opcoes_Json", "Opcoes IS NULL OR ISJSON(Opcoes) = 1");
            tabela.HasCheckConstraint("CK_Importacao_Mapeamento_Json", "Mapeamento IS NULL OR ISJSON(Mapeamento) = 1");
            tabela.HasCheckConstraint("CK_Importacao_Resumo_Json", "Resumo IS NULL OR ISJSON(Resumo) = 1");
        });

        builder.HasKey(i => i.Id).HasName("PK_Importacao");
        builder.HasIndex(i => i.CorrelacaoId).IsUnique().HasDatabaseName("UQ_Importacao_CorrelacaoId");
        builder.HasIndex(i => new { i.EntidadeId, i.CriadoEm }).HasDatabaseName("IX_Importacao_Entidade_CriadoEm");

        // O banco tem DEFAULT NEWSEQUENTIALID(), mas o Id é gerado pelo EF (como o do Registro): o arquivo entra no mesmo SaveChanges.
        builder.Property(i => i.Id).HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();
        builder.Property(i => i.GrupoId).HasColumnType("uniqueidentifier");
        builder.Property(i => i.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(i => i.UsuarioId).HasColumnType("uniqueidentifier");
        builder.Property(i => i.EquipePadraoId).HasColumnType("uniqueidentifier");
        builder.Property(i => i.NomeArquivo).HasColumnType("nvarchar(260)").IsRequired();
        builder.Property(i => i.Formato)
            .HasColumnType("varchar(4)")
            .HasConversion(f => ImportacaoConversor.ParaTexto(f), t => ImportacaoConversor.FormatoDeTexto(t));
        // Sem HasDefaultValue: o valor é sempre enviado (como a SituacaoAprovacao do Registro).
        builder.Property(i => i.Situacao)
            .HasColumnType("varchar(10)")
            .HasConversion(s => ImportacaoConversor.ParaTexto(s), t => ImportacaoConversor.SituacaoDeTexto(t));
        builder.Property(i => i.Opcoes).HasColumnType("nvarchar(max)");
        builder.Property(i => i.Mapeamento).HasColumnType("nvarchar(max)");
        builder.Property(i => i.Resumo).HasColumnType("nvarchar(max)");
        builder.Property(i => i.CorrelacaoId).HasColumnType("uniqueidentifier");
        builder.Property(i => i.CriadoEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.Data);
        builder.Property(i => i.ConcluidaEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.DataOpcional);
        builder.Property(i => i.DesfeitaEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.DataOpcional);
        builder.Property(i => i.Versao).HasColumnType("rowversion").IsRowVersion();

        builder.HasOne<GrupoTabela>().WithMany()
            .HasForeignKey(i => i.GrupoId)
            .HasConstraintName("FK_Importacao_Grupo")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(i => i.EntidadeId)
            .HasConstraintName("FK_Importacao_Entidade")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(i => i.UsuarioId)
            .HasConstraintName("FK_Importacao_Usuario")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Equipe>().WithMany()
            .HasForeignKey(i => i.EquipePadraoId)
            .HasConstraintName("FK_Importacao_Equipe")
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>catalogo.ImportacaoArquivo — espelha database/scripts/V011__importacao.sql.</summary>
internal sealed class ImportacaoArquivoConfiguracao : IEntityTypeConfiguration<ImportacaoArquivo>
{
    public void Configure(EntityTypeBuilder<ImportacaoArquivo> builder)
    {
        builder.ToTable("ImportacaoArquivo");
        builder.HasKey(a => a.ImportacaoId).HasName("PK_ImportacaoArquivo");
        builder.Property(a => a.ImportacaoId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
        builder.Property(a => a.Conteudo).HasColumnType("varbinary(max)").IsRequired();
        builder.HasOne<Importacao>().WithOne()
            .HasForeignKey<ImportacaoArquivo>(a => a.ImportacaoId)
            .HasConstraintName("FK_ImportacaoArquivo_Importacao")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
