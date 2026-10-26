using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Painel;

// RN85 (dimensões), RN86 (métricas) e RN87 (ordenação e limite) da agregação configurável do painel.
public class InterpretadorIndicadoresTests
{
    private static readonly Guid RelatorioId = Guid.NewGuid();
    private static readonly Guid AmbienteId = Guid.NewGuid();

    // A entidade apontada (como relatorio_teste): é dela que vêm as dimensões "relatorio.ambiente" (1 salto, RN89).
    private static readonly Campo AmbienteDoRelatorio = Metadado.Campo("ambiente", TipoDado.Referencia, entidadeReferenciadaId: AmbienteId, entidadeId: RelatorioId);
    private static readonly Campo ExecucaoDoRelatorio = Metadado.Campo("data_execucao", TipoDado.DataHora, entidadeId: RelatorioId);
    private static readonly Campo ConclusaoDoRelatorio = Metadado.Campo("conclusao", TipoDado.TextoLongo, entidadeId: RelatorioId);
    private static readonly Campo DuracaoDoRelatorio = Metadado.Campo("duracao", TipoDado.Inteiro, entidadeId: RelatorioId);

    // A entidade apontada pelo relatório: "relatorio.ambiente.nome" chega nela em 2 saltos.
    private static readonly Campo NomeDoAmbiente = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 50, entidadeId: AmbienteId);
    private static readonly Campo CapacidadeDoAmbiente = Metadado.Campo("capacidade", TipoDado.Inteiro, entidadeId: AmbienteId);

    private readonly Campo _status = Metadado.Campo("status", TipoDado.Lista, opcoes: ["OK", "FALHA"], ordem: 1);
    private readonly Campo _bateria = Metadado.Campo("bateria", TipoDado.Texto, tamanhoMaximo: 50, ordem: 2);
    private readonly Campo _ativo = Metadado.Campo("ativo", TipoDado.Booleano, ordem: 3);
    private readonly Campo _relatorio = Metadado.Campo("relatorio", TipoDado.Referencia, entidadeReferenciadaId: RelatorioId, ordem: 4);
    private readonly Campo _data = Metadado.Campo("data", TipoDado.Data, ordem: 5);
    private readonly Campo _execucao = Metadado.Campo("execucao", TipoDado.DataHora, ordem: 6);
    private readonly Campo _usuarios = Metadado.Campo("usuarios", TipoDado.Inteiro, ordem: 7);
    private readonly Campo _vazao = Metadado.Campo("vazao", TipoDado.Decimal, precisao: 18, escala: 2, ordem: 8);
    private readonly Campo _descricao = Metadado.Campo("descricao", TipoDado.TextoLongo, ordem: 9);
    private readonly Campo _antigo = Metadado.Campo("antigo", TipoDado.Lista, opcoes: ["A"], ordem: 10, ativo: false);

    private static readonly IReadOnlyDictionary<string, string> Criterios = new Dictionary<string, string> { ["qualidade"] = "Qualidade" };

    private Entidade Entidade(bool global = false) => new()
    {
        Id = Metadado.EntidadeId,
        GrupoId = Guid.Empty,
        Chave = "cenario",
        Nome = "Cenário",
        EscopoEquipe = !global,
        Campos = [_status, _bateria, _ativo, _relatorio, _data, _execucao, _usuarios, _vazao, _descricao, _antigo],
        RestricoesUnicas = [],
        Dependencias = [],
    };

    private static Entidade Relatorio => new()
    {
        Id = RelatorioId,
        GrupoId = Guid.Empty,
        Chave = "relatorio_teste",
        Nome = "Relatório",
        Campos = [AmbienteDoRelatorio, ExecucaoDoRelatorio, ConclusaoDoRelatorio, DuracaoDoRelatorio],
        RestricoesUnicas = [],
        Dependencias = [],
    };

    private static Entidade Ambiente => new()
    {
        Id = AmbienteId,
        GrupoId = Guid.Empty,
        Chave = "ambiente",
        Nome = "Ambiente",
        EscopoEquipe = false,
        Campos = [NomeDoAmbiente, CapacidadeDoAmbiente],
        RestricoesUnicas = [],
        Dependencias = [],
    };

    private ConsultaIndicadores Interpretar(params (string Chave, string Valor)[] parametros) => InterpretarEm(Entidade(), parametros);

    private static ConsultaIndicadores InterpretarEm(Entidade entidade, params (string Chave, string Valor)[] parametros)
    {
        var grupo = new Grupo { Id = Guid.Empty, Chave = "grupo", Nome = "Grupo", Entidades = [entidade, Relatorio, Ambiente] };
        return InterpretadorIndicadores.Interpretar(
            grupo, entidade, parametros.Select(p => new KeyValuePair<string, string>(p.Chave, p.Valor)).ToList(), Criterios);
    }

    private IReadOnlyDictionary<string, string[]> Erros(params (string Chave, string Valor)[] parametros) =>
        Assert.Throws<ValidacaoRegistroException>(() => Interpretar(parametros)).Erros;

    // ---- Sem parâmetros ----

    [Fact]
    public void SemParametros_UmaLinhaComAContagem()
    {
        var consulta = Interpretar();

        Assert.Empty(consulta.Dimensoes);
        Assert.Equal(FuncaoMetrica.Contagem, Assert.Single(consulta.Metricas).Funcao);
        Assert.Null(consulta.Ordenacao);
        Assert.Equal(ConsultaIndicadores.LimitePadrao, consulta.Limite);
    }

    // ---- Dimensões (RN85) ----

    [Theory]
    [InlineData("status", "LISTA")]
    [InlineData("bateria", "TEXTO")]
    [InlineData("ativo", "BOOLEANO")]
    [InlineData("relatorio", "REFERENCIA")]
    public void Dimensao_CampoSemPeriodo_Aceita(string chave, string tipo)
    {
        var dimensao = Assert.Single(Interpretar(("agruparPor", chave)).Dimensoes);

        Assert.Equal(chave, dimensao.Chave);
        Assert.Equal(tipo, dimensao.Tipo);
        Assert.Equal(chave, dimensao.Campo!.Chave);
        Assert.Null(dimensao.Periodo);
        Assert.Equal(tipo == "TEXTO", dimensao.EhTextoLivre);
    }

    [Theory]
    [InlineData("data:dia", PeriodoAgrupamento.Dia)]
    [InlineData("data:mes", PeriodoAgrupamento.Mes)]
    [InlineData("execucao:trimestre", PeriodoAgrupamento.Trimestre)]
    [InlineData("execucao:ano", PeriodoAgrupamento.Ano)]
    public void Dimensao_DataComPeriodo_Aceita(string chave, PeriodoAgrupamento periodo)
    {
        var dimensao = Assert.Single(Interpretar(("agruparPor", chave)).Dimensoes);

        Assert.Equal(chave, dimensao.Chave);
        Assert.Equal(periodo, dimensao.Periodo);
        Assert.EndsWith($"({InterpretadorIndicadores.NomePeriodo(periodo)})", dimensao.Nome);
    }

    [Theory]
    [InlineData("equipe", DimensaoSistema.Equipe, "EQUIPE", null)]
    [InlineData("situacaoAprovacao", DimensaoSistema.SituacaoAprovacao, "SITUACAO_APROVACAO", null)]
    [InlineData("criadoEm:mes", DimensaoSistema.CriadoEm, "DATA_HORA", PeriodoAgrupamento.Mes)]
    public void Dimensao_DeSistema_Aceita(string chave, DimensaoSistema sistema, string tipo, PeriodoAgrupamento? periodo)
    {
        var dimensao = Assert.Single(Interpretar(("agruparPor", chave)).Dimensoes);

        Assert.Equal(sistema, dimensao.Sistema);
        Assert.Equal(tipo, dimensao.Tipo);
        Assert.Equal(periodo, dimensao.Periodo);
        Assert.Null(dimensao.Campo);
    }

    [Fact]
    public void Dimensao_PorReferencia_UsaOCampoDoRegistroApontado()
    {
        var consulta = Interpretar(("agruparPor", "relatorio.ambiente,relatorio.data_execucao:mes"));

        Assert.Collection(consulta.Dimensoes,
            d =>
            {
                Assert.Equal("relatorio.ambiente", d.Chave);
                Assert.Same(AmbienteDoRelatorio, d.Campo);
                Assert.Same(_relatorio, Assert.Single(d.Saltos));
                Assert.Equal("REFERENCIA", d.Tipo);
                Assert.Equal("ambiente (relatorio)", d.Nome);
            },
            d =>
            {
                Assert.Equal("relatorio.data_execucao:mes", d.Chave);
                Assert.Same(ExecucaoDoRelatorio, d.Campo);
                Assert.Equal(PeriodoAgrupamento.Mes, d.Periodo);
            });
    }

    [Fact]
    public void Dimensao_DoisSaltos_UsaOCampoDoRegistroApontadoPeloApontado()
    {
        // RN89: cenário → relatório → ambiente.
        var dimensao = Assert.Single(Interpretar(("agruparPor", "relatorio.ambiente.nome")).Dimensoes);

        Assert.Equal("relatorio.ambiente.nome", dimensao.Chave);
        Assert.Same(NomeDoAmbiente, dimensao.Campo);
        Assert.Equal([_relatorio, AmbienteDoRelatorio], dimensao.Saltos);
        Assert.Equal("TEXTO", dimensao.Tipo);
        Assert.Equal("nome (relatorio › ambiente)", dimensao.Nome);
    }

    [Fact]
    public void Dimensao_CampoDireto_NaoTemSaltos()
    {
        Assert.Empty(Assert.Single(Interpretar(("agruparPor", "status")).Dimensoes).Saltos);
    }

    [Theory]
    [InlineData("vazao", "DECIMAL")]                    // tipo sem dimensão
    [InlineData("descricao", "TEXTO_LONGO")]
    [InlineData("usuarios", "INTEIRO")]
    public void Dimensao_TipoNaoAgrupavel_Recusa(string chave, string tipo)
    {
        Assert.Contains($"é {tipo}", Assert.Single(Erros(("agruparPor", chave))["agruparPor"]));
    }

    [Theory]
    [InlineData("data", "precisa de período")]                 // DATA sem período
    [InlineData("execucao", "precisa de período")]
    [InlineData("criadoEm", "informe o período")]
    [InlineData("status:mes", "período só em dimensões de data")]
    [InlineData("equipe:mes", "período só em dimensões de data")]
    [InlineData("data:semana", "período desconhecido")]
    [InlineData("nao_existe", "campo desconhecido")]
    [InlineData("antigo", "campo inativo")]
    [InlineData("status,", "Dimensão vazia")]
    [InlineData("status,status", "mais de uma vez")]
    [InlineData("bateria.ambiente", "não é REFERENCIA")]
    [InlineData("relatorio.nao_existe", "campo desconhecido em 'Relatório'")]
    [InlineData("relatorio.conclusao", "é TEXTO_LONGO")]
    [InlineData("relatorio.ambiente.nome.x", "no máximo 2 saltos")]           // RN89: 3 saltos
    [InlineData("relatorio.ambiente.capacidade", "é INTEIRO")]                // 2 saltos: o último campo segue as regras do tipo
    [InlineData("relatorio.ambiente.nao_existe", "campo desconhecido em 'Ambiente'")]
    [InlineData("relatorio..ambiente", "trecho vazio")]
    [InlineData("antigo.nome", "campo inativo")]
    public void Dimensao_Invalida_DevolveErroEmAgruparPor(string agruparPor, string trecho)
    {
        Assert.Contains(Erros(("agruparPor", agruparPor))["agruparPor"], m => m.Contains(trecho));
    }

    [Fact]
    public void Dimensao_EquipeEmEntidadeGlobal_Recusa()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => InterpretarEm(Entidade(global: true), ("agruparPor", "equipe")));

        Assert.Contains("é global", Assert.Single(erro.Erros["agruparPor"]));
    }

    [Fact]
    public void Dimensao_MaisDeDuas_Recusa()
    {
        Assert.Contains("No máximo 2 dimensões (vieram 3)", Assert.Single(Erros(("agruparPor", "status,ativo,relatorio"))["agruparPor"]));
    }

    // ---- Métricas (RN86) ----

    [Theory]
    [InlineData("contagem", FuncaoMetrica.Contagem, null)]
    [InlineData("media:vazao", FuncaoMetrica.Media, "vazao")]
    [InlineData("soma:usuarios", FuncaoMetrica.Soma, "usuarios")]
    [InlineData("minimo:vazao", FuncaoMetrica.Minimo, "vazao")]
    [InlineData("maximo:usuarios", FuncaoMetrica.Maximo, "usuarios")]
    public void Metrica_Valida_Aceita(string chave, FuncaoMetrica funcao, string? campo)
    {
        var metrica = Assert.Single(Interpretar(("metricas", chave)).Metricas);

        Assert.Equal(chave, metrica.Chave);
        Assert.Equal(funcao, metrica.Funcao);
        Assert.Equal(campo, metrica.Campo?.Chave);
    }

    [Theory]
    [InlineData("media:relatorio.duracao", "Média de duracao (relatorio)", 1)]                 // RN89: 1 salto
    [InlineData("maximo:relatorio.ambiente.capacidade", "Máximo de capacidade (relatorio › ambiente)", 2)]
    public void Metrica_PorCaminho_UsaOCampoDoRegistroApontado(string chave, string nome, int saltos)
    {
        var metrica = Assert.Single(Interpretar(("metricas", chave)).Metricas);

        Assert.Equal(chave, metrica.Chave);
        Assert.Equal(nome, metrica.Nome);
        Assert.Equal(saltos, metrica.Saltos!.Count);
        Assert.Same(_relatorio, metrica.Saltos[0]);
    }

    [Fact]
    public void Metrica_MediaAvaliacao_AceitaCriterioDeEstrelasAtivo()
    {
        var metrica = Assert.Single(Interpretar(("metricas", "media_avaliacao:qualidade")).Metricas);

        Assert.Equal(FuncaoMetrica.MediaAvaliacao, metrica.Funcao);
        Assert.Equal("qualidade", metrica.Criterio);
        Assert.Equal("Média de avaliação: Qualidade", metrica.Nome);
    }

    [Theory]
    [InlineData("media:status", "só vale para INTEIRO e DECIMAL")]       // LISTA
    [InlineData("soma:data", "só vale para INTEIRO e DECIMAL")]
    [InlineData("maximo:relatorio", "só vale para INTEIRO e DECIMAL")]
    [InlineData("mediana:vazao", "função desconhecida")]
    [InlineData("media", "use contagem, media:campo")]
    [InlineData("media:", "use contagem, media:campo")]
    [InlineData("media:nao_existe", "campo desconhecido")]
    [InlineData("media_avaliacao:rapidez", "critério desconhecido")]
    [InlineData("contagem,contagem", "mais de uma vez")]
    [InlineData("media:relatorio.ambiente", "só vale para INTEIRO e DECIMAL")]      // RN89: caminho até uma REFERENCIA
    [InlineData("media:relatorio.nao_existe", "campo desconhecido em 'Relatório'")]
    [InlineData("media:status.duracao", "não é REFERENCIA")]
    public void Metrica_Invalida_DevolveErroEmMetricas(string metricas, string trecho)
    {
        Assert.Contains(Erros(("metricas", metricas))["metricas"], m => m.Contains(trecho));
    }

    [Fact]
    public void Metrica_MediaAvaliacaoSemModeloAtivo_ExplicaOMotivo()
    {
        var grupo = new Grupo { Id = Guid.Empty, Chave = "grupo", Nome = "Grupo", Entidades = [Entidade(), Relatorio] };
        var erro = Assert.Throws<ValidacaoRegistroException>(() => InterpretadorIndicadores.Interpretar(
            grupo, Entidade(), [new("metricas", "media_avaliacao:qualidade")], new Dictionary<string, string>()));

        Assert.Contains("não tem modelo de avaliação ativo", Assert.Single(erro.Erros["metricas"]));
    }

    [Fact]
    public void Metrica_MaisDeCinco_Recusa()
    {
        var erros = Erros(("metricas", "contagem,media:vazao,soma:vazao,minimo:vazao,maximo:vazao,media:usuarios"));

        Assert.Contains("No máximo 5 métricas (vieram 6)", Assert.Single(erros["metricas"]));
    }

    // ---- Ordenação e limite (RN87) ----

    [Theory]
    [InlineData("status", "status", false)]
    [InlineData("-media:vazao", "media:vazao", true)]
    [InlineData("-contagem", "contagem", true)]                  // contagem vale mesmo sem ser pedida
    public void Ordenacao_PorDimensaoOuMetrica_Aceita(string ordenarPor, string chave, bool descendente)
    {
        var consulta = Interpretar(("agruparPor", "status"), ("metricas", "media:vazao"), ("ordenarPor", ordenarPor));

        Assert.Equal(new OrdenacaoIndicador(chave, descendente), consulta.Ordenacao);
    }

    [Fact]
    public void Ordenacao_ChaveForaDaConsulta_RecusaListandoAsAceitas()
    {
        var mensagem = Assert.Single(Erros(("agruparPor", "status"), ("ordenarPor", "ativo"))["ordenarPor"]);

        Assert.Contains("'ativo' não é uma dimensão nem uma métrica", mensagem);
        Assert.Contains("status, contagem", mensagem);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("500", 500)]
    public void Limite_DentroDaFaixa_Aceita(string limite, int esperado)
    {
        Assert.Equal(esperado, Interpretar(("limite", limite)).Limite);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("501")]
    [InlineData("-5")]
    [InlineData("dez")]
    public void Limite_ForaDaFaixa_Recusa(string limite)
    {
        Assert.Contains("de 1 a 500", Assert.Single(Erros(("limite", limite))["limite"]));
    }

    // ---- Todos os erros juntos (RN05) ----

    [Fact]
    public void VariosErros_SaemTodosJuntos()
    {
        var erros = Erros(("agruparPor", "vazao,data"), ("metricas", "media:status,mediana:vazao"), ("ordenarPor", "x"), ("limite", "0"));

        Assert.Equal(["agruparPor", "limite", "metricas", "ordenarPor"], erros.Keys.Order(StringComparer.Ordinal).ToList());
        Assert.Equal(2, erros["agruparPor"].Length);
        Assert.Equal(2, erros["metricas"].Length);
    }

    [Fact]
    public void CaminhosInvalidos_SaemTodosJuntos()
    {
        // RN89 + RN05: cada caminho inválido é um erro, e todos vêm no mesmo 400.
        var erros = Erros(
            ("agruparPor", "relatorio.nao_existe,bateria.ambiente"),
            ("metricas", "media:relatorio.ambiente.nome.x,soma:relatorio.conclusao"));

        Assert.Equal(2, erros["agruparPor"].Length);
        Assert.Equal(2, erros["metricas"].Length);
    }

    [Fact]
    public void ParametroRepetido_Recusa()
    {
        Assert.NotEmpty(Erros(("agruparPor", "status"), ("agruparPor", "ativo"))["agruparPor"]);
    }
}
