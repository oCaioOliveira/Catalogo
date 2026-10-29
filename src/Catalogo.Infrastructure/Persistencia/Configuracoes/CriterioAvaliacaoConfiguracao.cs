using Catalogo.Domain.Avaliacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.CriterioAvaliacao — espelha database/scripts/V005__avaliacao_itens.sql.</summary>
internal sealed class CriterioAvaliacaoConfiguracao : IEntityTypeConfiguration<CriterioAvaliacao>
{
    public void Configure(EntityTypeBuilder<CriterioAvaliacao> builder)
    {
        builder.ToTable("CriterioAvaliacao", tabela =>
        {
            tabela.HasCheckConstraint("CK_CriterioAvaliacao_Tipo", "Tipo IN ('ESTRELAS','TEXTO')");
            tabela.HasCheckConstraint("CK_CriterioAvaliacao_Comentario", "Comentario IN ('NENHUM','OPCIONAL','OBRIGATORIO')");
            tabela.HasCheckConstraint("CK_CriterioAvaliacao_ComentarioSoEmEstrelas", "Tipo = 'ESTRELAS' OR Comentario = 'NENHUM'");
            tabela.HasCheckConstraint("CK_CriterioAvaliacao_TamanhoMaximoTexto", "TamanhoMaximoTexto BETWEEN 1 AND 4000");
        });

        builder.HasKey(c => c.Id).HasName("PK_CriterioAvaliacao");
        builder.HasIndex(c => new { c.ModeloAvaliacaoId, c.Chave }).IsUnique().HasDatabaseName("UQ_CriterioAvaliacao_Modelo_Chave");

        builder.Property(c => c.Id).HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();
        builder.Property(c => c.ModeloAvaliacaoId).HasColumnType("uniqueidentifier");
        builder.Property(c => c.Chave).HasColumnType("varchar(100)").IsRequired();
        builder.Property(c => c.Nome).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(c => c.Descricao).HasColumnType("nvarchar(500)");
        builder.Property(c => c.Tipo)
            .HasColumnType("varchar(20)")
            .HasConversion(t => AvaliacaoConversor.ParaTexto(t), t => AvaliacaoConversor.TipoDeTexto(t));
        // Sem HasDefaultValue nos campos abaixo: o EF sempre envia o valor da aplicação (ver ModeloAvaliacaoConfiguracao).
        builder.Property(c => c.Obrigatorio).HasColumnType("bit");
        builder.Property(c => c.Comentario)
            .HasColumnType("varchar(20)")
            .HasConversion(c => AvaliacaoConversor.ParaTexto(c), t => AvaliacaoConversor.ComentarioDeTexto(t));
        builder.Property(c => c.TamanhoMaximoTexto).HasColumnType("int");
        builder.Property(c => c.Ordem).HasColumnType("int");
        builder.Property(c => c.Ativo).HasColumnType("bit");
        builder.Property(c => c.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);
    }
}
