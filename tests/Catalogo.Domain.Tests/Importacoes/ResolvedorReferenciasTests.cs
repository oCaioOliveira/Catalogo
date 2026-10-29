using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Importacoes;

// RN99: referência pelo nome, entre os itens que o usuário vê (a consulta já vem com o escopo) e que a linha pode referenciar.
public class ResolvedorReferenciasTests
{
    private static readonly Guid EquipeA = Guid.NewGuid();
    private static readonly Guid EquipeB = Guid.NewGuid();
    private static readonly Guid Autor = Guid.NewGuid();
    private static readonly Campo CampoProjeto = Metadado.Campo("projeto", TipoDado.Referencia, entidadeReferenciadaId: Guid.NewGuid());

    private static CandidatoReferencia Item(Guid? equipe, SituacaoAprovacao situacao = SituacaoAprovacao.Aprovado, Guid? criadoPor = null) =>
        new(Guid.NewGuid(), "Transferências Alfa", equipe, equipe is null, situacao, criadoPor);

    private static ResolucaoReferencia Resolver(Guid? equipeDaLinha, params CandidatoReferencia[] candidatos) =>
        ResolvedorReferencias.Resolver("transferencias alfa", candidatos, equipeDaLinha, Autor, CampoProjeto, "Projeto");

    [Fact]
    public void Resolver_UmItem_Encontrado()
    {
        var item = Item(EquipeA);

        Assert.Equal(item, Resolver(EquipeA, item).Item);
    }

    [Fact]
    public void Resolver_Nenhum_ErroComONomeDigitado()
    {
        var resolucao = Resolver(EquipeA);

        Assert.Null(resolucao.Item);
        Assert.Contains("'transferencias alfa'", resolucao.Erro);
    }

    [Fact]
    public void Resolver_DoisComOMesmoNome_ErroSugerindoAColunaId()
    {
        var resolucao = Resolver(EquipeA, Item(EquipeA), Item(EquipeA));

        Assert.Null(resolucao.Item);
        Assert.Contains("Use a coluna 'projeto (ID)'", resolucao.Erro);
    }

    [Fact]
    public void Resolver_ItemDeOutraEquipe_NaoServe_GlobalServe()
    {
        Assert.Null(Resolver(EquipeA, Item(EquipeB)).Item);           // RN29, mesmo que o usuário veja as duas
        Assert.NotNull(Resolver(EquipeA, Item(null)).Item);
        Assert.Null(Resolver(null, Item(EquipeA)).Item);               // item global só aponta para global
    }

    [Fact]
    public void Resolver_NaoAprovado_SoDoProprioAutor()
    {
        Assert.Null(Resolver(EquipeA, Item(EquipeA, SituacaoAprovacao.Pendente, Guid.NewGuid())).Item);   // RN38
        Assert.NotNull(Resolver(EquipeA, Item(EquipeA, SituacaoAprovacao.Pendente, Autor)).Item);
    }

    [Fact]
    public void Resolver_MesmoNomeEmDuasEquipes_ValeODaEquipeDaLinha()
    {
        var daA = Item(EquipeA);

        Assert.Equal(daA, Resolver(EquipeA, daA, Item(EquipeB)).Item);
    }

    [Fact]
    public void Resolver_MesmoNome_ADependenciaEscolhe()
    {
        var certo = Item(EquipeA);
        var errado = Item(EquipeA);

        var resolucao = ResolvedorReferencias.Resolver("transferencias alfa", [certo, errado], EquipeA, Autor, CampoProjeto, "Projeto",
            atendeDependencia: c => c == certo);

        Assert.Equal(certo, resolucao.Item);
    }

    [Fact]
    public void Resolver_NenhumAtendeADependencia_ContinuaAmbiguo()
    {
        var resolucao = ResolvedorReferencias.Resolver("transferencias alfa", [Item(EquipeA), Item(EquipeA)], EquipeA, Autor, CampoProjeto,
            "Projeto", atendeDependencia: _ => false);

        Assert.Contains("(ID)", resolucao.Erro);
    }

    [Theory]
    [InlineData(TipoDado.Texto, "Transferências Alfa", "TRANSFERENCIAS ALFA")]
    [InlineData(TipoDado.Inteiro, "007", "7")]
    [InlineData(TipoDado.Decimal, "1,50", "1.5")]
    public void ChaveDoNome_TextoSemAcentosNumeroPeloValor(TipoDado tipo, string nome, string chave) =>
        Assert.Equal(chave, ResolvedorReferencias.ChaveDoNome(Metadado.Campo("nome", tipo), nome));

    [Fact]
    public void AceitaNome_SoExibicaoDeTextoOuNumero()
    {
        Assert.True(ResolvedorReferencias.AceitaNome(Metadado.Campo("nome", TipoDado.Lista)));
        Assert.False(ResolvedorReferencias.AceitaNome(Metadado.Campo("quando", TipoDado.Data)));
        Assert.False(ResolvedorReferencias.AceitaNome(null));
    }
}
