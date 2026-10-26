using System.Text.Json.Nodes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.Tests.CasosDeUso;

// Avaliação de itens nos casos de uso (RN40 a RN48). Modelo de "relatorio_teste" na Equipe A:
// "qualidade" (ESTRELAS, comentário OBRIGATORIO) e "observacoes" (TEXTO, opcional).
public class AvaliacaoItensTests
{
    private const string Entidade = "relatorio_teste";

    private readonly AmbienteDeTeste _ambiente = new();
    private readonly UsuarioAtual _gestorA;
    private readonly UsuarioAtual _gestorB;
    private readonly UsuarioAtual _internoA;
    private readonly UsuarioAtual _outroInternoA;
    private readonly UsuarioAtual _terceiroInternoA;
    private readonly UsuarioAtual _externoA;

    public AvaliacaoItensTests()
    {
        var equipeA = _ambiente.EquipeA.Id;
        _gestorA = Usuario(Perfil.Gestor, "gestor.a", equipeA);
        _gestorB = Usuario(Perfil.Gestor, "gestor.b", _ambiente.EquipeB.Id);
        _internoA = Usuario(Perfil.Interno, "interno.a", equipeA);
        _outroInternoA = Usuario(Perfil.Interno, "interno2.a", equipeA);
        _terceiroInternoA = Usuario(Perfil.Interno, "interno3.a", equipeA);
        _externoA = Usuario(Perfil.Externo, "externo.a", equipeA);
    }

    // ---- Configuração (RN40 a RN43) ----

    [Fact]
    public async Task CriarModelo_GestorDaEquipe_CriaAtivoComHistorico()
    {
        // RN40 e RN32
        var modelo = await CriarModeloA();

        Assert.True(modelo.Ativo);
        Assert.Equal(["qualidade", "observacoes"], modelo.Criterios.Select(c => c.Chave));
        Assert.Equal("OBRIGATORIO", modelo.Criterios[0].Comentario);
        var linha = Assert.Single(_ambiente.Historico.Linhas, l => l.AlvoTipo == AlvoHistorico.ModeloAvaliacao);
        Assert.Equal((OperacaoHistorico.Criacao, _ambiente.EquipeA.Id), (linha.Operacao, linha.AlvoId));
        Assert.Contains("\"criterios.qualidade\"", linha.DadosDepois);
    }

