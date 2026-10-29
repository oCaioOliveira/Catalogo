using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.CasosDeUso.Administracao;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.Tests.CasosDeUso;

// RN23 a RN25 (login e senha), RN30 (último administrador), RN31 (equipe obrigatória) e RN32 (histórico sem senha).
public class AcessoTests
{
    private readonly UsuarioRepositoryFalso _usuarios = new();
    private readonly EquipeRepositoryFalso _equipes = new();
    private readonly HistoricoRepositoryFalso _historico = new();
    private readonly HasherSenhaFalso _hasher = new();
    private readonly RelogioFalso _relogio = new();
    private readonly SituacaoUsuariosFalsa _situacao = new();
    private readonly UsuarioAtualFalso _atual = new();
    private readonly UnidadeDeTrabalhoNula _unidadeDeTrabalho = new();
    private readonly Equipe _equipe = new() { Id = Guid.NewGuid(), Nome = "Equipe" };

    public AcessoTests()
    {
        _equipes.Equipes.Add(_equipe);
    }

    private ProvedorIdentidadeLocal Provedor => new(_usuarios, _hasher, _unidadeDeTrabalho, _relogio);
    private ServicoHistorico Historico => new(_historico, _relogio);

    [Fact]
    public async Task Autenticar_SenhaCerta_DevolveOUsuarioERegistraOAcesso()
    {
        var usuario = Cadastrar("joao", "Senha12345");

        var autenticado = await Provedor.AutenticarAsync("JOAO", "Senha12345", CancellationToken.None);

        Assert.Same(usuario, autenticado);
        Assert.Equal(_relogio.Agora.UtcDateTime, usuario.UltimoAcessoEm);
    }

    [Fact]
    public async Task Autenticar_CincoSenhasErradas_BloqueiaInclusiveASenhaCerta()
    {
        // RN24
        Cadastrar("joao", "Senha12345");
        for (var i = 0; i < 5; i++)
            Assert.Null(await Provedor.AutenticarAsync("joao", "errada", CancellationToken.None));

        Assert.Null(await Provedor.AutenticarAsync("joao", "Senha12345", CancellationToken.None));

        _relogio.Agora = _relogio.Agora.AddMinutes(15);
        Assert.NotNull(await Provedor.AutenticarAsync("joao", "Senha12345", CancellationToken.None));
    }

    [Fact]
    public async Task Autenticar_UsuarioInativoOuInexistente_Recusa()
    {
        // RN23
        Cadastrar("inativo", "Senha12345").Ativo = false;

        Assert.Null(await Provedor.AutenticarAsync("inativo", "Senha12345", CancellationToken.None));
        Assert.Null(await Provedor.AutenticarAsync("nao_existe", "Senha12345", CancellationToken.None));
    }

    [Fact]
    public async Task Login_QualquerFalha_TemAMesmaMensagem()
    {
        // RN23: não revela se a matrícula existe.
        Cadastrar("joao", "Senha12345");
        var login = new Login(Provedor, new GeradorTokenFalso(), _equipes);

        var semMatricula = await Assert.ThrowsAsync<NaoAutenticadoException>(() =>
            login.ExecutarAsync(new LoginEntrada("nao_existe", "Senha12345"), CancellationToken.None));
        var senhaErrada = await Assert.ThrowsAsync<NaoAutenticadoException>(() =>
            login.ExecutarAsync(new LoginEntrada("joao", "errada"), CancellationToken.None));

        Assert.Equal(semMatricula.Message, senhaErrada.Message);
    }

    [Fact]
    public async Task TrocarSenha_SenhaAtualErradaENovaFraca_DevolveOsDoisErros()
    {
        // RN25
        var usuario = Cadastrar("joao", "Senha12345");
        _atual.Usuario = Atual(usuario);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            TrocarSenha().ExecutarAsync(new TrocaSenhaEntrada("errada", "curta"), CancellationToken.None));

