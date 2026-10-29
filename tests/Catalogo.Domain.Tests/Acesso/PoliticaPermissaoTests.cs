using Catalogo.Domain.Acesso;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Acesso;

// RN26 (matriz), RN27 (globais e equipe na criação), RN28 (404) e RN29 (referências entre equipes).
public class PoliticaPermissaoTests
{
    private static readonly Guid EquipeA = Guid.NewGuid();
    private static readonly Guid EquipeB = Guid.NewGuid();

    public enum Situacao
    {
        MesmaEquipe,
        OutraEquipe,
        RegistroGlobal,
    }

    // Matriz completa: perfil × operação × (mesma equipe, outra equipe, global).
    // O usuário pertence só à Equipe A (o administrador não pertence a nenhuma).
    [Theory]
    // EXTERNO: vê A e globais; não escreve nada. Outra equipe = 404.
    [InlineData(Perfil.Externo, OperacaoRegistro.Ver, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Criar, Situacao.MesmaEquipe, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Alterar, Situacao.MesmaEquipe, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Excluir, Situacao.MesmaEquipe, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Ver, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Criar, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Alterar, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Excluir, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Ver, Situacao.RegistroGlobal, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Criar, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Alterar, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Externo, OperacaoRegistro.Excluir, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    // INTERNO: cria e altera na sua equipe; não exclui; globais só vê.
    [InlineData(Perfil.Interno, OperacaoRegistro.Ver, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Criar, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Alterar, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Excluir, Situacao.MesmaEquipe, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Ver, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Criar, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Alterar, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Excluir, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Ver, Situacao.RegistroGlobal, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Criar, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Alterar, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Interno, OperacaoRegistro.Excluir, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    // GESTOR: tudo na sua equipe; globais só vê.
    [InlineData(Perfil.Gestor, OperacaoRegistro.Ver, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Criar, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Alterar, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Excluir, Situacao.MesmaEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Ver, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Criar, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Alterar, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Excluir, Situacao.OutraEquipe, DecisaoPermissao.NaoEncontrado)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Ver, Situacao.RegistroGlobal, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Criar, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Alterar, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Gestor, OperacaoRegistro.Excluir, Situacao.RegistroGlobal, DecisaoPermissao.Proibido)]
    // ADMINISTRADOR: tudo, em qualquer equipe e nos globais, sem pertencer a nenhuma equipe.
    [InlineData(Perfil.Administrador, OperacaoRegistro.Ver, Situacao.OutraEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, OperacaoRegistro.Criar, Situacao.OutraEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, OperacaoRegistro.Alterar, Situacao.OutraEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, OperacaoRegistro.Excluir, Situacao.OutraEquipe, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, OperacaoRegistro.Ver, Situacao.RegistroGlobal, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, OperacaoRegistro.Criar, Situacao.RegistroGlobal, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, OperacaoRegistro.Alterar, Situacao.RegistroGlobal, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, OperacaoRegistro.Excluir, Situacao.RegistroGlobal, DecisaoPermissao.Permitido)]
    public void Decidir_MatrizDePermissoes(Perfil perfil, OperacaoRegistro operacao, Situacao situacao, DecisaoPermissao esperado)
    {
        var usuario = Usuario(perfil, perfil == Perfil.Administrador ? [] : [EquipeA]);
        var (global, equipe) = situacao switch
        {
            Situacao.MesmaEquipe => (false, (Guid?)EquipeA),
            Situacao.OutraEquipe => (false, EquipeB),
            _ => (true, (Guid?)null),
        };

        Assert.Equal(esperado, PoliticaPermissao.Decidir(usuario, operacao, global, equipe));
    }

    [Fact]
    public void Decidir_UsuarioEmDuasEquipes_VeEAlteraAsDuas()
    {
        var usuario = Usuario(Perfil.Interno, [EquipeA, EquipeB]);

        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Alterar, false, EquipeA));
        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.Decidir(usuario, OperacaoRegistro.Alterar, false, EquipeB));
    }

    [Fact]
    public void Decidir_RegistroDeEntidadePorEquipeSemEquipe_NaoEhVistoPorNaoAdministrador()
    {
        // Registros antigos (antes do V003) ficaram sem equipe: não são "globais".
        Assert.Equal(DecisaoPermissao.NaoEncontrado,
            PoliticaPermissao.Decidir(Usuario(Perfil.Gestor, [EquipeA]), OperacaoRegistro.Ver, registroGlobal: false, null));
    }

    [Theory]
    [InlineData(true, false, true)]   // destino global: sempre pode
    [InlineData(false, true, true)]   // mesma equipe
    [InlineData(false, false, false)] // outra equipe: nunca, mesmo que o usuário pertença às duas (RN29)
    public void PodeReferenciar_RegistroDeEquipe(bool destinoGlobal, bool mesmaEquipe, bool esperado)
    {
        Guid? destino = destinoGlobal ? null : mesmaEquipe ? EquipeA : EquipeB;

        Assert.Equal(esperado, PoliticaPermissao.PodeReferenciar(EquipeA, destino));
    }

    [Fact]
    public void PodeReferenciar_RegistroGlobalSoApontaParaGlobais()
    {
        Assert.True(PoliticaPermissao.PodeReferenciar(null, null));
        Assert.False(PoliticaPermissao.PodeReferenciar(null, EquipeA));
    }

