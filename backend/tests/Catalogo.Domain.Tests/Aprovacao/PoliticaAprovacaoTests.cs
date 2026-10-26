using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;

namespace Catalogo.Domain.Tests.Aprovacao;

// A política central estendida com a aprovação (RN33 a RN38). O usuário pertence à Equipe A e o
// registro é da Equipe A (o escopo de equipe já é coberto em PoliticaPermissaoTests).
public class PoliticaAprovacaoTests
{
    private static readonly Guid EquipeA = Guid.NewGuid();
    private static readonly Guid EquipeB = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid OutraPessoa = Guid.NewGuid();

    /// <summary>
    /// Todas as combinações perfil × operação × aprovação ativa × situação × autor (240 casos),
    /// com o resultado esperado escrito como a regra do documento (docs/regras-de-negocio.md).
    /// </summary>
    public static IEnumerable<object[]> Combinacoes() =>
        from perfil in Enum.GetValues<Perfil>()
        from operacao in Enum.GetValues<OperacaoRegistro>()
        from requer in new[] { false, true }
        from situacao in Enum.GetValues<SituacaoAprovacao>()
        from ehAutor in new[] { false, true }
        select new object[] { perfil, operacao, requer, situacao, ehAutor, Esperado(perfil, operacao, requer, situacao, ehAutor) };

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void Decidir_TodasAsCombinacoes(
        Perfil perfil, OperacaoRegistro operacao, bool requerAprovacao, SituacaoAprovacao situacao, bool ehAutor, DecisaoPermissao esperado)
    {
        var alvo = new RegistroAlvo(false, EquipeA, situacao, ehAutor ? UsuarioId : OutraPessoa, requerAprovacao);

        Assert.Equal(esperado, PoliticaPermissao.Decidir(Usuario(perfil), operacao, alvo));
    }

    // Alguns casos escritos à mão, para leitura rápida.
    [Theory]
    [InlineData(Perfil.Externo, OperacaoRegistro.Criar, true, DecisaoPermissao.Permitido)]    // RN34: com aprovação, o externo cria
    [InlineData(Perfil.Externo, OperacaoRegistro.Criar, false, DecisaoPermissao.Proibido)]    // sem aprovação, nada muda
    [InlineData(Perfil.Interno, OperacaoRegistro.Decidir, true, DecisaoPermissao.Proibido)]   // RN36: interno não decide
    [InlineData(Perfil.Gestor, OperacaoRegistro.Decidir, true, DecisaoPermissao.Permitido)]
    public void Decidir_CasosPrincipais(Perfil perfil, OperacaoRegistro operacao, bool requer, DecisaoPermissao esperado)
    {
        var alvo = new RegistroAlvo(false, EquipeA, SituacaoAprovacao.Pendente, UsuarioId, requer);

        Assert.Equal(esperado, PoliticaPermissao.Decidir(Usuario(perfil), operacao, alvo));
    }

    [Fact]
    public void Decidir_ExternoAlterandoProprioRejeitado_PodeReenviar()
    {
        // RN35
        var alvo = new RegistroAlvo(false, EquipeA, SituacaoAprovacao.Rejeitado, UsuarioId, RequerAprovacao: true);

        Assert.Equal(DecisaoPermissao.Permitido, PoliticaPermissao.Decidir(Usuario(Perfil.Externo), OperacaoRegistro.Alterar, alvo));
    }

    [Fact]
    public void Decidir_GestorDeOutraEquipe_NaoEncontra()
    {
        // RN36: só o gestor da equipe do registro decide; para os outros o registro "não existe".
        var alvo = new RegistroAlvo(false, EquipeB, SituacaoAprovacao.Pendente, OutraPessoa, true);

        Assert.Equal(DecisaoPermissao.NaoEncontrado, PoliticaPermissao.Decidir(Usuario(Perfil.Gestor), OperacaoRegistro.Decidir, alvo));
    }

    [Theory]
    [InlineData(Perfil.Administrador, true, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, false, DecisaoPermissao.Permitido)]  // qualquer equipe
    [InlineData(Perfil.Gestor, true, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Gestor, false, DecisaoPermissao.NaoEncontrado)]     // 404 para as outras
    [InlineData(Perfil.Interno, true, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Externo, true, DecisaoPermissao.Proibido)]
    public void PodeConfigurarEquipe_GestorDasSuasEAdministradorDeQualquer(Perfil perfil, bool equipeDele, DecisaoPermissao esperado)
    {
        // RN33
        Assert.Equal(esperado, PoliticaPermissao.PodeConfigurarEquipe(Usuario(perfil), equipeDele ? EquipeA : EquipeB));
    }

