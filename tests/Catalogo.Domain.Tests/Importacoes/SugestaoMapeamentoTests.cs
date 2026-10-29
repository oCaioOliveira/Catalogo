using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Tests.Importacoes;

// RN98: a coluna vai para o campo de mesmo nome (ou chave), sem diferenciar maiúsculas e acentos.
public class SugestaoMapeamentoTests
{
    private static readonly Guid Projetos = Guid.NewGuid();

    private static Entidade Relatorio(bool global = false) => new()
    {
        Id = Guid.NewGuid(),
        GrupoId = Guid.Empty,
        Chave = "relatorio_teste",
        Nome = "Relatório de Teste",
        EscopoEquipe = !global,
        Campos =
        [
            Campo("projeto", "Projeto", TipoDado.Referencia, Projetos),
            Campo("data_execucao", "Data de execução", TipoDado.DataHora),
            Campo("bateria_testes", "Bateria de testes", TipoDado.Texto),
            Campo("antigo", "Antigo", TipoDado.Texto, ativo: false),
        ],
    };

    private static Campo Campo(string chave, string nome, TipoDado tipo, Guid? referenciada = null, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(), EntidadeId = Guid.Empty, Chave = chave, Nome = nome, TipoDado = tipo, EntidadeReferenciadaId = referenciada,
        AcaoExclusao = referenciada is null ? null : AcaoExclusao.Restringir, Ativo = ativo,
    };

    private static IReadOnlyList<DestinoColuna> Sugerir(Entidade entidade, params string[] cabecalhos) =>
        SugestaoMapeamento.Sugerir(entidade, cabecalhos).Colunas.Select(c => c.Destino).ToList();

    [Fact]
    public void Sugerir_PeloNomeSemAcentosEMaiusculasEIgnorandoOAsterisco()
    {
        var destinos = Sugerir(Relatorio(), "PROJETO *", "data de execucao", " Bateria de Testes ");

        Assert.Equal(new DestinoColuna(TipoDestinoColuna.Campo, "projeto"), destinos[0]);
        Assert.Equal(new DestinoColuna(TipoDestinoColuna.Campo, "data_execucao"), destinos[1]);
        Assert.Equal(new DestinoColuna(TipoDestinoColuna.Campo, "bateria_testes"), destinos[2]);
    }

    [Fact]
    public void Sugerir_PelaChaveDoCampo() =>
        Assert.Equal(new DestinoColuna(TipoDestinoColuna.Campo, "data_execucao"), Sugerir(Relatorio(), "data_execucao")[0]);

    [Fact]
    public void Sugerir_ColunaId_SoEmReferencia()
    {
        var destinos = Sugerir(Relatorio(), "Projeto (ID)", "bateria_testes (id)");

        Assert.Equal(new DestinoColuna(TipoDestinoColuna.CampoId, "projeto"), destinos[0]);
        Assert.Equal(DestinoColuna.Ignorar, destinos[1]);
    }

    [Fact]
    public void Sugerir_Equipe_SoNoTipoDeItemPorEquipe()
    {
        Assert.Equal(DestinoColuna.Equipe, Sugerir(Relatorio(), "equipe")[0]);
        Assert.Equal(DestinoColuna.Ignorar, Sugerir(Relatorio(global: true), "Equipe")[0]);
    }

    [Fact]
    public void Sugerir_CampoInativoDesconhecidoERepetido_Ignorados()
    {
        var destinos = Sugerir(Relatorio(), "Antigo", "Observações", "Projeto", "projeto");

        Assert.Equal(DestinoColuna.Ignorar, destinos[0]);
        Assert.Equal(DestinoColuna.Ignorar, destinos[1]);
        Assert.Equal(TipoDestinoColuna.Campo, destinos[2].Tipo);
        Assert.Equal(DestinoColuna.Ignorar, destinos[3]);   // cada campo recebe uma coluna só
    }
}
