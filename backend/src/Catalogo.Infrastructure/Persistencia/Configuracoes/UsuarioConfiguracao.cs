using Catalogo.Domain.Acesso;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure.Persistencia.Configuracoes;

/// <summary>catalogo.Usuario — espelha database/scripts/V003__usuarios_equipes_historico.sql.</summary>
internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario", tabela =>
        {
            tabela.HasCheckConstraint("CK_Usuario_Perfil", "Perfil IN ('EXTERNO','INTERNO','GESTOR','ADMINISTRADOR')");
            tabela.HasCheckConstraint("CK_Usuario_TentativasFalhas", "TentativasFalhas >= 0");
        });

        builder.HasKey(u => u.Id).HasName("PK_Usuario");
        builder.HasAlternateKey(u => u.Matricula).HasName("UQ_Usuario_Matricula");

        // Gerado pelo EF ao adicionar (GUID sequencial), para o histórico usar o Id antes do SaveChanges.
        builder.Property(u => u.Id).HasColumnType("uniqueidentifier").ValueGeneratedOnAdd();
        builder.Property(u => u.Matricula).HasColumnType("varchar(20)").IsRequired();
        builder.Property(u => u.Nome).HasColumnType("nvarchar(150)").IsRequired();
        builder.Property(u => u.SenhaHash).HasColumnType("nvarchar(500)").IsRequired();
        builder.Property(u => u.Perfil)
            .HasColumnType("varchar(20)")
            .HasConversion(perfil => PerfilConversor.ParaTexto(perfil), texto => PerfilConversor.DeTexto(texto))
            .IsRequired();
        builder.Property(u => u.Ativo).HasColumnType("bit").HasDefaultValue(true);
        builder.Property(u => u.DeveTrocarSenha).HasColumnType("bit").HasDefaultValue(true);
        builder.Property(u => u.TentativasFalhas).HasColumnType("int").HasDefaultValue(0);
        builder.Property(u => u.BloqueadoAte).HasColumnType("datetime2").HasConversion(ConversoresUtc.DataOpcional);
        builder.Property(u => u.UltimoAcessoEm).HasColumnType("datetime2").HasConversion(ConversoresUtc.DataOpcional);
        builder.Property(u => u.CriadoEm).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(ConversoresUtc.Data);

        builder.Ignore(u => u.EhAdministrador);
    }
}
