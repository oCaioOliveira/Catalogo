using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Tests.Avaliacoes;

// Validação e evolução do modelo de avaliação (RN42 e RN43).
public class RegrasModeloAvaliacaoTests
{
    private static readonly DefinicaoCriterio Qualidade = new("qualidade", "Qualidade", null, "ESTRELAS", Comentario: "OBRIGATORIO");
    private static readonly DefinicaoCriterio Observacoes = new("observacoes", "Observações", null, "TEXTO");

    [Fact]
    public void Validar_DefinicaoValida_NormalizaEAplicaPadroes()
    {
        // RN42
        var erros = new ErrosPorCampo();

        var definido = RegrasModeloAvaliacao.Validar(new DefinicaoModelo("  Avaliação A  ", "  ", [Qualidade, Observacoes]), erros);

        Assert.False(erros.PossuiErros);
        Assert.Equal("Avaliação A", definido!.Nome);
        Assert.Null(definido.Descricao);
        var qualidade = definido.Criterios[0];
        Assert.Equal((TipoCriterio.Estrelas, ExigenciaComentario.Obrigatorio, false, 1, true, 2000),
            (qualidade.Tipo, qualidade.Comentario, qualidade.Obrigatorio, qualidade.Ordem, qualidade.Ativo, qualidade.TamanhoMaximoTexto));
        Assert.Equal((TipoCriterio.Texto, ExigenciaComentario.Nenhum, 2), (definido.Criterios[1].Tipo, definido.Criterios[1].Comentario, definido.Criterios[1].Ordem));
    }

    public static IEnumerable<object[]> CriteriosInvalidos() =>
    [
        [new DefinicaoCriterio(null, "N", null, "ESTRELAS"), "criterios[0].chave", "Obrigatória"],
        [new DefinicaoCriterio("Qualidade", "N", null, "ESTRELAS"), "criterios[0].chave", "minúsculas"],
        [new DefinicaoCriterio("tempo-execucao", "N", null, "ESTRELAS"), "criterios[0].chave", "minúsculas"],
        [new DefinicaoCriterio("ação", "N", null, "ESTRELAS"), "criterios[0].chave", "minúsculas"],
        [new DefinicaoCriterio(new string('a', 101), "N", null, "ESTRELAS"), "criterios[0].chave", "no máximo 100"],
        [new DefinicaoCriterio("x", " ", null, "ESTRELAS"), "criterios[0].nome", "Obrigatório"],
        [new DefinicaoCriterio("x", new string('n', 151), null, "ESTRELAS"), "criterios[0].nome", "no máximo 150"],
        [new DefinicaoCriterio("x", "N", new string('d', 501), "ESTRELAS"), "criterios[0].descricao", "no máximo 500"],
        [new DefinicaoCriterio("x", "N", null, null), "criterios[0].tipo", "Obrigatório"],
        [new DefinicaoCriterio("x", "N", null, "NOTA"), "criterios[0].tipo", "ESTRELAS ou TEXTO"],
        [new DefinicaoCriterio("x", "N", null, "ESTRELAS", Comentario: "AS_VEZES"), "criterios[0].comentario", "NENHUM, OPCIONAL ou OBRIGATORIO"],
        [new DefinicaoCriterio("x", "N", null, "TEXTO", Comentario: "OPCIONAL"), "criterios[0].comentario", "Só critérios ESTRELAS"],
        [new DefinicaoCriterio("x", "N", null, "ESTRELAS", TamanhoMaximoTexto: 0), "criterios[0].tamanhoMaximoTexto", "entre 1 e 4000"],
        [new DefinicaoCriterio("x", "N", null, "TEXTO", TamanhoMaximoTexto: 4001), "criterios[0].tamanhoMaximoTexto", "entre 1 e 4000"],
    ];

