using Catalogo.Domain.Acesso;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Metadados.Administracao;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Acesso;

// RN90 (visibilidade da listagem), RN91 (as equipes do usuário nesta listagem), RN92 (equipe removida) e RN93 (listagem nova).
public class ListagemPorEquipeTests
{
    private static readonly Guid EquipeT = Guid.NewGuid();
    private static readonly Guid EquipeD = Guid.NewGuid();
    private static readonly Guid EquipeX = Guid.NewGuid();

    public enum Cenario
    {
        EquipeHabilitada,
        UmaDasDuasHabilitada,
        NenhumaHabilitada,
        ListagemSemEquipes,
        ListagemDesativada,
    }

    // RN90: o ADMINISTRADOR vê todas; os demais, só listagens ATIVAS com alguma equipe deles habilitada. As equipes do usuário
    // já são só as ATIVAS (UsuarioAtual.Equipes): uma equipe inativa habilitada não conta, como no caso "NenhumaHabilitada".
    [Theory]
    [InlineData(Perfil.Externo, Cenario.EquipeHabilitada, true)]
    [InlineData(Perfil.Interno, Cenario.EquipeHabilitada, true)]
    [InlineData(Perfil.Gestor, Cenario.EquipeHabilitada, true)]
    [InlineData(Perfil.Gestor, Cenario.UmaDasDuasHabilitada, true)]
    [InlineData(Perfil.Externo, Cenario.NenhumaHabilitada, false)]
    [InlineData(Perfil.Interno, Cenario.NenhumaHabilitada, false)]
    [InlineData(Perfil.Gestor, Cenario.NenhumaHabilitada, false)]
    [InlineData(Perfil.Interno, Cenario.ListagemSemEquipes, false)]
    [InlineData(Perfil.Gestor, Cenario.ListagemSemEquipes, false)]
    [InlineData(Perfil.Gestor, Cenario.ListagemDesativada, false)]
    [InlineData(Perfil.Administrador, Cenario.NenhumaHabilitada, true)]
    [InlineData(Perfil.Administrador, Cenario.ListagemSemEquipes, true)]
    [InlineData(Perfil.Administrador, Cenario.ListagemDesativada, true)]
    public void PodeVerListagem_Matriz(Perfil perfil, Cenario cenario, bool esperado)
    {
        var equipesDoUsuario = cenario == Cenario.UmaDasDuasHabilitada ? new[] { EquipeT, EquipeD } : [EquipeT];
        var grupo = cenario switch
        {
            Cenario.EquipeHabilitada => Grupo([EquipeT]),
            Cenario.UmaDasDuasHabilitada => Grupo([EquipeD]),
            Cenario.NenhumaHabilitada => Grupo([EquipeX]),
            Cenario.ListagemSemEquipes => Grupo([]),
            _ => Grupo([EquipeT], ativo: false),
        };

        Assert.Equal(esperado, PoliticaPermissao.PodeVerListagem(Usuario(perfil, equipesDoUsuario), grupo));
    }

    [Fact]
    public void PodeVerListagem_EquipeInativaDoUsuario_NaoConta()
    {
        // RN90: "suas equipes" são as ATIVAS; o vínculo com uma equipe desativada não chega ao UsuarioAtual (RN31).
        var usuarioSemEquipeAtiva = Usuario(Perfil.Interno, []);

        Assert.False(PoliticaPermissao.PodeVerListagem(usuarioSemEquipeAtiva, Grupo([EquipeT])));
    }

    [Fact]
    public void EquipesNaListagem_NaoAdministrador_EhAIntersecao()
    {
        // RN91
        var gestor = Usuario(Perfil.Gestor, [EquipeT, EquipeD]);

        Assert.Equal([EquipeD], PoliticaPermissao.EquipesNaListagem(gestor, new HashSet<Guid> { EquipeD, EquipeX }));
        Assert.Empty(PoliticaPermissao.EquipesNaListagem(gestor, new HashSet<Guid> { EquipeX }));
    }

    [Fact]
    public void EquipesNaListagem_Administrador_TodasAsHabilitadas()
    {
        // RN91: o administrador não pertence a equipes, mas cria e configura em qualquer equipe habilitada.
        var habilitadas = new HashSet<Guid> { EquipeD, EquipeX };

        Assert.Equal(habilitadas, PoliticaPermissao.EquipesNaListagem(Usuario(Perfil.Administrador, []), habilitadas));
    }