    [Fact]
    public void ResolverEquipe_UsuarioDeUmaEquipeSemInformar_AssumeAEquipeDele()
    {
        var resolucao = PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Interno, [EquipeA]), PorEquipe(), null);

        Assert.Equal(EquipeA, resolucao.EquipeId);
        Assert.Null(resolucao.ErroValidacao);
    }

    [Fact]
    public void ResolverEquipe_UsuarioDeVariasEquipesSemInformar_EhErroDeValidacao()
    {
        var resolucao = PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Interno, [EquipeA, EquipeB]), PorEquipe(), null);

        Assert.NotNull(resolucao.ErroValidacao);
        Assert.False(resolucao.Proibido);
    }

    [Fact]
    public void ResolverEquipe_AdministradorSemInformar_EhErroDeValidacao()
    {
        Assert.NotNull(PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Administrador, []), PorEquipe(), null).ErroValidacao);
    }

    [Fact]
    public void ResolverEquipe_AdministradorInformandoQualquerEquipe_Aceita()
    {
        Assert.Equal(EquipeB, PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Administrador, []), PorEquipe(), EquipeB).EquipeId);
    }

    [Fact]
    public void ResolverEquipe_EquipeDaQualNaoParticipa_EhProibido()
    {
        Assert.True(PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Gestor, [EquipeA]), PorEquipe(), EquipeB).Proibido);
    }

    [Fact]
    public void ResolverEquipe_NaoAdministradorSemEquipeAtiva_EhProibido()
    {
        Assert.True(PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Gestor, []), PorEquipe(), null).Proibido);
    }

    [Fact]
    public void ResolverEquipe_EntidadeGlobalComEquipe_EhErroDeValidacao()
    {
        var global = new Entidade { Id = Guid.NewGuid(), GrupoId = Guid.Empty, Chave = "g", Nome = "Global", Campos = [], EscopoEquipe = false };

        Assert.NotNull(PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Administrador, []), global, EquipeA).ErroValidacao);
        Assert.Null(PoliticaPermissao.ResolverEquipe(Usuario(Perfil.Administrador, []), global, null).EquipeId);
    }

    [Fact]
    public void EquipesVisiveis_AdministradorSemRestricaoDemaisSoAsSuas()
    {
        Assert.Null(PoliticaPermissao.EquipesVisiveis(Usuario(Perfil.Administrador, [])));
        Assert.Equal([EquipeA], PoliticaPermissao.EquipesVisiveis(Usuario(Perfil.Externo, [EquipeA]))!);
    }

    [Theory]
    [InlineData(Perfil.Externo, false)]
    [InlineData(Perfil.Interno, false)]
    [InlineData(Perfil.Gestor, false)]
    [InlineData(Perfil.Administrador, true)]
    public void PodeAdministrarAcessos_SoAdministrador(Perfil perfil, bool esperado)
    {
        Assert.Equal(esperado, PoliticaPermissao.PodeAdministrarAcessos(Usuario(perfil, [EquipeA])));
    }

    private static Entidade PorEquipe() => Metadado.Entidade([]);

    [Theory]
    [InlineData(Perfil.Administrador, true)]
    [InlineData(Perfil.Gestor, true)]
    [InlineData(Perfil.Interno, false)]
    [InlineData(Perfil.Externo, false)]
    public void PodeVerPainel_SoGestorEAdministrador(Perfil perfil, bool esperado)
    {
        // RN78: o painel gerencial é do GESTOR e do ADMINISTRADOR.
        Assert.Equal(esperado, PoliticaPermissao.PodeVerPainel(Usuario(perfil, [EquipeA])));
    }

    // RN97: importar = poder criar no tipo de item, em alguma das equipes; cadastro de apoio só o ADMINISTRADOR.
    [Theory]
    [InlineData(Perfil.Administrador, false, false, true)]
    [InlineData(Perfil.Gestor, false, false, true)]
    [InlineData(Perfil.Interno, false, false, true)]
    [InlineData(Perfil.Externo, false, false, false)]
    [InlineData(Perfil.Externo, false, true, true)]       // com aprovação ativa, o EXTERNO importa (itens PENDENTES)
    [InlineData(Perfil.Administrador, true, false, true)]
    [InlineData(Perfil.Gestor, true, false, false)]
    [InlineData(Perfil.Interno, true, false, false)]
    [InlineData(Perfil.Externo, true, true, false)]
    public void PodeImportar_MesmaRegraDeCriar(Perfil perfil, bool global, bool aprovacaoAtiva, bool esperado)
    {
        var entidade = global
            ? new Entidade { Id = Guid.NewGuid(), GrupoId = Guid.Empty, Chave = "g", Nome = "Global", Campos = [], EscopoEquipe = false }
            : PorEquipe();
        IReadOnlySet<Guid> comAprovacao = aprovacaoAtiva ? new HashSet<Guid> { EquipeA } : new HashSet<Guid>();

        Assert.Equal(esperado, PoliticaPermissao.PodeImportar(Usuario(perfil, [EquipeA]), entidade, comAprovacao));
    }

    [Fact]
    public void PodeImportar_SemEquipeNaListagem_Nao() =>
        Assert.False(PoliticaPermissao.PodeImportar(Usuario(Perfil.Interno, []), PorEquipe(), new HashSet<Guid>()));

    private static UsuarioAtual Usuario(Perfil perfil, Guid[] equipes) => new()
    {
        Id = Guid.NewGuid(),
        Matricula = "m",
        Nome = "n",
        Perfil = perfil,
        Equipes = equipes.ToHashSet(),
    };
}
