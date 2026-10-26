using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.Registro — espelha database/scripts/V001__listagem_generica.sql.</summary>
internal sealed class RegistroConfiguracao : IEntityTypeConfiguration<Registro>
{
    public void Configure(EntityTypeBuilder<Registro> builder)
    {
        builder.ToTable("Registro", tabela =>
        {
            tabela.HasCheckConstraint("CK_Registro_Dados_Json", "ISJSON(Dados) = 1");
            tabela.HasCheckConstraint("CK_Registro_SituacaoAprovacao", "SituacaoAprovacao IN ('PENDENTE','APROVADO','REJEITADO')");
        });

        builder.HasKey(r => r.Id).HasName("PK_Registro");
        // Alvo das FKs compostas de RegistroReferencia e RegistroChaveUnica.
        builder.HasAlternateKey(r => new { r.Id, r.EntidadeId }).HasName("UQ_Registro_Id_Entidade");
        builder.HasIndex(r => new { r.EntidadeId, r.CriadoEm })
            .HasDatabaseName("IX_Registro_Entidade_CriadoEm")
            .IsDescending(false, true);

        // O banco tem DEFAULT NEWSEQUENTIALID(), mas o Id é gerado pelo EF ao adicionar o registro
        // (GUID sequencial, bom para o índice clusterizado). Assim RegistroReferencia e
        // RegistroChaveUnica recebem o Id antes do INSERT, tudo no mesmo SaveChanges.
        builder.Property(r => r.Id).HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();
        builder.Property(r => r.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.Dados).HasColumnType("nvarchar(max)").HasDefaultValueSql("N'{}'").IsRequired();
        // datetime2 não guarda o fuso: o valor é sempre UTC (SYSUTCDATETIME), então marcamos como UTC ao ler.
        builder.Property(r => r.CriadoEm)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(valor => valor, valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc));
        builder.Property(r => r.AtualizadoEm)
            .HasColumnType("datetime2")
            .HasConversion(valor => valor, valor => valor == null ? null : DateTime.SpecifyKind(valor.Value, DateTimeKind.Utc));
        // rowversion: o SQL Server muda a cada UPDATE; o EF usa na concorrência otimista (RN12).
        builder.Property(r => r.Versao).HasColumnType("rowversion").IsRowVersion();

        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(r => r.EntidadeId)
            .HasConstraintName("FK_Registro_Entidade")
            .OnDelete(DeleteBehavior.NoAction);

        // V004: aprovação (RN34, RN36). SEM HasDefaultValue de propósito: o valor padrão do enum (0) é
        // PENDENTE, e o EF deixaria de enviar PENDENTE achando que é "valor não informado".
        builder.Property(r => r.SituacaoAprovacao)
            .HasColumnType("varchar(20)")
            .HasConversion(s => AprovacaoConversor.ParaTexto(s), t => AprovacaoConversor.SituacaoDeTexto(t))
            .IsRequired();
        builder.Property(r => r.DecididoPorId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.DecididoEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.DataOpcional);
        builder.Property(r => r.JustificativaDecisao).HasColumnType("nvarchar(1000)");
        builder.HasIndex(r => new { r.SituacaoAprovacao, r.EquipeId, r.CriadoEm }).HasDatabaseName("IX_Registro_Situacao_Equipe");
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(r => r.DecididoPorId)
            .HasConstraintName("FK_Registro_DecididoPor")
            .OnDelete(DeleteBehavior.NoAction);

        // V003: equipe dona e autores (RN27, RN32).
        builder.Property(r => r.EquipeId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.CriadoPorId).HasColumnType("uniqueidentifier");
        builder.Property(r => r.AtualizadoPorId).HasColumnType("uniqueidentifier");
        builder.HasIndex(r => new { r.EntidadeId, r.EquipeId }).HasDatabaseName("IX_Registro_Entidade_Equipe");

        builder.HasOne<Equipe>().WithMany()
            .HasForeignKey(r => r.EquipeId)
            .HasConstraintName("FK_Registro_Equipe")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(r => r.CriadoPorId)
            .HasConstraintName("FK_Registro_CriadoPor")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(r => r.AtualizadoPorId)
            .HasConstraintName("FK_Registro_AtualizadoPor")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
