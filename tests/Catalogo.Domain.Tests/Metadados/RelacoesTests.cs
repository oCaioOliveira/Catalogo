using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Metadados;

/// <summary>RN94: as relações de um item são descobertas pelos metadados (ADR 0047).</summary>
public class RelacoesTests
{
    private static readonly Guid ProjetoId = Guid.NewGuid();

    private static Entidade Entidade(string chave, int ordem, bool ativo = true, params Campo[] campos) => new()
    {
        Id = chave == "projeto" ? ProjetoId : Guid.NewGuid(),
        GrupoId = Guid.Empty,
        Chave = chave,
        Nome = chave,
        Ordem = ordem,
        Ativo = ativo,
        Campos = campos,
    };

    private static Campo Ref(string chave, Guid alvo, int ordem = 0, bool ativo = true, bool obrigatorio = false,
        AcaoExclusao acao = AcaoExclusao.Restringir) => new()
    {
        Id = Guid.NewGuid(),
        EntidadeId = Guid.NewGuid(),
        Chave = chave,
        Nome = chave,
        TipoDado = TipoDado.Referencia,
        Obrigatorio = obrigatorio,
        EntidadeReferenciadaId = alvo,
        AcaoExclusao = acao,
        Ordem = ordem,
        Ativo = ativo,
    };

    private static Grupo Grupo(params Entidade[] entidades) =>
        new() { Id = Guid.NewGuid(), Chave = "g", Nome = "G", Entidades = entidades };

    [Fact]
    public void UmaRelacaoPorCampoQueAponta_NaOrdemDasEntidadesEDosCampos()
    {
        var projeto = Entidade("projeto", 2, true, Metadado.Campo("nome", TipoDado.Texto));
        var endpoint = Entidade("endpoint", 3, true, Ref("projeto", ProjetoId));
        var relatorio = Entidade("relatorio", 1, true,
            Ref("projeto_destino", ProjetoId, ordem: 2), Ref("projeto", ProjetoId, ordem: 1), Metadado.Campo("bateria", TipoDado.Texto));

        var relacoes = Relacoes.QueApontamPara(Grupo(endpoint, projeto, relatorio), projeto);

        Assert.Equal(["relatorio.projeto", "relatorio.projeto_destino", "endpoint.projeto"],
            relacoes.Select(r => $"{r.Origem.Chave}.{r.Campo.Chave}"));
    }

    [Fact]
    public void CampoOuEntidadeInativos_FicamFora()
    {
        var projeto = Entidade("projeto", 1, true);
        var inativa = Entidade("antiga", 2, false, Ref("projeto", ProjetoId));
        var comCampoInativo = Entidade("endpoint", 3, true, Ref("projeto", ProjetoId, ativo: false));

        Assert.Empty(Relacoes.QueApontamPara(Grupo(projeto, inativa, comCampoInativo), projeto));
    }

    [Theory]
    [InlineData(true, AcaoExclusao.Cascata, true)]
    [InlineData(false, AcaoExclusao.Cascata, false)]
    [InlineData(true, AcaoExclusao.Restringir, false)]
    public void Filha_SoComCampoObrigatorioECascata(bool obrigatorio, AcaoExclusao acao, bool filha)
    {
        var projeto = Entidade("projeto", 1, true);
        var cenario = Entidade("cenario", 2, true, Ref("projeto", ProjetoId, obrigatorio: obrigatorio, acao: acao));

        Assert.Equal(filha, Relacoes.QueApontamPara(Grupo(projeto, cenario), projeto).Single().Filha);
    }
}
