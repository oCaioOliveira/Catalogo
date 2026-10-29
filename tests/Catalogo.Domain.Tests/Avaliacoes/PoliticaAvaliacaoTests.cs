using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;

namespace Catalogo.Domain.Tests.Avaliacoes;

/// <summary>Resultado final de "avaliar um item", juntando a política (quem) e a regra da situação (o quê).</summary>
public enum ResultadoAvaliar
{
    Permitido,
    NaoEncontrado,   // 404
    Proibido,        // 403
    NaoAprovado,     // 409
}

// Quem configura e quem avalia (RN40 e RN44): perfil × equipe × situação de aprovação × aprovação ativa × autor.
public class PoliticaAvaliacaoTests
{
    private static readonly Guid EquipeA = Guid.NewGuid();
    private static readonly Guid EquipeB = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid OutraPessoa = Guid.NewGuid();

    /// <summary>4 perfis × 2 equipes × 3 situações × aprovação ativa ou não × autor ou não = 96 casos.</summary>
    public static IEnumerable<object[]> Combinacoes() =>
        from perfil in Enum.GetValues<Perfil>()
        from daSuaEquipe in new[] { true, false }
        from situacao in Enum.GetValues<SituacaoAprovacao>()
        from requerAprovacao in new[] { false, true }
        from ehAutor in new[] { false, true }
        select new object[] { perfil, daSuaEquipe, situacao, requerAprovacao, ehAutor, Esperado(perfil, daSuaEquipe, situacao, ehAutor) };

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void Avaliar_TodasAsCombinacoes(
        Perfil perfil, bool daSuaEquipe, SituacaoAprovacao situacao, bool requerAprovacao, bool ehAutor, ResultadoAvaliar esperado)
    {
        var alvo = new RegistroAlvo(false, daSuaEquipe ? EquipeA : EquipeB, situacao, ehAutor ? UsuarioId : OutraPessoa, requerAprovacao);

        // O caso de uso aplica nesta ordem: política (404/403) e depois a situação (409).
        var decisao = PoliticaPermissao.PodeAvaliar(Usuario(perfil), alvo);
        var resultado = decisao switch
        {
            DecisaoPermissao.NaoEncontrado => ResultadoAvaliar.NaoEncontrado,
            DecisaoPermissao.Proibido => ResultadoAvaliar.Proibido,
            _ => RegrasAvaliacao.PodeSerAvaliado(situacao) ? ResultadoAvaliar.Permitido : ResultadoAvaliar.NaoAprovado,
        };

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData(Perfil.Administrador, true, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Administrador, false, DecisaoPermissao.Permitido)]   // qualquer equipe
    [InlineData(Perfil.Gestor, true, DecisaoPermissao.Permitido)]
    [InlineData(Perfil.Gestor, false, DecisaoPermissao.NaoEncontrado)]      // 404 para as outras
    [InlineData(Perfil.Interno, true, DecisaoPermissao.Proibido)]
    [InlineData(Perfil.Externo, true, DecisaoPermissao.Proibido)]
    public void ConfigurarModelo_GestorDasSuasEquipesEAdministradorDeQualquer(Perfil perfil, bool equipeDele, DecisaoPermissao esperado)
    {
        // RN40: a mesma regra da configuração de aprovação.
        Assert.Equal(esperado, PoliticaPermissao.PodeConfigurarEquipe(Usuario(perfil), equipeDele ? EquipeA : EquipeB));
    }

    [Theory]
    [InlineData(Perfil.Externo, false)]
    [InlineData(Perfil.Interno, true)]
    [InlineData(Perfil.Gestor, true)]
    [InlineData(Perfil.Administrador, true)]
    public void AvaliacoesIndividuais_SoQuemNaoEExterno(Perfil perfil, bool esperado)
    {
        // RN47
        Assert.Equal(esperado, PoliticaPermissao.PodeVerAvaliacoesIndividuais(Usuario(perfil)));
    }

    /// <summary>A regra, escrita como no documento (RN44), para um usuário da Equipe A.</summary>
    private static ResultadoAvaliar Esperado(Perfil perfil, bool daSuaEquipe, SituacaoAprovacao situacao, bool ehAutor)
    {
        // Precisa VER o item: equipe (o administrador vê todas) e situação (o EXTERNO não vê não aprovados de outros).
        var veEquipe = daSuaEquipe || perfil == Perfil.Administrador;
        var veSituacao = situacao == SituacaoAprovacao.Aprovado || perfil != Perfil.Externo || ehAutor;
        if (!veEquipe || !veSituacao)
            return ResultadoAvaliar.NaoEncontrado;

        // O EXTERNO nunca avalia.
        if (perfil == Perfil.Externo)
            return ResultadoAvaliar.Proibido;

        // Só itens APROVADOS.
        return situacao == SituacaoAprovacao.Aprovado ? ResultadoAvaliar.Permitido : ResultadoAvaliar.NaoAprovado;
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
