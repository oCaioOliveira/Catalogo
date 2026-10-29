using Catalogo.Domain.Acesso;

namespace Catalogo.Domain.Tests.Acesso;

public class RegrasDeAcessoTests
{
    private static readonly DateTime Agora = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    // RN25 — política de senha
    [Theory]
    [InlineData("abcdefghi1", true)]    // 10 caracteres, letra e número: limite
    [InlineData("abcdefgh1", false)]    // 9 caracteres
    [InlineData("abcdefghij", false)]   // sem número
    [InlineData("1234567890", false)]   // sem letra
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PoliticaSenha_Validar(string? senha, bool valida)
    {
        Assert.Equal(valida, PoliticaSenha.Validar(senha).Count == 0);
    }

    [Fact]
    public void PoliticaSenha_NovaIgualAAtual_EhRecusada()
    {
        Assert.Contains("diferente da senha atual", Assert.Single(PoliticaSenha.Validar("Senha12345", "Senha12345")));
    }

    [Fact]
    public void PoliticaSenha_VariosProblemas_DevolveTodos()
    {
        Assert.Equal(2, PoliticaSenha.Validar("abc").Count);   // curta e sem número
    }

    [Fact]
    public void PoliticaSenha_GerarProvisoria_SempreAtendeAPoliticaENaoSeRepete()
    {
        var senhas = Enumerable.Range(0, 200).Select(_ => PoliticaSenha.GerarProvisoria()).ToList();

        Assert.All(senhas, s => Assert.Empty(PoliticaSenha.Validar(s)));
        Assert.Equal(senhas.Count, senhas.Distinct().Count());
    }

    // RN24 — bloqueio por tentativas
    [Fact]
    public void Usuario_QuatroErros_AindaNaoBloqueia()
    {
        var usuario = new Usuario();
        for (var i = 0; i < 4; i++)
            usuario.RegistrarFalhaDeLogin(Agora);

        Assert.False(usuario.EstaBloqueado(Agora));
        Assert.Equal(4, usuario.TentativasFalhas);
    }

    [Fact]
    public void Usuario_CincoErros_BloqueiaPor15Minutos()
    {
        var usuario = new Usuario();
        for (var i = 0; i < 5; i++)
            usuario.RegistrarFalhaDeLogin(Agora);

        Assert.True(usuario.EstaBloqueado(Agora));
        Assert.True(usuario.EstaBloqueado(Agora.AddMinutes(14).AddSeconds(59)));
        Assert.False(usuario.EstaBloqueado(Agora.AddMinutes(15)));
    }

    [Fact]
    public void Usuario_ErroDepoisDoBloqueioVencer_RecomecaAContagem()
    {
        var usuario = new Usuario();
        for (var i = 0; i < 5; i++)
            usuario.RegistrarFalhaDeLogin(Agora);

        usuario.RegistrarFalhaDeLogin(Agora.AddMinutes(16));

        Assert.False(usuario.EstaBloqueado(Agora.AddMinutes(16)));
        Assert.Equal(1, usuario.TentativasFalhas);
    }

    [Fact]
    public void Usuario_LoginCerto_ZeraTentativasERegistraAcesso()
    {
        var usuario = new Usuario();
        usuario.RegistrarFalhaDeLogin(Agora);

        usuario.RegistrarLoginComSucesso(Agora);

        Assert.Equal(0, usuario.TentativasFalhas);
        Assert.Equal(Agora, usuario.UltimoAcessoEm);
    }

    [Fact]
    public void Usuario_DefinirSenhaProvisoria_ObrigaATrocaEDesbloqueia()
    {
        // RN25
        var usuario = new Usuario { DeveTrocarSenha = false };
        for (var i = 0; i < 5; i++)
            usuario.RegistrarFalhaDeLogin(Agora);

        usuario.DefinirSenha("novo-hash", provisoria: true);

        Assert.True(usuario.DeveTrocarSenha);
        Assert.False(usuario.EstaBloqueado(Agora));
        Assert.Equal("novo-hash", usuario.SenhaHash);
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