    [Fact]
    public async Task CriarModelo_GestorDeOutraEquipe404_Interno403_Externo403()
    {
        // RN40
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarModeloA(como: _gestorB));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarModeloA(como: _internoA));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarModeloA(como: _externoA));
    }

    [Fact]
    public async Task CriarModelo_EntidadeGlobal_400()
    {
        // RN40: só entidades por equipe têm modelo.
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            _ambiente.CriarModelo.ExecutarAsync(_ambiente.EquipeA.Id, "testes", "comunidade", EntradaModelo(), CancellationToken.None));

        Assert.Contains("global", erro.Erros["entidade"][0]);
    }

    [Fact]
    public async Task CriarModelo_SegundoAtivo_409()
    {
        // RN41
        await CriarModeloA();

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => CriarModeloA());

        Assert.Equal("MODELO_ATIVO_EXISTENTE", erro.Codigo);
    }

    [Fact]
    public async Task CriarModelo_DefinicaoInvalida_TodosOsErrosJuntos()
    {
        // RN42 e RN05
        var entrada = new ModeloAvaliacaoEntrada(null, "", null,
        [
            new CriterioAvaliacaoEntrada("Qualidade", "Qualidade", null, "ESTRELAS", null, null, 5000, null, null),
            new CriterioAvaliacaoEntrada("obs", "Obs", null, "TEXTO", null, "OPCIONAL", null, null, null),
        ]);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            _ambiente.CriarModelo.ExecutarAsync(_ambiente.EquipeA.Id, "testes", Entidade, entrada, CancellationToken.None));

        Assert.Equal(
            ["criterios[0].chave", "criterios[0].tamanhoMaximoTexto", "criterios[1].comentario", "nome"],
            erro.Erros.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AtualizarModelo_VersaoAntiga_409()
    {
        // RN12 no modelo
        var modelo = await CriarModeloA();
        await AtualizarModeloA(modelo.Versao, Qualidade with { Nome = "Qualidade geral" }, Observacoes);

        await Assert.ThrowsAsync<ConflitoConcorrenciaException>(() => AtualizarModeloA(modelo.Versao, Qualidade, Observacoes));
    }

    [Fact]
    public async Task AtualizarModelo_ExcluirCriterioComRespostas400_DesativarFuncionaETiraDasMedias()
    {
        // RN43
        var modelo = await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        await Como(_internoA, () => Avaliar(relatorio, Nota(4)));

        // Excluir "qualidade" (tem respostas) é proibido; o erro explica o caminho.
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => AtualizarModeloA(modelo.Versao, Observacoes));
        Assert.Contains("desativ", erro.Erros["criterios"][0], StringComparison.OrdinalIgnoreCase);

        // Desativar funciona: o critério some do resumo e das médias, a avaliação continua lá.
        var alterado = await AtualizarModeloA(modelo.Versao, Qualidade with { Ativo = false }, Observacoes with { Obrigatorio = false });
        Assert.False(alterado.Criterios.Single(c => c.Chave == "qualidade").Ativo);
        Assert.True(alterado.Criterios.Single(c => c.Chave == "qualidade").PossuiRespostas);

        var resumo = (await Como(_internoA, () => ListarAvaliacoes(relatorio))).Resumo!;
        Assert.Empty(resumo.Criterios);
        Assert.Equal(1, resumo.QuantidadeAvaliadores);
    }

    [Fact]
    public async Task AtualizarModelo_MudarTipoDeCriterioComRespostas_400()
    {
        // RN43
        var modelo = await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        await Como(_internoA, () => Avaliar(relatorio, Nota(4, observacoes: "texto")));

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            AtualizarModeloA(modelo.Versao, Qualidade, Observacoes with { Tipo = "ESTRELAS" }));

        Assert.Contains("o tipo não pode mudar", erro.Erros["criterios[1].tipo"][0]);
    }

    [Fact]
    public async Task AtualizarModelo_NovoCriterioObrigatorio_NaoInvalidaAvaliacaoAntigaMasValeNaEdicao()
    {
        // RN43: regras novas valem para avaliações criadas ou editadas depois.
        var modelo = await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        var antiga = await Como(_internoA, () => Avaliar(relatorio, Nota(4)));

        await AtualizarModeloA(modelo.Versao, Qualidade, Observacoes,
            new CriterioAvaliacaoEntrada("clareza", "Clareza", null, "ESTRELAS", true, null, null, null, null));

        // A antiga continua no resumo...
        var resumo = (await Como(_internoA, () => ListarAvaliacoes(relatorio))).Resumo!;
        Assert.Equal(4.0m, resumo.Criterios.Single(c => c.Chave == "qualidade").Media);
        Assert.Equal(0, resumo.Criterios.Single(c => c.Chave == "clareza").Quantidade);

        // ... mas editá-la exige o critério novo.
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            Como(_internoA, () => Avaliar(relatorio, Nota(5), antiga.Avaliacao.Versao)));
        Assert.True(erro.Erros.ContainsKey("respostas.clareza"));
    }

    [Fact]
    public async Task DesativarModelo_ImpedeNovasAvaliacoesEMantemAsAntigasSeparadas()
    {
        // RN41
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        await Como(_internoA, () => Avaliar(relatorio, Nota(5)));

        await _ambiente.AlterarAtivacaoModelo.ExecutarAsync(
            _ambiente.EquipeA.Id, "testes", Entidade, new AtivacaoModeloEntrada(false, null), CancellationToken.None);

        await Assert.ThrowsAsync<ValidacaoRegistroException>(() => Como(_outroInternoA, () => Avaliar(relatorio, Nota(3))));
        var avaliacoes = await Como(_internoA, () => ListarAvaliacoes(relatorio));
        Assert.Null(avaliacoes.Resumo);
        var anterior = Assert.Single(avaliacoes.ResumosAnteriores);
        Assert.False(anterior.Modelo.Ativo);
        Assert.Equal("Avaliação da Equipe A", anterior.Modelo.Nome);
        Assert.Equal(5.0m, anterior.Criterios[0].Media);
        Assert.Single(avaliacoes.Avaliacoes!.Itens);

        // Depois de desativar, a equipe pode criar outro modelo.
        var novo = await CriarModeloA();
        Assert.True(novo.Ativo);
    }

    // ---- Avaliar (RN44 a RN46) ----

    [Fact]
    public async Task Avaliar_InternoCriaEDepoisEdita_ContinuaUmaSo()
    {
        // RN45
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();

        var criada = await Como(_internoA, () => Avaliar(relatorio, Nota(3)));
        var editada = await Como(_internoA, () => Avaliar(relatorio, Nota(5, observacoes: "Revisei"), criada.Avaliacao.Versao));

        Assert.True(criada.Criada);
        Assert.False(editada.Criada);
        Assert.Equal(criada.Avaliacao.Id, editada.Avaliacao.Id);
        Assert.Single(_ambiente.BancoAvaliacoes.Avaliacoes);
        Assert.Equal(5, editada.Avaliacao.Respostas["qualidade"]!["estrelas"]!.GetValue<byte>());
        Assert.Equal("Revisei", editada.Avaliacao.Respostas["observacoes"]!["texto"]!.GetValue<string>());
        Assert.Equal(
            [OperacaoHistorico.Criacao, OperacaoHistorico.Alteracao],
            _ambiente.Historico.Linhas.Where(l => l.AlvoTipo == AlvoHistorico.Avaliacao).Select(l => l.Operacao));
    }

    [Fact]
    public async Task Avaliar_EditarSemVersao400_ComVersaoAntiga409()
    {
        // RN45 e RN12
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        var criada = await Como(_internoA, () => Avaliar(relatorio, Nota(3)));
        await Como(_internoA, () => Avaliar(relatorio, Nota(4), criada.Avaliacao.Versao));

        var semVersao = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => Como(_internoA, () => Avaliar(relatorio, Nota(5))));
        Assert.True(semVersao.Erros.ContainsKey("versao"));
        await Assert.ThrowsAsync<ConflitoConcorrenciaException>(() =>
            Como(_internoA, () => Avaliar(relatorio, Nota(5), criada.Avaliacao.Versao)));
    }

    [Fact]
    public async Task Avaliar_RespostasInvalidas_TresErrosJuntos()
    {
        // RN46: sem o comentário obrigatório, com 6 estrelas e com um critério inexistente.
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => Como(_internoA, () => Avaliar(relatorio, new JsonObject
        {
            ["qualidade"] = new JsonObject { ["estrelas"] = 6 },
            ["inexistente"] = new JsonObject { ["estrelas"] = 3 },
        })));

        Assert.Equal(
            ["respostas.inexistente", "respostas.qualidade.comentario", "respostas.qualidade.estrelas"],
            erro.Erros.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(_ambiente.BancoAvaliacoes.Avaliacoes);
    }

    [Fact]
    public async Task Avaliar_Externo403_OutraEquipe404_SemModelo400()
    {
        // RN44
        await CriarModeloA();
        var relatorioA = await CriarRelatorioA();
        var relatorioB = await CriarRelatorio(_ambiente.EquipeB.Id);
        var projetoA = Guid.Parse((await _ambiente.Obter(Entidade, relatorioA)).Dados["projeto"]!["id"]!.GetValue<string>());

        await Assert.ThrowsAsync<AcessoNegadoException>(() => Como(_externoA, () => Avaliar(relatorioA, Nota(4))));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Como(_internoA, () => Avaliar(relatorioB, Nota(4))));
        var semModelo = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            Como(_internoA, () => _ambiente.AvaliarRegistro.ExecutarAsync("testes", "projeto", projetoA,
                new AvaliacaoEntrada(null, new JsonObject()), CancellationToken.None)));
        Assert.Contains("não tem modelo de avaliação ativo", semModelo.Erros["modelo"][0]);
    }

    [Fact]
    public async Task Avaliar_ComAprovacaoAtiva_PendenteDa409_AprovadoPodeSerAvaliado()
    {
        // RN44: só itens APROVADOS.
        await CriarModeloA();
        _ambiente.AtivarAprovacao(_ambiente.Grupo.Relatorio, _ambiente.EquipeA.Id);
        var projeto = await _ambiente.CriarProjeto();
        var pendente = await Como(_externoA, () => _ambiente.Criar(Entidade, new JsonObject { ["projeto"] = projeto.ToString() }));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Como(_internoA, () => Avaliar(pendente.Id, Nota(4))));
        Assert.Equal("REGISTRO_NAO_APROVADO", erro.Codigo);

        await Como(_gestorA, () => _ambiente.Decidir(Entidade, pendente.Id, "APROVAR", "Conferido com a área responsável."));
        var avaliada = await Como(_internoA, () => Avaliar(pendente.Id, Nota(4)));
        Assert.True(avaliada.Criada);
    }

    [Fact]
    public async Task RemoverMinhaAvaliacao_RemoveComHistorico_SegundaVez404()
    {
        // RN45
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        await Como(_internoA, () => Avaliar(relatorio, Nota(4)));

        await Como(_internoA, () => Remover(relatorio));

        Assert.Empty(_ambiente.BancoAvaliacoes.Avaliacoes);
        var linha = _ambiente.Historico.Linhas.Last();
        Assert.Equal((AlvoHistorico.Avaliacao, OperacaoHistorico.Exclusao, relatorio), (linha.AlvoTipo, linha.Operacao, linha.AlvoId));
        Assert.Contains("\"qualidade.estrelas\":4", linha.DadosAntes);
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Como(_internoA, () => Remover(relatorio)));
    }

    // ---- Resumo e visibilidade (RN47, RN48) ----

    [Fact]
    public async Task Resumo_TresAvaliadores_MediaDistribuicaoEMinha()
    {
        // RN47
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        await Como(_internoA, () => Avaliar(relatorio, Nota(4)));
        await Como(_outroInternoA, () => Avaliar(relatorio, Nota(5)));
        await Como(_terceiroInternoA, () => Avaliar(relatorio, Nota(3)));

        var visto = await Como(_outroInternoA, () => ListarAvaliacoes(relatorio));

        var qualidade = Assert.Single(visto.Resumo!.Criterios);
        Assert.Equal((4.0m, 3), (qualidade.Media, qualidade.Quantidade));
        Assert.Equal(new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 1, ["4"] = 1, ["5"] = 1 }, qualidade.Distribuicao);
        Assert.Equal(3, visto.Resumo.QuantidadeAvaliadores);
        Assert.Equal(5, visto.Resumo.Minha!.Respostas["qualidade"]!["estrelas"]!.GetValue<byte>());
        Assert.Equal(3, visto.Avaliacoes!.Total);
    }

    [Fact]
    public async Task Resumo_ExternoVeMediasMasNaoAsAvaliacoesIndividuaisNemOHistorico()
    {
        // RN47
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        await Como(_internoA, () => Avaliar(relatorio, Nota(4)));

        var visto = await Como(_externoA, () => ListarAvaliacoes(relatorio));

        Assert.Equal(4.0m, visto.Resumo!.Criterios[0].Media);
        Assert.Null(visto.Resumo.Minha);
        Assert.Null(visto.Avaliacoes);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => Como(_externoA, () =>
            _ambiente.ListarHistoricoAvaliacoes.ExecutarAsync("testes", Entidade, relatorio, [], CancellationToken.None)));
    }

    [Fact]
    public async Task Registro_ComModeloAtivoTrazAvaliacao_SemModeloNaoTraz()
    {
        // RN48: em entidades e equipes sem modelo, nada muda.
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        var semModelo = await CriarRelatorio(_ambiente.EquipeB.Id);
        await Como(_internoA, () => Avaliar(relatorio, Nota(4)));
        await Como(_outroInternoA, () => Avaliar(relatorio, Nota(5)));

        var dto = await Como(_internoA, () => _ambiente.Obter(Entidade, relatorio));

        Assert.Equal(4.5m, dto.Avaliacao!.Medias["qualidade"]);
        Assert.Equal(2, dto.Avaliacao.Quantidade);
        Assert.True(dto.Avaliacao.Minha);
        Assert.False((await Como(_terceiroInternoA, () => _ambiente.Obter(Entidade, relatorio))).Avaliacao!.Minha);
        Assert.Null((await _ambiente.Obter(Entidade, semModelo)).Avaliacao);
    }

    [Fact]
    public async Task ExcluirRegistro_GravaNoHistoricoAsAvaliacoesQueSaemJunto()
    {
        // RN45: o CASCADE remove as avaliações; o histórico guarda o que existia.
        await CriarModeloA();
        var relatorio = await CriarRelatorioA();
        await Como(_internoA, () => Avaliar(relatorio, Nota(4)));

        await _ambiente.Excluir(Entidade, relatorio);

        var linhas = _ambiente.Historico.Linhas.Where(l => l.AlvoId == relatorio && l.Operacao == OperacaoHistorico.Exclusao).ToList();
        Assert.Equal([AlvoHistorico.Registro, AlvoHistorico.Avaliacao], linhas.Select(l => l.AlvoTipo).Order());
        Assert.Single(linhas.Select(l => l.CorrelacaoId).Distinct());
    }

    // ---- Apoio ----

    private static readonly CriterioAvaliacaoEntrada Qualidade = new("qualidade", "Qualidade", null, "ESTRELAS", true, "OBRIGATORIO", null, 1, null);
    private static readonly CriterioAvaliacaoEntrada Observacoes = new("observacoes", "Observações", null, "TEXTO", false, null, null, 2, null);

    private static ModeloAvaliacaoEntrada EntradaModelo(string? versao = null, params CriterioAvaliacaoEntrada[] criterios) =>
        new(versao, "Avaliação da Equipe A", null, criterios.Length == 0 ? [Qualidade, Observacoes] : criterios);

    private Task<ModeloAvaliacaoDto> CriarModeloA(UsuarioAtual? como = null) => Como(como ?? _gestorA, () =>
        _ambiente.CriarModelo.ExecutarAsync(_ambiente.EquipeA.Id, "testes", Entidade, EntradaModelo(), CancellationToken.None));

    private Task<ModeloAvaliacaoDto> AtualizarModeloA(string versao, params CriterioAvaliacaoEntrada[] criterios) => Como(_gestorA, () =>
        _ambiente.AtualizarModelo.ExecutarAsync(_ambiente.EquipeA.Id, "testes", Entidade, EntradaModelo(versao, criterios), CancellationToken.None));

    private Task<Guid> CriarRelatorioA() => CriarRelatorio(_ambiente.EquipeA.Id);

    private async Task<Guid> CriarRelatorio(Guid equipe)
    {
        var projeto = (await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" + Guid.NewGuid(), ["equipeId"] = equipe.ToString() })).Id;
        return (await _ambiente.Criar(Entidade, new JsonObject { ["projeto"] = projeto.ToString(), ["equipeId"] = equipe.ToString() })).Id;
    }

    private static JsonObject Nota(int estrelas, string comentario = "Justificativa", string? observacoes = null)
    {
        var respostas = new JsonObject { ["qualidade"] = new JsonObject { ["estrelas"] = estrelas, ["comentario"] = comentario } };
        if (observacoes is not null)
            respostas["observacoes"] = new JsonObject { ["texto"] = observacoes };
        return respostas;
    }

    private Task<ResultadoAvaliacao> Avaliar(Guid relatorio, JsonObject respostas, string? versao = null) =>
        _ambiente.AvaliarRegistro.ExecutarAsync("testes", Entidade, relatorio, new AvaliacaoEntrada(versao, respostas), CancellationToken.None);

    private Task Remover(Guid relatorio) =>
        _ambiente.RemoverMinhaAvaliacao.ExecutarAsync("testes", Entidade, relatorio, CancellationToken.None);

    private Task<AvaliacoesDoRegistroDto> ListarAvaliacoes(Guid relatorio) =>
        _ambiente.ListarAvaliacoes.ExecutarAsync("testes", Entidade, relatorio, [], CancellationToken.None);

    private UsuarioAtual Usuario(Perfil perfil, string matricula, Guid equipe)
    {
        var usuario = UsuarioAtualFalso.Criar(perfil, equipe);
        _ambiente.Usuarios.Usuarios.Add(new Usuario { Id = usuario.Id, Matricula = matricula, Nome = matricula, Perfil = perfil });
        return usuario;
    }

    /// <summary>Executa como outro usuário e volta para o administrador padrão.</summary>
    private async Task<T> Como<T>(UsuarioAtual usuario, Func<Task<T>> acao)
    {
        var anterior = _ambiente.Usuario;
        _ambiente.Usuario = usuario;
        try
        {
            return await acao();
        }
        finally
        {
            _ambiente.Usuario = anterior;
        }
    }

    private Task Como(UsuarioAtual usuario, Func<Task> acao) => Como(usuario, async () =>
    {
        await acao();
        return true;
    });
}