    [Theory]
    [InlineData(SituacaoAprovacao.Aprovado, false, true)]
    [InlineData(SituacaoAprovacao.Pendente, true, true)]     // pendente do próprio autor
    [InlineData(SituacaoAprovacao.Rejeitado, true, true)]
    [InlineData(SituacaoAprovacao.Pendente, false, false)]   // pendente de outra pessoa
    [InlineData(SituacaoAprovacao.Rejeitado, false, false)]
    public void PodeReferenciarSituacao_SoAprovadosOuDoProprioAutor(SituacaoAprovacao situacao, bool mesmoAutor, bool esperado)
    {
        // RN38
        Assert.Equal(esperado, PoliticaPermissao.PodeReferenciarSituacao(situacao, mesmoAutor ? UsuarioId : OutraPessoa, UsuarioId));
    }

    [Theory]
    [InlineData(Perfil.Externo, true)]
    [InlineData(Perfil.Interno, false)]
    [InlineData(Perfil.Gestor, false)]
    [InlineData(Perfil.Administrador, false)]
    public void NaoAprovadosSomenteDoAutor_SoRestringeOExterno(Perfil perfil, bool restringe)
    {
        // RN37
        Assert.Equal(restringe ? UsuarioId : null, PoliticaPermissao.NaoAprovadosSomenteDoAutor(Usuario(perfil)));
    }

    [Fact]
    public void EquipesDaFilaDeAprovacao_GestorSuasEquipesAdministradorTodas()
    {
        // RN36
        Assert.Equal([EquipeA], PoliticaPermissao.EquipesDaFilaDeAprovacao(Usuario(Perfil.Gestor))!);
        Assert.Null(PoliticaPermissao.EquipesDaFilaDeAprovacao(Usuario(Perfil.Administrador)));
        Assert.False(PoliticaPermissao.PodeDecidirAlgo(Usuario(Perfil.Interno)));
    }

    /// <summary>A regra, escrita como no documento, para o usuário da Equipe A e um registro da Equipe A.</summary>
    private static DecisaoPermissao Esperado(
        Perfil perfil, OperacaoRegistro operacao, bool requer, SituacaoAprovacao situacao, bool ehAutor)
    {
        // RN37: PENDENTE/REJEITADO de outra pessoa não existe para o EXTERNO (exceto na criação).
        var externo = perfil == Perfil.Externo;
        if (operacao != OperacaoRegistro.Criar && externo && situacao != SituacaoAprovacao.Aprovado && !ehAutor)
            return DecisaoPermissao.NaoEncontrado;

        return (perfil, operacao) switch
        {
            (_, OperacaoRegistro.Ver) => DecisaoPermissao.Permitido,
            // RN34: o EXTERNO cria só com aprovação ativa.
            (Perfil.Externo, OperacaoRegistro.Criar) => requer ? DecisaoPermissao.Permitido : DecisaoPermissao.Proibido,
            // RN35: altera só o que criou, enquanto PENDENTE ou REJEITADO, e só com aprovação ativa.
            (Perfil.Externo, OperacaoRegistro.Alterar) =>
                requer && ehAutor && situacao != SituacaoAprovacao.Aprovado ? DecisaoPermissao.Permitido : DecisaoPermissao.Proibido,
            (Perfil.Externo, _) => DecisaoPermissao.Proibido,
            // Os demais: a matriz da parte 1, independente de aprovação e situação.
            (Perfil.Interno, OperacaoRegistro.Criar or OperacaoRegistro.Alterar) => DecisaoPermissao.Permitido,
            (Perfil.Interno, _) => DecisaoPermissao.Proibido,
            (Perfil.Gestor or Perfil.Administrador, _) => DecisaoPermissao.Permitido,
            _ => throw new InvalidOperationException(),
        };
    }

    private static UsuarioAtual Usuario(Perfil perfil) => new()
    {
        Id = UsuarioId,
        Matricula = "m",
        Nome = "n",
        Perfil = perfil,
        Equipes = new HashSet<Guid> { EquipeA },
    };
}