    [Theory]
    [MemberData(nameof(CriteriosInvalidos))]
    public void Validar_CadaRegraDoCriterio(DefinicaoCriterio criterio, string chaveErro, string trecho)
    {
        // RN42
        var erros = new ErrosPorCampo();

        var definido = RegrasModeloAvaliacao.Validar(new DefinicaoModelo("Modelo", null, [criterio]), erros);

        Assert.Null(definido);
        Assert.Contains(trecho, Assert.Single(erros.ParaDicionario()[chaveErro]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4000)]
    public void Validar_TamanhoMaximoTextoNosLimites_Aceita(int tamanho)
    {
        var erros = new ErrosPorCampo();

        RegrasModeloAvaliacao.Validar(new DefinicaoModelo("M", null, [Observacoes with { TamanhoMaximoTexto = tamanho }]), erros);

        Assert.False(erros.PossuiErros);
    }

    [Fact]
    public void Validar_ChaveRepetida_Erro()
    {
        // RN42: chaves únicas no modelo.
        var erros = new ErrosPorCampo();

        RegrasModeloAvaliacao.Validar(new DefinicaoModelo("M", null, [Qualidade, Qualidade with { Nome = "Outra" }]), erros);

        Assert.Contains("mais de uma vez", Assert.Single(erros.ParaDicionario()["criterios[1].chave"]));
    }

    [Theory]
    [InlineData(false)]   // lista vazia
    [InlineData(true)]    // só critérios inativos
    public void Validar_SemCriterioAtivo_Erro(bool soInativos)
    {
        // RN42: pelo menos um critério ativo.
        var erros = new ErrosPorCampo();
        IReadOnlyList<DefinicaoCriterio?> criterios = soInativos ? [Qualidade with { Ativo = false }] : [];

        RegrasModeloAvaliacao.Validar(new DefinicaoModelo("M", null, criterios), erros);

        Assert.Contains("pelo menos um critério ativo", Assert.Single(erros.ParaDicionario()["criterios"]));
    }

    [Fact]
    public void Validar_VariosErros_VoltamTodosJuntos()
    {
        // RN05 e RN42
        var erros = new ErrosPorCampo();

        RegrasModeloAvaliacao.Validar(new DefinicaoModelo(null, new string('d', 501),
        [
            new DefinicaoCriterio("Ruim", "N", null, "TEXTO", Comentario: "OBRIGATORIO"),
            null,
            new DefinicaoCriterio("ok", null, null, "ESTRELAS", TamanhoMaximoTexto: 5000),
        ]), erros);

        Assert.Equal(
            ["criterios[0].chave", "criterios[0].comentario", "criterios[1]", "criterios[2].nome", "criterios[2].tamanhoMaximoTexto", "descricao", "nome"],
            erros.ParaDicionario().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ValidarAlteracao_SemRespostas_PodeExcluirEMudarTipo()
    {
        // RN43: critério sem respostas pode tudo.
        var modelo = ModeloComQualidadeEObservacoes();
        var novo = Definir(Observacoes with { Tipo = "ESTRELAS" });
        var erros = new ErrosPorCampo();

        RegrasModeloAvaliacao.ValidarAlteracao(modelo, novo, new HashSet<Guid>(), erros);

        Assert.False(erros.PossuiErros);
    }

    [Fact]
    public void ValidarAlteracao_CriterioComRespostas_NaoPodeSerExcluidoNemMudarDeTipo()
    {
        // RN43
        var modelo = ModeloComQualidadeEObservacoes();
        var comRespostas = modelo.Criterios.Select(c => c.Id).ToHashSet();
        var novo = Definir(Qualidade with { Tipo = "TEXTO", Comentario = "NENHUM" });   // observacoes sumiu; qualidade virou TEXTO
        var erros = new ErrosPorCampo();

        RegrasModeloAvaliacao.ValidarAlteracao(modelo, novo, comRespostas, erros);

        var dicionario = erros.ParaDicionario();
        Assert.Contains("o tipo não pode mudar", Assert.Single(dicionario["criterios[0].tipo"]));
        Assert.Contains("'observacoes' já tem respostas e não pode ser excluído", Assert.Single(dicionario["criterios"]));
    }

    [Fact]
    public void ValidarAlteracao_CriterioComRespostas_PodeMudarODemaisEAdicionarCriterios()
    {
        // RN43: nome, descrição, ordem, obrigatoriedade, comentário, tamanho, desativar e critérios novos.
        var modelo = ModeloComQualidadeEObservacoes();
        var comRespostas = modelo.Criterios.Select(c => c.Id).ToHashSet();
        var novo = Definir(
            Qualidade with { Nome = "Qualidade geral", Descricao = "Nota", Ordem = 9, Obrigatorio = true, Comentario = "OPCIONAL", TamanhoMaximoTexto = 100 },
            Observacoes with { Ativo = false },
            new DefinicaoCriterio("clareza", "Clareza", null, "ESTRELAS", Obrigatorio: true));
        var erros = new ErrosPorCampo();

        RegrasModeloAvaliacao.ValidarAlteracao(modelo, novo, comRespostas, erros);

        Assert.False(erros.PossuiErros);
    }

    [Fact]
    public void Aplicar_AtualizaIncluiERemovePelaChave()
    {
        var modelo = ModeloComQualidadeEObservacoes();
        var idQualidade = modelo.ObterCriterio("qualidade")!.Id;
        var autor = Guid.NewGuid();
        var agora = new DateTime(2026, 10, 21, 15, 0, 0, DateTimeKind.Utc);

        var removidos = RegrasModeloAvaliacao.Aplicar(modelo,
            Definir(Qualidade with { Nome = "Qualidade geral" }, new DefinicaoCriterio("clareza", "Clareza", null, "ESTRELAS")), autor, agora);

        Assert.Equal("observacoes", Assert.Single(removidos).Chave);
        Assert.Equal(["qualidade", "clareza"], modelo.Criterios.Select(c => c.Chave));
        Assert.Equal(idQualidade, modelo.ObterCriterio("qualidade")!.Id);   // o existente é o mesmo objeto
        Assert.Equal("Qualidade geral", modelo.ObterCriterio("qualidade")!.Nome);
        Assert.Equal((autor, agora), (modelo.AtualizadoPorId!.Value, modelo.AtualizadoEm!.Value));
    }

    [Fact]
    public void Criar_ModeloAtivoComOsCriterios()
    {
        var modelo = RegrasModeloAvaliacao.Criar(Definir(Qualidade, Observacoes), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.True(modelo.Ativo);
        Assert.Equal(["qualidade", "observacoes"], modelo.CriteriosAtivos.Select(c => c.Chave));
    }

    private static ModeloDefinido Definir(params DefinicaoCriterio[] criterios)
    {
        var erros = new ErrosPorCampo();
        var definido = RegrasModeloAvaliacao.Validar(new DefinicaoModelo("Modelo", null, criterios), erros);
        Assert.False(erros.PossuiErros, string.Join("; ", erros.ParaDicionario().Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));
        return definido!;
    }

    private static ModeloAvaliacao ModeloComQualidadeEObservacoes()
    {
        var modelo = RegrasModeloAvaliacao.Criar(Definir(Qualidade, Observacoes), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        modelo.Id = Guid.NewGuid();
        foreach (var criterio in modelo.Criterios)
            criterio.Id = Guid.NewGuid();
        return modelo;
    }
}
