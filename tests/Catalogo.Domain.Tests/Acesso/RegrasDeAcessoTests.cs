using Catalogo.Domain.Acesso;

namespace Catalogo.Domain.Tests.Acesso;

public class RegrasDeAcessoTests
{
    // RN23 — matrícula a partir do login do Windows
    [Theory]
    [InlineData(@"CORPCAIXA\c161587", "C161587")]
    [InlineData(@"corpcaixa\C161587", "C161587")]
    [InlineData(@"FLORESTA\CORP\c161587", "C161587")]
    [InlineData("c161587@corpcaixa.local", "C161587")]
    [InlineData("  c161587  ", "C161587")]
    [InlineData("admin.testes", "ADMIN.TESTES")]
    public void MatriculaWindows_Extrair_TiraODominioEPoeEmMaiusculas(string nome, string esperada)
    {
        Assert.Equal(esperada, MatriculaWindows.Extrair(nome));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"CORPCAIXA\")]
    [InlineData("@corpcaixa")]
    [InlineData(@"CORPCAIXA\c16 1587")]
    [InlineData(@"CORPCAIXA\c1234567890123456789012")]   // 22 caracteres: mais que a coluna aceita
    public void MatriculaWindows_Extrair_SemMatriculaValida_DevolveNulo(string? nome)
    {
        Assert.Null(MatriculaWindows.Extrair(nome));
    }

    [Fact]
    public void Usuario_RegistrarAcesso_GuardaAHora()
    {
        var agora = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var usuario = new Usuario();

        usuario.RegistrarAcesso(agora);

        Assert.Equal(agora, usuario.UltimoAcessoEm);
    }

    // RN30 — último administrador
    [Theory]
    [InlineData(Perfil.Interno, true, 1, true)]          // rebaixar o único
    [InlineData(Perfil.Administrador, false, 1, true)]   // desativar o único
    [InlineData(Perfil.Interno, true, 2, false)]         // há outro administrador ativo
    [InlineData(Perfil.Administrador, true, 1, false)]   // continua administrador ativo
    public void RegrasAdministrador_RemoveriaUltimoAdministrador(Perfil novoPerfil, bool novoAtivo, int administradores, bool esperado)
    {
        var alvo = new Usuario { Perfil = Perfil.Administrador, Ativo = true };

        Assert.Equal(esperado, RegrasAdministrador.RemoveriaUltimoAdministrador(alvo, novoPerfil, novoAtivo, administradores));
    }

    [Fact]
    public void RegrasAdministrador_AlvoQueNaoEhAdministrador_NuncaBloqueia()
    {
        var alvo = new Usuario { Perfil = Perfil.Gestor, Ativo = true };

        Assert.False(RegrasAdministrador.RemoveriaUltimoAdministrador(alvo, Perfil.Externo, false, 1));
    }

    [Theory]
    [InlineData("EXTERNO", Perfil.Externo)]
    [InlineData("gestor", Perfil.Gestor)]
    [InlineData(" Administrador ", Perfil.Administrador)]
    public void PerfilConversor_TentarConverter_AceitaQualquerCaixa(string texto, Perfil esperado)
    {
        Assert.True(PerfilConversor.TentarConverter(texto, out var perfil));
        Assert.Equal(esperado, perfil);
    }

    [Theory]
    [InlineData("CHEFE")]
    [InlineData("")]
    [InlineData(null)]
    public void PerfilConversor_TentarConverter_RecusaDesconhecido(string? texto)
    {
        Assert.False(PerfilConversor.TentarConverter(texto, out _));
    }
}
