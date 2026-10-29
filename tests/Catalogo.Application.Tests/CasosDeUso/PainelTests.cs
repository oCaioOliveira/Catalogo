using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.CasosDeUso.Painel;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// Painel gerencial: acesso (RN78), visibilidade da listagem (RN79), período (RN80), aprovação (RN82), formatação das linhas
// da agregação (RN85 a RN87) e exportação (RN88). Os números em si vêm do banco (testes de integração).
public class PainelTests
{
    private readonly AmbienteDeTeste _ambiente = new();
    private readonly PainelRepositoryFalso _painel = new();
    private readonly OpcoesPainel _opcoes = new(TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));

    private ObterVisaoGeral VisaoGeral()
    {
        var metadados = new MetadadosProviderFalso(_ambiente.Grupo.Grupo);
        return new ObterVisaoGeral(_ambiente.UsuarioAtual, metadados, _painel, _ambiente.Modelos, _ambiente.Repositorio,
            _ambiente.Equipes, _ambiente.Usuarios, _opcoes, _ambiente.Relogio);
    }

    private CalcularIndicadores Indicadores()
    {
        var metadados = new MetadadosProviderFalso(_ambiente.Grupo.Grupo);
        return new CalcularIndicadores(_ambiente.UsuarioAtual, metadados, new PreparadorConsultaRegistros(_ambiente.UsuarioAtual, metadados, _ambiente.Modelos),
            _ambiente.Modelos, _painel, _ambiente.Repositorio, _opcoes);
    }

    private static List<KeyValuePair<string, string>> Parametros(string url) =>
        url.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Select(p => KeyValuePair.Create(p[0], p.Length > 1 ? Uri.UnescapeDataString(p[1]) : ""))
            .ToList();

    private Task<VisaoGeralDto> ObterVisaoGeral(string url = "") => VisaoGeral().ExecutarAsync("testes", Parametros(url), CancellationToken.None);

    private Task<IndicadoresDto> Calcular(string entidade, string url) =>
        Indicadores().ExecutarAsync("testes", entidade, Parametros(url), CancellationToken.None);

    // ---- Acesso (RN78) ----

    [Theory]
    [InlineData(Perfil.Interno)]
    [InlineData(Perfil.Externo)]
    public async Task InternoEExterno_NaoVeemOPainel(Perfil perfil)
    {
        _ambiente.Usuario = UsuarioAtualFalso.Criar(perfil, _ambiente.EquipeA.Id);

        var visao = await Assert.ThrowsAsync<AcessoNegadoException>(() => ObterVisaoGeral());
        var indicadores = await Assert.ThrowsAsync<AcessoNegadoException>(() => Calcular("projeto", ""));

        Assert.Equal(CodigosErro.SemPermissao, visao.Codigo);
        Assert.Equal(CodigosErro.SemPermissao, indicadores.Codigo);
        Assert.Empty(_painel.PedidosDeVolume);
    }

    [Fact]
    public async Task Gestor_FiltrandoEquipeDeQueNaoParticipa_403()
    {
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, _ambiente.EquipeA.Id);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => ObterVisaoGeral($"equipeId={_ambiente.EquipeB.Id}"));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => Calcular("projeto", $"equipeId={_ambiente.EquipeB.Id}"));
    }

    // ---- Visibilidade (RN79) ----

    [Fact]
    public async Task VisaoGeral_Gestor_UsaOEscopoDaListagem()
    {
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, _ambiente.EquipeA.Id);

        await ObterVisaoGeral();

        var porEntidade = _painel.PedidosDeVolume.ToDictionary(p => p.EntidadeId, p => p.Escopo);
        Assert.Equal(5, porEntidade.Count);
        // Global: todos os registros. Por equipe: só as equipes do gestor (o mesmo escopo da listagem).
        Assert.Null(porEntidade[_ambiente.Grupo.Comunidade].EquipesPermitidas);
        Assert.Equal([_ambiente.EquipeA.Id], porEntidade[_ambiente.Grupo.Projeto].EquipesPermitidas!);
        Assert.All(porEntidade.Values, e => Assert.Null(e.NaoAprovadosSomenteDoAutor));
        var atividade = Assert.Single(_painel.PedidosDeAtividade);
        Assert.Equal([_ambiente.Grupo.Comunidade], atividade.EntidadesGlobais);
        Assert.Equal([_ambiente.EquipeA.Id], atividade.Equipes!);
    }

    [Fact]
    public async Task VisaoGeral_AdministradorComEquipe_FiltraAsPorEquipeEMantemAsGlobais()
    {
        await ObterVisaoGeral($"equipeId={_ambiente.EquipeB.Id}");

        var porEntidade = _painel.PedidosDeVolume.ToDictionary(p => p.EntidadeId, p => p.Escopo);
        Assert.Null(porEntidade[_ambiente.Grupo.Comunidade].FiltroEquipe);
        Assert.Equal(_ambiente.EquipeB.Id, porEntidade[_ambiente.Grupo.Cenario].FiltroEquipe);
        Assert.Null(porEntidade[_ambiente.Grupo.Cenario].EquipesPermitidas);
    }

    [Fact]
    public async Task VisaoGeral_SomaOVolumeEDaNomeAsEquipes()
    {
        _painel.Volumes[_ambiente.Grupo.Projeto] = new VolumeEntidade(3, [new(_ambiente.EquipeA.Id, 3)], [new(2026, 3, 2), new(2026, 4, 1)], 3, 1);
        _painel.Volumes[_ambiente.Grupo.Relatorio] = new VolumeEntidade(2, [new(_ambiente.EquipeA.Id, 1), new(_ambiente.EquipeB.Id, 1)], [new(2026, 3, 2)], 2, 0);

        var volume = (await ObterVisaoGeral()).Volume;

        Assert.Equal(5, volume.Total);
        Assert.Equal(5, volume.CriadosNoPeriodo);
        Assert.Equal(1, volume.AlteradosNoPeriodo);
        Assert.Equal([new QuantidadeEquipeDto(_ambiente.EquipeA.Id, "Equipe A", 4), new QuantidadeEquipeDto(_ambiente.EquipeB.Id, "Equipe B", 1)], volume.PorEquipe);
        Assert.Equal([new QuantidadeMesDto("2026-03", 4), new QuantidadeMesDto("2026-04", 1)], volume.CriadosPorMes);
        // Na ordem do menu (Ordem; aqui todas têm a mesma, então pela chave).
        Assert.Equal(["cenario_teste", "comunidade", "endpoint_servico", "projeto", "relatorio_teste"], volume.PorEntidade.Select(e => e.Entidade).ToList());
    }

    // ---- Aprovação (RN82) ----

    [Fact]
    public async Task VisaoGeral_SemAprovacaoNemModelo_BlocosNulos()
    {
        var visao = await ObterVisaoGeral();

        Assert.Null(visao.Aprovacao);
        Assert.Null(visao.Avaliacao);
        Assert.Empty(_painel.PedidosDeAprovacao);
    }

    [Fact]
    public async Task VisaoGeral_Aprovacao_SoNasEquipesComAprovacaoAtiva_ETotaisPonderados()
    {
        _painel.AprovacoesAtivas.AddRange([new(_ambiente.Grupo.Projeto, _ambiente.EquipeA.Id), new(_ambiente.Grupo.Relatorio, _ambiente.EquipeA.Id)]);
        // Projeto: 1 primeira decisão em 1 h. Relatório: 3 primeiras decisões com média de 5 h. Total: (1×1 + 3×5) / 4 = 4 h.
        _painel.Aprovacoes[_ambiente.Grupo.Projeto] = new AprovacaoEntidade(
            new Dictionary<SituacaoAprovacao, int> { [SituacaoAprovacao.Pendente] = 2, [SituacaoAprovacao.Aprovado] = 1 }, 1, 3600, 1, 1, 0);
        _painel.Aprovacoes[_ambiente.Grupo.Relatorio] = new AprovacaoEntidade(
            new Dictionary<SituacaoAprovacao, int> { [SituacaoAprovacao.Rejeitado] = 1, [SituacaoAprovacao.Aprovado] = 2 }, 0, 5 * 3600, 3, 2, 2);

        var aprovacao = (await ObterVisaoGeral()).Aprovacao!;

        Assert.Equal(7, aprovacao.DiasPendenteAntigo);
        Assert.Equal(new NumerosAprovacaoDto(2, 3, 1, 1, 4.0, 4, 3, 2, 0.4), aprovacao.Total);
        Assert.Equal(["projeto", "relatorio_teste"], aprovacao.PorEntidade.Select(e => e.Entidade).ToList());
        // O escopo da aprovação é o da listagem, reduzido às equipes com aprovação ativa.
        Assert.All(_painel.PedidosDeAprovacao, p => Assert.Equal([_ambiente.EquipeA.Id], p.Escopo.EquipesPermitidas!));
    }

    [Fact]
    public async Task VisaoGeral_ParametrosInvalidos_TodosOsErrosJuntos()
    {
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => ObterVisaoGeral("de=2026-13-01&equipeId=x&pagina=2"));

        Assert.Equal(["de", "equipeId", "pagina"], erro.Erros.Keys.Order(StringComparer.Ordinal).ToList());
    }

    // ---- Agregação (RN85 a RN87) ----

    [Fact]
    public async Task Indicadores_ErrosDeFiltroDosIndicadoresEDoPeriodo_SaemTodosJuntos()
    {
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            Calcular("cenario_teste", "numero_cenario=abc&agruparPor=nao_existe&metricas=media:relatorio_teste&de=ontem&tamanho=10"));

        Assert.Equal(["agruparPor", "de", "metricas", "numero_cenario", "tamanho"], erro.Erros.Keys.Order(StringComparer.Ordinal).ToList());
        Assert.Empty(_painel.PedidosDeIndicadores);
    }

    [Fact]
    public async Task Indicadores_UsaOsFiltrosEOEscopoDaListagem()
    {
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, _ambiente.EquipeA.Id);

        await Calcular("cenario_teste", "numero_cenario.de=2&agruparPor=relatorio_teste&de=2026-03-01&ate=2026-03-31");

        var (consulta, periodo, indicadores) = Assert.Single(_painel.PedidosDeIndicadores);
        Assert.Equal("numero_cenario", Assert.Single(consulta.Condicoes).Campo.Chave);
        Assert.Equal([_ambiente.EquipeA.Id], consulta.EquipesPermitidas!);
        Assert.Equal(new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc), periodo.InicioUtc);
        Assert.Equal("relatorio_teste", Assert.Single(indicadores.Dimensoes).Chave);
    }

    [Fact]
    public async Task Indicadores_FormataAsLinhas()
    {
        var projeto = await _ambiente.CriarProjeto("Alfa");
        _painel.Indicadores = new ResultadoIndicadores(
            [
                new LinhaResultado([new ValorDimensao(Id: projeto), new ValorDimensao(Data: new DateTime(2026, 4, 1))], 3, [2.5000000000m, 3]),
                new LinhaResultado([new ValorDimensao(), new ValorDimensao()], 1, [null, 1]),
            ],
            Truncado: true);

        var resultado = await Calcular(
            "cenario_teste", "agruparPor=relatorio_teste.projeto,criadoEm:trimestre&metricas=media:numero_cenario,contagem");

        Assert.Equal(["relatorio_teste.projeto", "criadoEm:trimestre"], resultado.Dimensoes.Select(d => d.Chave).ToList());
        Assert.Equal("trimestre", resultado.Dimensoes[1].Periodo);
        Assert.True(resultado.Truncado);
        var linha = resultado.Linhas[0];
        Assert.Equal(new ValorReferenciaDto(projeto, "Alfa"), linha["relatorio_teste.projeto"]);
        Assert.Equal("2026-T2", linha["criadoEm:trimestre"]);
        Assert.Equal(3, linha["contagem"]);
        Assert.Equal("2.5", ((decimal)linha["media:numero_cenario"]!).ToString(System.Globalization.CultureInfo.InvariantCulture));
        // Registros sem valor na dimensão: null. Média sem valores: null.
        Assert.Null(resultado.Linhas[1]["relatorio_teste.projeto"]);
        Assert.Null(resultado.Linhas[1]["media:numero_cenario"]);
    }

    // ---- Exportação (RN88) ----

    [Fact]
    public async Task Exportar_ColunasDaRespostaComProtecaoContraFormula()
    {
        _painel.Indicadores = new ResultadoIndicadores(
            [
                new LinhaResultado([new ValorDimensao(Texto: "=1+1")], 2, [2]),
                new LinhaResultado([new ValorDimensao(Outros: true)], 5, [5]),
            ],
            false);
        var exportar = new ExportarIndicadores(Indicadores(), [_ambiente.Gerador], _opcoes, _ambiente.Relogio);

        var arquivo = await exportar.ExecutarAsync("testes", "comunidade", Parametros("formato=csv&agruparPor=nome"), CancellationToken.None);
        await arquivo.EscreverAsync(Stream.Null, CancellationToken.None);

        Assert.Equal("comunidade_indicadores_2026-10-21.csv", arquivo.NomeArquivo);
        Assert.Equal(["Nome", "Contagem"], _ambiente.Gerador.Colunas);
        Assert.Equal(["'=1+1", 2m], _ambiente.Gerador.Linhas[0]);
        Assert.Equal(["Outros", 5m], _ambiente.Gerador.Linhas[1]);
    }

    [Fact]
    public async Task Exportar_FormatoInvalido_SaiJuntoComOsOutrosErros()
    {
        var exportar = new ExportarIndicadores(Indicadores(), [_ambiente.Gerador], _opcoes, _ambiente.Relogio);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            exportar.ExecutarAsync("testes", "comunidade", Parametros("formato=pdf&agruparPor=descricao"), CancellationToken.None));

        Assert.Equal(["agruparPor", "formato"], erro.Erros.Keys.Order(StringComparer.Ordinal).ToList());
    }
}
