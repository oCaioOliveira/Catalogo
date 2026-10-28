using Catalogo.Domain.Acesso;
using Catalogo.Domain.Historico;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// catalogo.HistoricoAlteracao — espelha database/scripts/V003__usuarios_equipes_historico.sql.
/// Sem FK para AlvoId: o histórico sobrevive à exclusão do alvo.
/// </summary>
internal sealed class HistoricoAlteracaoConfiguracao : IEntityTypeConfiguration<HistoricoAlteracao>
{
    public void Configure(EntityTypeBuilder<HistoricoAlteracao> builder)
    {
        builder.ToTable("HistoricoAlteracao", tabela =>
        {
            // V004: CONFIGURACAO, APROVACAO e REJEICAO. V005: MODELO_AVALIACAO e AVALIACAO. V006: METADADO.
            tabela.HasCheckConstraint("CK_HistoricoAlteracao_AlvoTipo",
                "AlvoTipo IN ('REGISTRO','USUARIO','EQUIPE','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO','METADADO')");
            tabela.HasCheckConstraint("CK_HistoricoAlteracao_Operacao", "Operacao IN ('CRIACAO','ALTERACAO','EXCLUSAO','APROVACAO','REJEICAO')");
            // V013: sem os CHECKs de ISJSON (SQL Server 2014); o JSON é sempre gerado pela aplicação.
            // V011: a origem da gravação (nula = cadastro manual).
            tabela.HasCheckConstraint("CK_HistoricoAlteracao_Origem", "Origem IS NULL OR Origem IN ('IMPORTACAO')");
        });

        builder.HasKey(h => h.Id).HasName("PK_HistoricoAlteracao");
        builder.HasIndex(h => new { h.AlvoTipo, h.AlvoId, h.OcorridoEm }).HasDatabaseName("IX_HistoricoAlteracao_Alvo");

        builder.Property(h => h.Id).HasColumnType("bigint").UseIdentityColumn();
        builder.Property(h => h.AlvoTipo)
            .HasColumnType("varchar(20)")
            .HasConversion(alvo => HistoricoConversor.ParaTexto(alvo), texto => HistoricoConversor.AlvoDeTexto(texto));
        builder.Property(h => h.AlvoId).HasColumnType("uniqueidentifier");
        builder.Property(h => h.EntidadeId).HasColumnType("uniqueidentifier");
        builder.Property(h => h.EquipeId).HasColumnType("uniqueidentifier");
        builder.Property(h => h.Operacao)
            .HasColumnType("varchar(20)")
            .HasConversion(operacao => HistoricoConversor.ParaTexto(operacao), texto => HistoricoConversor.OperacaoDeTexto(texto));
        builder.Property(h => h.UsuarioId).HasColumnType("uniqueidentifier");
        builder.Property(h => h.UsuarioMatricula).HasColumnType("varchar(20)").IsRequired();
        builder.Property(h => h.OcorridoEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.Data);
        builder.Property(h => h.CorrelacaoId).HasColumnType("uniqueidentifier");
        builder.Property(h => h.Justificativa).HasColumnType("nvarchar(1000)");
        builder.Property(h => h.Origem)
            .HasColumnType("varchar(20)")
            .HasConversion(
                origem => origem.HasValue ? HistoricoConversor.ParaTexto(origem.Value) : null,
                texto => texto == null ? null : HistoricoConversor.OrigemDeTexto(texto));
        builder.Property(h => h.DadosAntes).HasColumnType("nvarchar(max)");
        builder.Property(h => h.DadosDepois).HasColumnType("nvarchar(max)");

        builder.HasOne<Usuario>().WithMany()
            .HasForeignKey(h => h.UsuarioId)
            .HasConstraintName("FK_HistoricoAlteracao_Usuario")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EntidadeTabela>().WithMany()
            .HasForeignKey(h => h.EntidadeId)
            .HasConstraintName("FK_HistoricoAlteracao_Entidade")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Equipe>().WithMany()
            .HasForeignKey(h => h.EquipeId)
            .HasConstraintName("FK_HistoricoAlteracao_Equipe")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