        Assert.Equal(["novaSenha", "senhaAtual"], erro.Erros.Keys.Order());
    }

    [Fact]
    public async Task TrocarSenha_Valida_TiraAObrigacaoEGravaHistoricoSemASenha()
    {
        // RN25 e RN32
        var usuario = Cadastrar("joao", "Provisoria1", provisoria: true);
        _atual.Usuario = Atual(usuario);

        await TrocarSenha().ExecutarAsync(new TrocaSenhaEntrada("Provisoria1", "NovaSenha123"), CancellationToken.None);

        Assert.False(usuario.DeveTrocarSenha);
        Assert.Equal("hash:NovaSenha123", usuario.SenhaHash);
        var linha = Assert.Single(_historico.Linhas);
        Assert.Contains("\"senha\":\"alterada\"", linha.DadosDepois);
        Assert.DoesNotContain("NovaSenha123", linha.DadosDepois);
        Assert.DoesNotContain("hash:", linha.DadosDepois);
        Assert.Contains(usuario.Id, _situacao.Invalidados);
    }

    [Fact]
    public async Task CriarPrimeiroAdministrador_SemAdministrador_CriaComTrocaObrigatoria()
    {
        var caso = new CriarPrimeiroAdministrador(_usuarios, _hasher, _unidadeDeTrabalho, Historico);

        await caso.ExecutarAsync("admin", "Administrador", "SenhaForte123", CancellationToken.None);

        var admin = Assert.Single(_usuarios.Usuarios);
        Assert.True(admin.EhAdministrador);
        Assert.True(admin.DeveTrocarSenha);
        Assert.Equal(admin.Id, Assert.Single(_historico.Linhas).UsuarioId);   // autor = o próprio
    }

    [Fact]
    public async Task CriarPrimeiroAdministrador_JaExisteAdministradorAtivo_Recusa()
    {
        // RN30
        Cadastrar("admin", "Senha12345").Perfil = Perfil.Administrador;
        var caso = new CriarPrimeiroAdministrador(_usuarios, _hasher, _unidadeDeTrabalho, Historico);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            caso.ExecutarAsync("outro", "Outro", "SenhaForte123", CancellationToken.None));
    }

    [Fact]
    public async Task DesativarOuRebaixar_UltimoAdministrador_Recusa()
    {
        // RN30
        var admin = Cadastrar("admin", "Senha12345");
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
        var caso = new CriarUsuario(_atual, _usuarios, _equipes, _hasher, _unidadeDeTrabalho, Historico);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            caso.ExecutarAsync(new UsuarioEntrada("m1", "Maria", "INTERNO", []), CancellationToken.None));

        Assert.True(erro.Erros.ContainsKey("equipes"));
    }

    [Fact]
    public async Task CriarUsuario_SenhaProvisoriaGeradaFuncionaUmaVezEObrigaATroca()
    {
        _atual.Usuario = UsuarioAtualFalso.Criar(Perfil.Administrador);
        var caso = new CriarUsuario(_atual, _usuarios, _equipes, _hasher, _unidadeDeTrabalho, Historico);

        var criado = await caso.ExecutarAsync(new UsuarioEntrada("m1", "Maria", "interno", [_equipe.Id]), CancellationToken.None);

        Assert.Empty(PoliticaSenha.Validar(criado.SenhaProvisoria));
        Assert.True(criado.Usuario.DeveTrocarSenha);
        Assert.Equal("INTERNO", criado.Usuario.Perfil);
        Assert.NotNull(await Provedor.AutenticarAsync("m1", criado.SenhaProvisoria, CancellationToken.None));
        Assert.DoesNotContain(criado.SenhaProvisoria, _historico.Linhas.Single().DadosDepois);
    }

    [Fact]
    public async Task CriarUsuario_PorNaoAdministrador_LancaAcessoNegado()
    {
        _atual.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, _equipe.Id);
        var caso = new CriarUsuario(_atual, _usuarios, _equipes, _hasher, _unidadeDeTrabalho, Historico);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            caso.ExecutarAsync(new UsuarioEntrada("m1", "Maria", "INTERNO", [_equipe.Id]), CancellationToken.None));
    }

    [Fact]
    public async Task RedefinirSenha_MarcaComoRedefinidaNoHistoricoSemValor()
    {
        // RN32
        var usuario = Cadastrar("joao", "Senha12345");
        _atual.Usuario = UsuarioAtualFalso.Criar(Perfil.Administrador);
        var caso = new RedefinirSenhaUsuario(_atual, _usuarios, _hasher, _unidadeDeTrabalho, Historico, _situacao);

        var nova = await caso.ExecutarAsync(usuario.Id, CancellationToken.None);

        var linha = Assert.Single(_historico.Linhas);
        Assert.Equal(OperacaoHistorico.Alteracao, linha.Operacao);
        Assert.Contains("\"senha\":\"redefinida\"", linha.DadosDepois);
        Assert.DoesNotContain(nova.SenhaProvisoria, linha.DadosDepois);
        Assert.True(usuario.DeveTrocarSenha);
    }

    private TrocarSenha TrocarSenha() => new(_atual, _usuarios, _hasher, _unidadeDeTrabalho, Historico, _situacao);

    private Usuario Cadastrar(string matricula, string senha, bool provisoria = false)
    {
        var usuario = new Usuario { Matricula = matricula, Nome = matricula, Perfil = Perfil.Interno };
        usuario.DefinirSenha(_hasher.GerarHash(senha), provisoria);
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
