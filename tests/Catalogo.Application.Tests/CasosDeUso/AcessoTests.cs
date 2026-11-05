using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.CasosDeUso.Administracao;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.Tests.CasosDeUso;

// RN23 (login pelo Windows), RN105 (primeiro acesso), RN30 (último administrador), RN31 (equipe obrigatória) e RN32 (histórico).
public class AcessoTests
{
    private readonly UsuarioRepositoryFalso _usuarios = new();
    private readonly EquipeRepositoryFalso _equipes = new();
    private readonly HistoricoRepositoryFalso _historico = new();
    private readonly RelogioFalso _relogio = new();
    private readonly SituacaoUsuariosFalsa _situacao = new();
    private readonly UsuarioAtualFalso _atual = new();
    private readonly UnidadeDeTrabalhoNula _unidadeDeTrabalho = new();
    private readonly Equipe _equipe = new() { Id = Guid.NewGuid(), Nome = "Equipe" };

    public AcessoTests()
    {
        _equipes.Equipes.Add(_equipe);
    }

    private Login Login => new(_usuarios, _equipes, new GeradorTokenFalso(), _unidadeDeTrabalho, Historico, _relogio);
    private ServicoHistorico Historico => new(_historico, _relogio);

    [Fact]
    public async Task Login_UsuarioCadastrado_DevolveTokenERegistraOAcesso()
    {
        var usuario = Cadastrar("C161587");

        var resultado = await Login.ExecutarAsync(@"CORPCAIXA\c161587", CancellationToken.None);

        Assert.Equal("token", resultado.Token);
        Assert.Equal(usuario.Id, resultado.Usuario.Id);
        Assert.Equal("INTERNO", resultado.Usuario.Perfil);
        Assert.Equal(_relogio.Agora.UtcDateTime, usuario.UltimoAcessoEm);
        Assert.Empty(_historico.Linhas);
    }

    [Fact]
    public async Task Login_MatriculaNova_CriaExternoAtivoSemEquipeComHistorico()
    {
        // RN105
        var resultado = await Login.ExecutarAsync(@"CORPCAIXA\c999001", CancellationToken.None);

        var novo = Assert.Single(_usuarios.Usuarios);
        Assert.Equal("C999001", novo.Matricula);
        Assert.Equal(Perfil.Externo, novo.Perfil);
        Assert.True(novo.Ativo);
        Assert.Empty(novo.Equipes);
        Assert.Empty(resultado.Usuario.Equipes);
        var linha = Assert.Single(_historico.Linhas);
        Assert.Equal(OperacaoHistorico.Criacao, linha.Operacao);
        Assert.Equal(novo.Id, linha.UsuarioId);   // autor = o próprio
    }

    [Fact]
    public async Task Login_UsuarioDesativado_LancaAcessoNegadoSemToken()
    {
        Cadastrar("C161587").Ativo = false;

        var erro = await Assert.ThrowsAsync<AcessoNegadoException>(() => Login.ExecutarAsync(@"CORPCAIXA\C161587", CancellationToken.None));

        Assert.Equal(CodigosErro.SemPermissao, erro.Codigo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"CORPCAIXA\")]
    public async Task Login_SemNomeDoWindows_LancaNaoAutenticado(string? nome)
    {
        await Assert.ThrowsAsync<NaoAutenticadoException>(() => Login.ExecutarAsync(nome, CancellationToken.None));
        Assert.Empty(_usuarios.Usuarios);
    }

    [Fact]
    public async Task CriarPrimeiroAdministrador_SemAdministrador_CriaSemSenha()
    {
        var caso = new CriarPrimeiroAdministrador(_usuarios, _unidadeDeTrabalho, Historico);

        await caso.ExecutarAsync("C161587", "Administrador", CancellationToken.None);

        var admin = Assert.Single(_usuarios.Usuarios);
        Assert.True(admin.EhAdministrador);
        Assert.Equal(admin.Id, Assert.Single(_historico.Linhas).UsuarioId);   // autor = o próprio
    }

    [Fact]
    public async Task CriarPrimeiroAdministrador_MatriculaJaExiste_PromoveAAdministrador()
    {
        // RN105: a pessoa entrou pelo Windows antes do comando e virou EXTERNO.
        var existente = Cadastrar("C161587");
        existente.Perfil = Perfil.Externo;
        var caso = new CriarPrimeiroAdministrador(_usuarios, _unidadeDeTrabalho, Historico);

        var resultado = await caso.ExecutarAsync("c161587", "Fulana", CancellationToken.None);

        Assert.Equal(existente.Id, resultado.Id);
        Assert.Single(_usuarios.Usuarios);
        Assert.True(existente.EhAdministrador);
        Assert.Equal("Fulana", existente.Nome);
        Assert.Equal(OperacaoHistorico.Alteracao, Assert.Single(_historico.Linhas).Operacao);
    }

    [Fact]
    public async Task CriarPrimeiroAdministrador_JaExisteAdministradorAtivo_Recusa()
    {
        // RN30
        Cadastrar("admin").Perfil = Perfil.Administrador;
        var caso = new CriarPrimeiroAdministrador(_usuarios, _unidadeDeTrabalho, Historico);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            caso.ExecutarAsync("outro", "Outro", CancellationToken.None));
    }

    [Fact]
    public async Task DesativarOuRebaixar_UltimoAdministrador_Recusa()
    {
        // RN30
        var admin = Cadastrar("admin");
        admin.Perfil = Perfil.Administrador;
        _atual.Usuario = Atual(admin);

        var desativar = new AlterarAtivacaoUsuario(_atual, _usuarios, _equipes, _unidadeDeTrabalho, Historico, _situacao);
        var rebaixar = new AtualizarUsuario(_atual, _usuarios, _equipes, _unidadeDeTrabalho, Historico, _situacao);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            desativar.ExecutarAsync(admin.Id, new AtivacaoUsuarioEntrada(false), CancellationToken.None));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            rebaixar.ExecutarAsync(admin.Id, new UsuarioEntrada(null, "Admin", "GESTOR", [_equipe.Id]), CancellationToken.None));