    [Fact]
    public void NaListagem_ItensDeEquipeNaoHabilitada_SaoNaoEncontrados()
    {
        // RN91 e RN92: dentro da listagem, o item de uma equipe do usuário que não está habilitada nela "não existe" (404).
        var gestor = PoliticaPermissao.NaListagem(Usuario(Perfil.Gestor, [EquipeT, EquipeD]), Grupo([EquipeD]));

        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.Decidir(gestor, OperacaoRegistro.Ver, false, EquipeD));
        Assert.Equal(DecisaoPermissao.NaoEncontrado, PoliticaPermissao.Decidir(gestor, OperacaoRegistro.Ver, false, EquipeT));
        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.Decidir(gestor, OperacaoRegistro.Ver, true, null));
        Assert.Equal([EquipeD], PoliticaPermissao.EquipesVisiveis(gestor));
    }

    [Fact]
    public void NaListagem_Administrador_VeOsItensOcultos()
    {
        // RN92: os itens de uma equipe removida ficam visíveis só para o administrador (nada é apagado).
        var admin = PoliticaPermissao.NaListagem(Usuario(Perfil.Administrador, []), Grupo([EquipeD]));

        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.Decidir(admin, OperacaoRegistro.Ver, false, EquipeT));
        Assert.Null(PoliticaPermissao.EquipesVisiveis(admin));
    }

    [Fact]
    public void ResolverEquipe_UmaSoEquipeNaListagem_EhUsadaSemInformar()
    {
        // RN91 + RN27: o GESTOR de T e D, numa listagem só de D, cria em D sem informar equipeId.
        var gestor = PoliticaPermissao.NaListagem(Usuario(Perfil.Gestor, [EquipeT, EquipeD]), Grupo([EquipeD]));

        var resolucao = PoliticaPermissao.ResolverEquipe(gestor, PorEquipe(), null);

        Assert.Equal(EquipeD, resolucao.EquipeId);
    }

    [Fact]
    public void ResolverEquipe_EquipeDoUsuarioNaoHabilitada_EhProibida()
    {
        // RN91: só equipes do conjunto (403, como uma equipe da qual não participa).
        var gestor = PoliticaPermissao.NaListagem(Usuario(Perfil.Gestor, [EquipeT, EquipeD]), Grupo([EquipeD]));

        Assert.True(PoliticaPermissao.ResolverEquipe(gestor, PorEquipe(), EquipeT).Proibido);
    }

    [Fact]
    public void ResolverEquipe_AdministradorEmEquipeNaoHabilitada_EhInvalida()
    {
        // RN91: o administrador escolhe qualquer equipe HABILITADA; outra é 400.
        var admin = PoliticaPermissao.NaListagem(Usuario(Perfil.Administrador, []), Grupo([EquipeD]));

        Assert.Equal(EquipeD, PoliticaPermissao.ResolverEquipe(admin, PorEquipe(), EquipeD).EquipeId);
        Assert.NotNull(PoliticaPermissao.ResolverEquipe(admin, PorEquipe(), EquipeT).ErroValidacao);
    }

    [Theory]
    [InlineData(Perfil.Administrador)]
    [InlineData(Perfil.Gestor)]
    public void PodeConfigurarEquipe_SoEquipesHabilitadas(Perfil perfil)
    {
        // RN91: aprovação e avaliação só para equipes habilitadas na listagem.
        var usuario = PoliticaPermissao.NaListagem(Usuario(perfil, [EquipeT, EquipeD]), Grupo([EquipeD]));

        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.PodeConfigurarEquipe(usuario, EquipeD));
        Assert.Equal(DecisaoPermissao.NaoEncontrado, PoliticaPermissao.PodeConfigurarEquipe(usuario, EquipeT));
    }

    [Fact]
    public void PodeConfigurarEquipe_AdministradorForaDeListagem_QualquerEquipe()
    {
        // Sem listagem (EquipesHabilitadasNaListagem nulo), a regra de antes: o administrador configura qualquer equipe.
        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.PodeConfigurarEquipe(Usuario(Perfil.Administrador, []), EquipeX));
    }

    [Fact]
    public void Planejador_TirarEquipe_AvisaItensOcultosSemBloquear()
    {
        // RN92: uma verificação NÃO bloqueante por equipe removida; habilitar não verifica nada.
        var grupo = Grupo([EquipeT, EquipeD]);
        var nomes = new Dictionary<Guid, string> { [EquipeT] = "T", [EquipeD] = "D", [EquipeX] = "X" };

        var plano = PlanejadorAlteracoes.EquipesDaListagem(grupo, new HashSet<Guid> { EquipeD, EquipeX }, nomes);

        var verificacao = Assert.IsType<RegistrosDaEquipeNoGrupo>(Assert.Single(plano.Verificacoes));
        Assert.Equal(EquipeT, verificacao.EquipeId);
        Assert.Equal(PlanejadorAlteracoes.ItensOcultos, verificacao.Tipo);
        Assert.False(verificacao.Bloqueia);
        Assert.Contains("'T'", verificacao.Descricao);
        Assert.Empty(plano.Escritas);
    }

    [Fact]
    public void Planejador_SoHabilitar_SemVerificacao()
    {
        var plano = PlanejadorAlteracoes.EquipesDaListagem(Grupo([]), new HashSet<Guid> { EquipeT }, new Dictionary<Guid, string>());

        Assert.Empty(plano.Verificacoes);
    }

    private static Grupo Grupo(Guid[] habilitadas, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(),
        Chave = "tasks",
        Nome = "Tasks",
        Ativo = ativo,
        Entidades = [],
        EquipesHabilitadas = habilitadas.ToHashSet(),
    };

    private static Entidade PorEquipe() => Metadado.Entidade([]);

    private static UsuarioAtual Usuario(Perfil perfil, Guid[] equipes) => new()
    {
        Id = Guid.NewGuid(),
        Matricula = "m",
        Nome = "n",
        Perfil = perfil,
        Equipes = equipes.ToHashSet(),
    };
}
