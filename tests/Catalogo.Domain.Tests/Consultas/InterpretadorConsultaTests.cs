using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Consultas;

// RN17 (paginação), RN18 (filtros) e RN19 (ordenação).
public class InterpretadorConsultaTests
{
    private readonly Campo _nome = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 100, ordem: 1);
    private readonly Campo _descricao = Metadado.Campo("descricao", TipoDado.TextoLongo, ordem: 2);
    private readonly Campo _numero = Metadado.Campo("numero", TipoDado.Inteiro, ordem: 3);
    private readonly Campo _vazao = Metadado.Campo("vazao", TipoDado.Decimal, precisao: 18, escala: 2, ordem: 4);
    private readonly Campo _data = Metadado.Campo("data", TipoDado.Data, ordem: 5);
    private readonly Campo _execucao = Metadado.Campo("execucao", TipoDado.DataHora, ordem: 6);
    private readonly Campo _ativo = Metadado.Campo("ativo", TipoDado.Booleano, ordem: 7);
    private readonly Campo _metodo = Metadado.Campo("metodo", TipoDado.Lista, opcoes: ["GET", "POST"], ordem: 8);
    private readonly Campo _projeto = Metadado.Campo("projeto", TipoDado.Referencia, entidadeReferenciadaId: Guid.NewGuid(), ordem: 9);
    private readonly Campo _antigo = Metadado.Campo("antigo", TipoDado.Texto, tamanhoMaximo: 10, ordem: 10, ativo: false);

    private Entidade Entidade => Metadado.Entidade([_nome, _descricao, _numero, _vazao, _data, _execucao, _ativo, _metodo, _projeto, _antigo]);

    [Fact]
    public void Interpretar_SemParametros_UsaPadroes()
    {
        var consulta = Interpretar();

        Assert.Empty(consulta.Condicoes);
        Assert.Equal(new Paginacao(1, 20), consulta.Paginacao);
        Assert.Equal(Ordenacao.Padrao, consulta.Ordenacao);
        Assert.True(consulta.Ordenacao.Descendente);
        Assert.Equal(CampoSistema.CriadoEm, consulta.Ordenacao.CampoSistema);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("100", 100)]   // limite
    public void Interpretar_TamanhoDentroDoLimite_Aceita(string tamanho, int esperado)
    {
        Assert.Equal(esperado, Interpretar(("tamanho", tamanho)).Paginacao.Tamanho);
    }

    [Theory]
    [InlineData("tamanho", "101", "no máximo 100")]
    [InlineData("tamanho", "0", "maior ou igual a 1")]
    [InlineData("pagina", "0", "maior ou igual a 1")]
    [InlineData("pagina", "-1", "maior ou igual a 1")]
    [InlineData("pagina", "abc", "número inteiro")]
    public void Interpretar_PaginacaoInvalida_DevolveErroNoParametro(string parametro, string valor, string trecho)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar((parametro, valor)));

        Assert.Contains(trecho, Assert.Single(erro.Erros[parametro]));
    }

    [Fact]
    public void Interpretar_FormatoNaListagem_DevolveErroNoParametro()
    {
        // "formato" é da exportação (.../registros/exportar), não um filtro da listagem.
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("formato", "csv")));

        Assert.Contains("exportação", Assert.Single(erro.Erros["formato"]));
    }

    [Theory]
    [InlineData("agruparPor")]
    [InlineData("metricas")]
    [InlineData("ordenarPor")]
    [InlineData("limite")]
    [InlineData("de")]
    [InlineData("ate")]
    public void Interpretar_ParametroDoPainelNaListagem_DevolveErroNoParametro(string parametro)
    {
        // RN85: os parâmetros da agregação são do painel (/api/v1/painel/...), não filtros da listagem.
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar((parametro, "x")));

        Assert.Contains("painel", Assert.Single(erro.Erros[parametro]));
    }

    [Fact]
    public void Paginacao_CalculaPularETotalDePaginas()
    {
        var paginacao = new Paginacao(3, 20);

        Assert.Equal(40, paginacao.Pular);
        Assert.Equal(3, paginacao.TotalPaginas(57));
        Assert.Equal(0, paginacao.TotalPaginas(0));
    }

    [Theory]
    [InlineData("nome", "nome", null, "Ana")]                  // igual, todos os tipos
    [InlineData("nome.contem", "nome", Comparacao.Contem, "an")]
    [InlineData("descricao.contem", "descricao", Comparacao.Contem, "x")]
    [InlineData("metodo", "metodo", null, "POST")]
    public void Interpretar_FiltroDeTexto_Aceita(string parametro, string campo, Comparacao? comparacao, string valor)
    {
        var condicao = Assert.Single(Interpretar((parametro, valor)).Condicoes);

        Assert.Equal(campo, condicao.Campo.Chave);
        Assert.Equal(comparacao ?? Comparacao.Igual, condicao.Comparacao);
        Assert.Equal(valor, condicao.Texto);
    }

    [Theory]
    [InlineData("numero", Comparacao.Igual, "42", 42)]
    [InlineData("numero.de", Comparacao.MaiorOuIgual, "-5", -5)]
    [InlineData("vazao.ate", Comparacao.MenorOuIgual, "9.5", 9.5)]
    public void Interpretar_FiltroNumerico_ConverteParaNumero(string parametro, Comparacao comparacao, string valor, double esperado)
    {
        var condicao = Assert.Single(Interpretar((parametro, valor)).Condicoes);

        Assert.Equal(comparacao, condicao.Comparacao);
        Assert.Equal((decimal)esperado, condicao.Numero);
    }

    [Fact]
    public void Interpretar_FiltroDeData_UsaAMeiaNoite()
    {
        var condicao = Assert.Single(Interpretar(("data.de", "2026-10-01")).Condicoes);

        Assert.Equal(new DateTime(2026, 10, 1), condicao.Data);
        Assert.Equal(Comparacao.MaiorOuIgual, condicao.Comparacao);
    }

    [Fact]
    public void Interpretar_DataHoraComSoAData_AteIncluiODiaInteiro()
    {
        // "ate=2026-10-31" num DATA_HORA = antes de 01/11 00:00 UTC.
        var condicao = Assert.Single(Interpretar(("execucao.ate", "2026-10-31")).Condicoes);

        Assert.Equal(Comparacao.Menor, condicao.Comparacao);
        Assert.Equal(new DateTime(2026, 11, 1), condicao.Data);
    }

    [Fact]
    public void Interpretar_DataHoraIgualComSoAData_ViraIntervaloDoDia()
    {
        var condicoes = Interpretar(("execucao", "2026-10-21")).Condicoes;

        Assert.Equal(2, condicoes.Count);
        Assert.Contains(condicoes, c => c.Comparacao == Comparacao.MaiorOuIgual && c.Data == new DateTime(2026, 10, 21));
        Assert.Contains(condicoes, c => c.Comparacao == Comparacao.Menor && c.Data == new DateTime(2026, 10, 22));
    }

    [Fact]
    public void Interpretar_DataHoraComFuso_ConverteParaUtc()
    {
        var condicao = Assert.Single(Interpretar(("execucao.de", "2026-10-21T14:30:00-03:00")).Condicoes);

        Assert.Equal(new DateTime(2026, 10, 21, 17, 30, 0), condicao.Data);
    }

    [Fact]
    public void Interpretar_FiltrosDeBooleanoEReferencia_ConvertemOValor()
    {
        var projeto = Guid.NewGuid();

        var condicoes = Interpretar(("ativo", "true"), ("projeto", projeto.ToString())).Condicoes;

        Assert.Equal(true, condicoes.Single(c => c.Campo == _ativo).Booleano);
        Assert.Equal(projeto, condicoes.Single(c => c.Campo == _projeto).Referencia);
    }

    [Fact]
    public void Interpretar_VariosFiltros_CombinamComE()
    {
        var consulta = Interpretar(("nome.contem", "a"), ("numero.de", "1"), ("numero.ate", "9"));

        Assert.Equal(3, consulta.Condicoes.Count);
    }

    [Theory]
    [InlineData("numero.contem", "1")]      // contem só em texto
    [InlineData("nome.de", "a")]             // de/ate só em números e datas
    [InlineData("metodo.contem", "PO")]      // LISTA só aceita igual
    [InlineData("projeto.de", "x")]
    [InlineData("ativo.ate", "true")]
    public void Interpretar_OperadorInvalidoParaOTipo_DevolveErroComOsOperadoresAceitos(string parametro, string valor)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar((parametro, valor)));

        Assert.Contains("não é aceito", Assert.Single(erro.Erros[parametro]));
    }

    [Fact]
    public void Interpretar_OperadorDesconhecido_DevolveErro()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("nome.parecido", "a")));

        Assert.Contains("desconhecido", Assert.Single(erro.Erros["nome.parecido"]));
    }

    [Theory]
    [InlineData("numero", "1.5")]
    [InlineData("numero", "abc")]
    [InlineData("vazao.de", "1,5")]          // vírgula não: cultura invariante
    [InlineData("data", "21/10/2026")]
    [InlineData("execucao.de", "2026-10-21T14:30")]   // data e hora sem fuso
    [InlineData("ativo", "sim")]
    [InlineData("metodo", "PUT")]            // fora das opções
    [InlineData("projeto", "nao-e-guid")]
    [InlineData("nome", "   ")]              // vazio
    public void Interpretar_ValorQueNaoConverteParaOTipo_DevolveErro(string parametro, string valor)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar((parametro, valor)));

        Assert.True(erro.Erros.ContainsKey(parametro));
    }

    [Theory]
    [InlineData("nao_existe", "campo desconhecido")]
    [InlineData("antigo", "campo inativo")]
    public void Interpretar_ChaveDesconhecidaOuInativa_DevolveErro(string chave, string trecho)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar((chave, "x")));

        Assert.Contains(trecho, Assert.Single(erro.Erros[chave]));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("versao")]
    [InlineData("criadoEm.de")]
    [InlineData("criadoPor")]
    public void Interpretar_ChaveReservadaComoFiltro_DevolveErro(string parametro)
    {
        // RN14
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar((parametro, "2026-01-01")));

        Assert.Contains("reservada", Assert.Single(erro.Erros[parametro]));
    }

    [Fact]
    public void Interpretar_VariosProblemas_DevolveTodosOsErrosJuntos()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(
            ("tamanho", "500"), ("nao_existe", "1"), ("numero.contem", "1"), ("data", "ontem"), ("ordenacao", "descricao")));

        Assert.Equal(["data", "nao_existe", "numero.contem", "ordenacao", "tamanho"], erro.Erros.Keys.Order());
    }

    [Theory]
    [InlineData("numero", "numero", false)]
    [InlineData("-vazao", "vazao", true)]
    [InlineData("execucao", "execucao", false)]
    public void Interpretar_OrdenacaoPorCampo_RespeitaOSinal(string valor, string campo, bool descendente)
    {
        var ordenacao = Interpretar(("ordenacao", valor)).Ordenacao;

        Assert.Equal(campo, ordenacao.Campo!.Chave);
        Assert.Equal(descendente, ordenacao.Descendente);
    }

    [Theory]
    [InlineData("atualizadoEm", CampoSistema.AtualizadoEm, false)]
    [InlineData("-criadoEm", CampoSistema.CriadoEm, true)]
    public void Interpretar_OrdenacaoPorColunaDeSistema_Aceita(string valor, CampoSistema campo, bool descendente)
    {
        var ordenacao = Interpretar(("ordenacao", valor)).Ordenacao;

        Assert.Equal(campo, ordenacao.CampoSistema);
        Assert.Equal(descendente, ordenacao.Descendente);
    }

    [Fact]
    public void Interpretar_OrdenacaoPorTextoLongo_DevolveErro()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("ordenacao", "descricao")));

        Assert.Contains("TEXTO_LONGO", Assert.Single(erro.Erros["ordenacao"]));
    }

    [Theory]
    [InlineData("url", false)]
    [InlineData("-url", true)]
    public void Interpretar_OrdenacaoPorUrl_Aceita(string valor, bool descendente)
    {
        // ADR 0042: diferente do TEXTO_LONGO, a URL pode ser coluna e ordenar.
        var url = Metadado.Campo("url", TipoDado.Url);
        var entidade = Metadado.Entidade([_nome, url]);

        var ordenacao = InterpretadorConsulta.Interpretar(entidade, [KeyValuePair.Create("ordenacao", valor)]).Ordenacao;

        Assert.Same(url, ordenacao.Campo);
        Assert.Equal(descendente, ordenacao.Descendente);
    }

    [Theory]
    [InlineData("url=https://exemplo.com.br/a", Comparacao.Igual)]
    [InlineData("url.contem=exemplo", Comparacao.Contem)]
    public void Interpretar_FiltroDeUrl_IgualOuContem(string parametro, Comparacao esperada)
    {
        var url = Metadado.Campo("url", TipoDado.Url);
        var (chave, valor) = (parametro[..parametro.IndexOf('=')], parametro[(parametro.IndexOf('=') + 1)..]);

        var condicao = Assert.Single(InterpretadorConsulta.Interpretar(Metadado.Entidade([url]), [KeyValuePair.Create(chave, valor)]).Condicoes);

        Assert.Equal(esperada, condicao.Comparacao);
    }

    [Fact]
    public void Interpretar_FiltroDeUrlComIntervalo_DevolveErro()
    {
        var url = Metadado.Campo("url", TipoDado.Url);

        var erro = Assert.Throws<ValidacaoRegistroException>(() =>
            InterpretadorConsulta.Interpretar(Metadado.Entidade([url]), [KeyValuePair.Create("url.de", "a")]));

        Assert.Contains("URL", Assert.Single(erro.Erros["url.de"]));
    }

    // ---- Ordenação por REFERENCIA (RN19): pelo campo de exibição do registro apontado ----

    private static readonly Guid ProjetoId = Guid.NewGuid();
    private static readonly Campo NomeDoProjeto = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 150, entidadeId: ProjetoId);

    private ConsultaRegistros InterpretarComProjeto(Guid? campoExibicao, params (string Chave, string Valor)[] parametros)
    {
        var projeto = Metadado.Campo("projeto", TipoDado.Referencia, entidadeReferenciadaId: ProjetoId, ordem: 9);
        var entidade = Metadado.Entidade([_nome, projeto]);
        var apontada = new Entidade
        {
            Id = ProjetoId, GrupoId = Guid.Empty, Chave = "projeto", Nome = "Projeto", Campos = [NomeDoProjeto], CampoExibicaoId = campoExibicao,
        };
        var grupo = new Grupo { Id = Guid.Empty, Chave = "grupo", Nome = "Grupo", Entidades = [entidade, apontada] };
        return InterpretadorConsulta.Interpretar(entidade, parametros.Select(p => KeyValuePair.Create(p.Chave, p.Valor)), grupo: grupo);
    }

    [Theory]
    [InlineData("projeto", false)]
    [InlineData("-projeto", true)]
    public void Interpretar_OrdenacaoPorReferencia_UsaOCampoDeExibicaoDaApontada(string valor, bool descendente)
    {
        var ordenacao = InterpretarComProjeto(NomeDoProjeto.Id, ("ordenacao", valor)).Ordenacao;

        Assert.Equal("projeto", ordenacao.Campo!.Chave);
        Assert.Same(NomeDoProjeto, ordenacao.CampoExibicaoReferenciada);
        Assert.Equal(descendente, ordenacao.Descendente);
    }

    [Fact]
    public void Interpretar_OrdenacaoPorReferenciaSemCampoDeExibicao_DevolveErro()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => InterpretarComProjeto(null, ("ordenacao", "projeto")));

        Assert.Contains("não tem campo de exibição", Assert.Single(erro.Erros["ordenacao"]));
    }

    [Fact]
    public void Interpretar_OrdenacaoPorReferenciaSemGrupo_DevolveErro()
    {
        // Sem o grupo não há como achar a entidade apontada: o mesmo 400 de antes.
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("ordenacao", "-projeto")));

        Assert.Contains("não tem campo de exibição", Assert.Single(erro.Erros["ordenacao"]));
    }

    [Fact]
    public void Interpretar_FiltroEquipeId_ViraFiltroDeEquipeENaoCondicao()
    {
        // RN27
        var equipe = Guid.NewGuid();

        var consulta = Interpretar(("equipeId", equipe.ToString()));

        Assert.Equal(equipe, consulta.FiltroEquipe);
        Assert.Empty(consulta.Condicoes);
        Assert.Null(consulta.EquipesPermitidas);   // escopo de segurança nunca vem da URL
    }

    [Fact]
    public void Interpretar_FiltroEquipeIdInvalidoOuEmEntidadeGlobal_DevolveErro()
    {
        Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("equipeId", "x")));

        var global = new Entidade
        {
            Id = Guid.NewGuid(), GrupoId = Guid.Empty, Chave = "g", Nome = "Global", Campos = [], EscopoEquipe = false,
        };
        var erro = Assert.Throws<ValidacaoRegistroException>(() =>
            InterpretadorConsulta.Interpretar(global, [KeyValuePair.Create("equipeId", Guid.NewGuid().ToString())]));
        Assert.Contains("global", erro.Erros["equipeId"][0]);
    }

    [Theory]
    [InlineData("PENDENTE", SituacaoAprovacao.Pendente)]
    [InlineData("rejeitado", SituacaoAprovacao.Rejeitado)]
    public void Interpretar_FiltroSituacaoAprovacao_ViraFiltroDeSituacao(string valor, SituacaoAprovacao esperado)
    {
        // RN37
        var consulta = Interpretar(("situacaoAprovacao", valor));

        Assert.Equal(esperado, consulta.FiltroSituacao);
        Assert.Empty(consulta.Condicoes);
        Assert.Null(consulta.NaoAprovadosSomenteDoAutor);   // escopo de segurança nunca vem da URL
    }

    [Fact]
    public void Interpretar_FiltroSituacaoAprovacaoInvalido_DevolveErro()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("situacaoAprovacao", "ANALISE")));

        Assert.True(erro.Erros.ContainsKey("situacaoAprovacao"));
    }

    [Fact]
    public void Interpretar_ParametroDePaginacaoRepetido_DevolveErro()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("pagina", "1"), ("pagina", "2")));

        Assert.Contains("uma única vez", erro.Erros["pagina"][0]);
    }

    [Fact]
    public void Interpretar_ValorComCaracteresDeSql_EhApenasTexto()
    {
        // O valor nunca vira SQL: é só o texto que a Infrastructure passa como parâmetro.
        var condicao = Assert.Single(Interpretar(("nome", "' OR 1=1 --")).Condicoes);

        Assert.Equal("' OR 1=1 --", condicao.Texto);
    }

    // ---- RN48: ordenação e filtro pela média de um critério de avaliação ----

    private static readonly IReadOnlySet<string> Criterios = new HashSet<string> { "qualidade", "clareza" };

    [Theory]
    [InlineData("avaliacao.qualidade", false)]
    [InlineData("-avaliacao.qualidade", true)]
    public void Interpretar_OrdenacaoPorAvaliacao_ViraOrdenacaoPeloCriterio(string ordenacao, bool descendente)
    {
        // RN48
        var consulta = InterpretarComCriterios(("ordenacao", ordenacao));

        Assert.Equal(new Ordenacao(null, null, descendente, "qualidade"), consulta.Ordenacao);
    }

    [Theory]
    [InlineData("4", Comparacao.MaiorOuIgual, "3.95")]     // exibida >= 4.0  ⇔  média >= 3.95
    [InlineData("3.5", Comparacao.MaiorOuIgual, "3.45")]
    [InlineData("3.55", Comparacao.MaiorOuIgual, "3.55")]  // exibida >= 3.55 ⇔ exibida >= 3.6 ⇔ média >= 3.55
    public void Interpretar_FiltroAvaliacaoDe_LimiteSobreAMediaExibida(string valor, Comparacao comparacao, string limite)
    {
        // RN48: o filtro vale para a média como a API mostra (uma casa decimal).
        var condicao = Assert.Single(InterpretarComCriterios(("avaliacao.qualidade.de", valor)).CondicoesAvaliacao!);

        Assert.Equal(new CondicaoAvaliacao("qualidade", comparacao, decimal.Parse(limite, System.Globalization.CultureInfo.InvariantCulture)), condicao);
        Assert.Empty(InterpretarComCriterios(("avaliacao.qualidade.de", valor)).Condicoes);
    }

    [Fact]
    public void Interpretar_FiltroAvaliacaoAte_ViraMenorQueOLimite()
    {
        // exibida <= 3  ⇔  média < 3.05
        var condicao = Assert.Single(InterpretarComCriterios(("avaliacao.clareza.ate", "3")).CondicoesAvaliacao!);

        Assert.Equal(new CondicaoAvaliacao("clareza", Comparacao.Menor, 3.05m), condicao);
    }

    [Theory]
    [InlineData("avaliacao.inexistente.de", "4", "desconhecido")]
    [InlineData("avaliacao.qualidade", "4", "avaliacao.qualidade.de")]   // igualdade não existe
    [InlineData("avaliacao.qualidade.contem", "4", "avaliacao.qualidade.de")]
    [InlineData("avaliacao.qualidade.de", "6", "de 1 a 5")]
    [InlineData("avaliacao.qualidade.de", "quatro", "de 1 a 5")]
    public void Interpretar_FiltroAvaliacaoInvalido_DevolveErroNoParametro(string parametro, string valor, string trecho)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => InterpretarComCriterios((parametro, valor)));

        Assert.Contains(trecho, Assert.Single(erro.Erros[parametro]));
    }

    [Fact]
    public void Interpretar_AvaliacaoSemModeloAtivo_ExplicaQueNaoHaCriterios()
    {
        // Sem modelo ativo (lista vazia), "avaliacao.x" é erro tanto na ordenação quanto no filtro.
        var erro = Assert.Throws<ValidacaoRegistroException>(() =>
            Interpretar(("ordenacao", "-avaliacao.qualidade"), ("avaliacao.qualidade.de", "4")));

        Assert.Contains("não tem modelo de avaliação ativo", erro.Erros["ordenacao"][0]);
        Assert.Contains("não tem modelo de avaliação ativo", erro.Erros["avaliacao.qualidade.de"][0]);
    }

    // ---- Filtros por caminho de referência (RN89) ----

    // Grupo: esta entidade → "relatorio" → "ambiente" (global). O mesmo intérprete de caminhos do painel (CaminhoCampo).
    private static readonly Guid RelatorioId = Guid.NewGuid();
    private static readonly Guid AmbienteId = Guid.NewGuid();
    private static readonly Campo NomeDoAmbiente = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 50, entidadeId: AmbienteId);
    private static readonly Campo AmbienteDoRelatorio = Metadado.Campo("ambiente", TipoDado.Referencia, entidadeReferenciadaId: AmbienteId, entidadeId: RelatorioId);
    private static readonly Campo TipoDoRelatorio = Metadado.Campo("tipo_teste", TipoDado.Texto, tamanhoMaximo: 50, entidadeId: RelatorioId);
    private static readonly Campo ExecucaoDoRelatorio = Metadado.Campo("data_execucao", TipoDado.DataHora, entidadeId: RelatorioId);
    private readonly Campo _relatorio = Metadado.Campo("relatorio", TipoDado.Referencia, entidadeReferenciadaId: RelatorioId, ordem: 11);

    private ConsultaRegistros InterpretarComCaminhos(params (string Chave, string Valor)[] parametros)
    {
        var entidade = Metadado.Entidade([_nome, _numero, _projeto, _antigo, _relatorio]);
        var relatorio = new Entidade
        {
            Id = RelatorioId, GrupoId = Guid.Empty, Chave = "relatorio", Nome = "Relatório",
            Campos = [AmbienteDoRelatorio, TipoDoRelatorio, ExecucaoDoRelatorio],
        };
        var ambiente = new Entidade { Id = AmbienteId, GrupoId = Guid.Empty, Chave = "ambiente", Nome = "Ambiente", EscopoEquipe = false, Campos = [NomeDoAmbiente] };
        var grupo = new Grupo { Id = Guid.Empty, Chave = "grupo", Nome = "Grupo", Entidades = [entidade, relatorio, ambiente] };
        return InterpretadorConsulta.Interpretar(entidade, parametros.Select(p => KeyValuePair.Create(p.Chave, p.Valor)), grupo: grupo);
    }

    [Fact]
    public void Interpretar_FiltroPorCaminhoDeUmSalto_IgualdadeNoCampoDoRegistroApontado()
    {
        var condicao = Assert.Single(InterpretarComCaminhos(("relatorio.tipo_teste", "Carga")).Condicoes);

        Assert.Same(TipoDoRelatorio, condicao.Campo);
        Assert.Same(_relatorio, Assert.Single(condicao.Saltos!));
        Assert.Equal(Comparacao.Igual, condicao.Comparacao);
        Assert.Equal("Carga", condicao.Texto);
    }

    [Fact]
    public void Interpretar_FiltroPorCaminhoComOperador_SeparaOOperadorDoUltimoTrecho()
    {
        var condicoes = InterpretarComCaminhos(("relatorio.data_execucao.de", "2026-01-01"), ("relatorio.tipo_teste.contem", "carga")).Condicoes;

        Assert.Equal(Comparacao.MaiorOuIgual, condicoes[0].Comparacao);
        Assert.Equal(new DateTime(2026, 1, 1), condicoes[0].Data);
        Assert.Same(ExecucaoDoRelatorio, condicoes[0].Campo);
        Assert.Equal(Comparacao.Contem, condicoes[1].Comparacao);
        Assert.All(condicoes, c => Assert.Same(_relatorio, Assert.Single(c.Saltos!)));
    }

    [Fact]
    public void Interpretar_FiltroPorCaminhoDeDoisSaltos_GuardaOsDoisSaltos()
    {
        var condicao = Assert.Single(InterpretarComCaminhos(("relatorio.ambiente.nome.contem", "homolog")).Condicoes);

        Assert.Same(NomeDoAmbiente, condicao.Campo);
        Assert.Equal([_relatorio, AmbienteDoRelatorio], condicao.Saltos);
    }

    [Fact]
    public void Interpretar_FiltroDireto_NaoTemSaltos()
    {
        Assert.Null(Assert.Single(InterpretarComCaminhos(("nome", "a")).Condicoes).Saltos);
    }

    [Theory]
    [InlineData("relatorio.nao_existe", "campo desconhecido em 'Relatório'")]
    [InlineData("relatorio.nao_existe.de", "campo desconhecido em 'Relatório'")]
    [InlineData("relatorio.ambiente.nome.x.de", "no máximo 2 saltos")]
    [InlineData("nome.tipo_teste", "Operador 'tipo_teste' desconhecido")]     // "nome" é TEXTO: o resto vira operador
    [InlineData("numero.tipo_teste.de", "não é REFERENCIA")]
    [InlineData("relatorio.tipo_teste.de", "não é aceito para campos TEXTO")]   // o operador segue o tipo do último campo
    [InlineData("antigo.nome", "campo inativo")]
    [InlineData("projeto.nome", "não está disponível")]                         // a entidade apontada não está no grupo
    [InlineData("projeto.contem", "não é aceito para campos REFERENCIA")]
    public void Interpretar_FiltroPorCaminhoInvalido_DevolveErroNoParametro(string parametro, string trecho)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => InterpretarComCaminhos((parametro, "2026-01-01")));

        Assert.Contains(trecho, Assert.Single(erro.Erros[parametro]));
    }

    [Fact]
    public void Interpretar_VariosCaminhosInvalidos_SaemTodosJuntos()
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() =>
            InterpretarComCaminhos(("relatorio.nao_existe", "x"), ("relatorio.ambiente.nome.x.y", "x"), ("relatorio.data_execucao.de", "ontem")));

        Assert.Equal(3, erro.Erros.Count);
    }

    [Fact]
    public void Interpretar_CaminhoSemGrupo_NaoAtravessaReferencias()
    {
        // Sem o grupo, não há como chegar à entidade apontada.
        var erro = Assert.Throws<ValidacaoRegistroException>(() => Interpretar(("projeto.nome", "x")));

        Assert.Contains("não está disponível", Assert.Single(erro.Erros["projeto.nome"]));
    }

    private ConsultaRegistros InterpretarComCriterios(params (string Chave, string Valor)[] parametros) =>
        InterpretadorConsulta.Interpretar(Entidade, parametros.Select(p => KeyValuePair.Create(p.Chave, p.Valor)), Criterios);

    private ConsultaRegistros Interpretar(params (string Chave, string Valor)[] parametros) =>
        InterpretadorConsulta.Interpretar(Entidade, parametros.Select(p => KeyValuePair.Create(p.Chave, p.Valor)));
}