        Assert.Equal("ULTIMO_ADMINISTRADOR", erro.Codigo);
        Assert.True(admin.Ativo);
        Assert.True(admin.EhAdministrador);
    }

    [Fact]
    public async Task CriarUsuario_NaoAdministradorSemEquipe_EhErroDeValidacao()
    {
        // RN31
        _atual.Usuario = UsuarioAtualFalso.Criar(Perfil.Administrador);
        var caso = new CriarUsuario(_atual, _usuarios, _equipes, _unidadeDeTrabalho, Historico);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            caso.ExecutarAsync(new UsuarioEntrada("m1", "Maria", "INTERNO", []), CancellationToken.None));

        Assert.True(erro.Erros.ContainsKey("equipes"));
    }

    [Fact]
    public async Task CriarUsuario_PelaMatricula_CriaSemSenhaEOLoginDoWindowsOAcha()
    {
        _atual.Usuario = UsuarioAtualFalso.Criar(Perfil.Administrador);
        var caso = new CriarUsuario(_atual, _usuarios, _equipes, _unidadeDeTrabalho, Historico);

        var criado = await caso.ExecutarAsync(new UsuarioEntrada("C123456", "Maria", "interno", [_equipe.Id]), CancellationToken.None);

        Assert.Equal("INTERNO", criado.Perfil);
        var login = await Login.ExecutarAsync(@"CORPCAIXA\c123456", CancellationToken.None);
        Assert.Equal(criado.Id, login.Usuario.Id);
    }

    [Fact]
    public async Task CriarUsuario_PorNaoAdministrador_LancaAcessoNegado()
    {
        _atual.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, _equipe.Id);
        var caso = new CriarUsuario(_atual, _usuarios, _equipes, _unidadeDeTrabalho, Historico);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            caso.ExecutarAsync(new UsuarioEntrada("m1", "Maria", "INTERNO", [_equipe.Id]), CancellationToken.None));
    }

    private Usuario Cadastrar(string matricula)
    {
        var usuario = new Usuario { Matricula = matricula, Nome = matricula, Perfil = Perfil.Interno };
        usuario.Equipes.Add(new UsuarioEquipe { EquipeId = _equipe.Id });
        _usuarios.Adicionar(usuario);
        return usuario;
    }

    private static UsuarioAtual Atual(Usuario usuario) => new()
    {
        Id = usuario.Id,
        Matricula = usuario.Matricula,
        Nome = usuario.Nome,
        Perfil = usuario.Perfil,
        Equipes = usuario.Equipes.Select(e => e.EquipeId).ToHashSet(),
    };

    private sealed class UnidadeDeTrabalhoNula : Abstracoes.IUnidadeDeTrabalho
    {
        public Task SalvarAlteracoesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void DescartarAlteracoes()
        {
        }

        public async Task<T> ExecutarSerializavelAsync<T>(
            Func<CancellationToken, Task<(T Resultado, bool Confirmar)>> operacao, CancellationToken cancellationToken) =>
            (await operacao(cancellationToken)).Resultado;
    }

    private sealed class GeradorTokenFalso : Abstracoes.IGeradorToken
    {
        public Abstracoes.TokenAcesso Gerar(Usuario usuario) => new("token", DateTime.UtcNow.AddHours(8));
    }
}
