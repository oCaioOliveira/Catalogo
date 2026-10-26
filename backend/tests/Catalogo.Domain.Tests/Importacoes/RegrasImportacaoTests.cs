using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Exclusao;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Importacoes;

// RN102 (duplicados no arquivo), RN103 (desfazer) e o resumo da prévia (RN101).
public class RegrasImportacaoTests
{
    private static readonly RestricaoUnica Restricao = Metadado.Restricao("uq_nome", Metadado.Campo("nome", TipoDado.Texto));

    private static ChaveUnicaCalculada Chave(string valor) => new(Restricao, System.Text.Encoding.UTF8.GetBytes(valor));

    [Fact]
    public void DuplicadosNoArquivo_MesmaChave_ErroNasDuasLinhas()
    {
        var repetidas = DuplicadosNoArquivo.Encontrar([(2, [Chave("A")]), (3, [Chave("B")]), (4, [Chave("A")])]);

        Assert.Equal([2, 4], repetidas.Keys.Order());
        Assert.Equal([4], repetidas[2].Outras);
        Assert.Equal("uq_nome", repetidas[4].Restricao);
    }

    [Fact]
    public void DuplicadosNoArquivo_SemChave_NaoConflita() =>
        Assert.Empty(DuplicadosNoArquivo.Encontrar([(2, []), (3, [])]));   // RN10: vazio não gera chave

    private static readonly Guid Criado1 = Guid.NewGuid();
    private static readonly Guid Criado2 = Guid.NewGuid();
    private static readonly Guid DeFora = Guid.NewGuid();

    private static PlanoExclusao Plano(params ReferenciaEntrante[] referencias)
    {
        var planejador = new PlanejadorExclusao([Criado1, Criado2]);
        for (var pendentes = planejador.ObterPendentes(); pendentes.Count > 0; pendentes = planejador.ObterPendentes())
            planejador.Registrar(pendentes, referencias.Where(r => pendentes.Contains(r.RegistroDestinoId)));
        return planejador.Concluir();
    }

    private static ReferenciaEntrante Aponta(Guid origem, Guid destino, AcaoExclusao acao) => new(origem, Guid.Empty, Guid.Empty, destino, acao);

    [Fact]
    public void Desfazer_SemAlteracoesNemReferencias_Permitido() =>
        Assert.Empty(RegrasDesfazer.Avaliar(new HashSet<Guid> { Criado1, Criado2 }, new HashSet<Guid>(), Plano()));

    [Fact]
    public void Desfazer_ReferenciaEntreItensDaPropriaImportacao_NaoImpede() =>
        Assert.Empty(RegrasDesfazer.Avaliar(new HashSet<Guid> { Criado1, Criado2 }, new HashSet<Guid>(),
            Plano(Aponta(Criado2, Criado1, AcaoExclusao.Restringir))));

    [Fact]
    public void Desfazer_ItemAlteradoDepois_Impede()
    {
        var impedimento = Assert.Single(RegrasDesfazer.Avaliar(new HashSet<Guid> { Criado1, Criado2 }, new HashSet<Guid> { Criado2 }, Plano()));

        Assert.Equal([Criado2], impedimento.Itens);
        Assert.Contains("alterados", impedimento.Motivo);
    }

    [Theory]
    [InlineData(AcaoExclusao.Restringir)]   // bloquearia a exclusão
    [InlineData(AcaoExclusao.Cascata)]      // levaria junto um item que não veio da importação
    public void Desfazer_ItemDeForaApontando_Impede(AcaoExclusao acao)
    {
        var impedimento = Assert.Single(RegrasDesfazer.Avaliar(new HashSet<Guid> { Criado1, Criado2 }, new HashSet<Guid>(),
            Plano(Aponta(DeFora, Criado1, acao))));

        Assert.Equal([DeFora], impedimento.Itens);
    }

    [Fact]
    public void PlanejadorComVariosAlvos_RestringirEntreEles_NaoBloqueia()
    {
        var plano = Plano(Aponta(Criado2, Criado1, AcaoExclusao.Restringir));

        Assert.True(plano.PodeExcluir);
        Assert.True(plano.OrdemExclusao.ToList().IndexOf(Criado2) < plano.OrdemExclusao.ToList().IndexOf(Criado1));
    }

    private static ResultadoLinha Linha(int numero, SituacaoLinha situacao, Guid? registro = null) => new(numero, situacao, [], [], registro);

    [Fact]
    public void Resumo_TudoOuNadaComErro_NaoExecutavel_SoValidas_Executavel()
    {
        IReadOnlyList<ResultadoLinha> linhas = [Linha(2, SituacaoLinha.Nova), Linha(3, SituacaoLinha.Erro)];

        Assert.False(ResumoImportacao.De(linhas, OpcoesImportacao.Padrao with { LinhasComErro = TratamentoErros.TudoOuNada }).Executavel);
        Assert.True(ResumoImportacao.De(linhas, OpcoesImportacao.Padrao).Executavel);
        Assert.False(ResumoImportacao.De([Linha(2, SituacaoLinha.Ignorada)], OpcoesImportacao.Padrao).Executavel);   // nada a gravar
    }

    [Fact]
    public void Resumo_MesmoResultado_ComparaSituacaoEItemAtualizado()
    {
        var item = Guid.NewGuid();
        var previa = ResumoImportacao.De([Linha(2, SituacaoLinha.Nova), Linha(3, SituacaoLinha.Atualizacao, item)], OpcoesImportacao.Padrao);

        Assert.True(previa.MesmoResultadoQue(
            ResumoImportacao.De([Linha(2, SituacaoLinha.Nova, Guid.NewGuid()), Linha(3, SituacaoLinha.Atualizacao, item)], OpcoesImportacao.Padrao)));
        Assert.False(previa.MesmoResultadoQue(
            ResumoImportacao.De([Linha(2, SituacaoLinha.Erro), Linha(3, SituacaoLinha.Atualizacao, item)], OpcoesImportacao.Padrao)));
        Assert.False(previa.MesmoResultadoQue(
            ResumoImportacao.De([Linha(2, SituacaoLinha.Nova), Linha(3, SituacaoLinha.Atualizacao, Guid.NewGuid())], OpcoesImportacao.Padrao)));
    }
}
