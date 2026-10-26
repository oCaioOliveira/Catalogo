using System.Text.Json.Nodes;
using Catalogo.Application.CasosDeUso;
using Catalogo.Application.CasosDeUso.Aprovacao;
using Catalogo.Application.CasosDeUso.Avaliacoes;
using Catalogo.Application.CasosDeUso.Historico;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.Tests.Dubles;

/// <summary>
/// Monta todos os casos de uso sobre os MESMOS dublês, como a injeção de dependência faria
/// numa requisição. Cada teste cria o seu ambiente (nada é compartilhado entre testes).
/// O usuário padrão é um ADMINISTRADOR; troque <see cref="Usuario"/> para testar outros perfis.
/// </summary>
internal sealed class AmbienteDeTeste
{
    public AmbienteDeTeste()
    {
        Repositorio = new RegistroRepositoryFalso(Grupo.Grupo);
        UnidadeDeTrabalho = new UnidadeDeTrabalhoFalsa(Repositorio, BancoAvaliacoes);
        Modelos = new ModeloAvaliacaoRepositoryFalso(BancoAvaliacoes);
        Avaliacoes = new AvaliacaoRepositoryFalso(BancoAvaliacoes);
        Equipes.Equipes.AddRange([EquipeA, EquipeB]);
        Grupo.EquipesHabilitadas.UnionWith([EquipeA.Id, EquipeB.Id]); // RN90: as duas equipes usam a listagem de teste.
        Usuarios.Usuarios.Add(new Usuario { Id = UsuarioAtual.Usuario.Id, Matricula = "admin", Nome = "Admin", Perfil = Perfil.Administrador });

        var metadados = _metadados = new MetadadosProviderFalso(Grupo.Grupo);
        var preparador = new PreparadorRegistro(Repositorio, new ValidadorRegistro(ValidadoresPadrao.Todos()), new CalculadoraChaveUnica());
        var montador = _montador = new MontadorRegistroDto(Repositorio, Usuarios, Modelos, Avaliacoes, UsuarioAtual);
        var montadorModelo = new MontadorModeloAvaliacao(Modelos, Usuarios);
        var resolvedor = new ResolvedorEquipe(Equipes);
        var historico = new ServicoHistorico(Historico, Relogio);
        var contexto = new ContextoAprovacao(Configuracoes);

        // O mesmo caminho de gravação do cadastro manual e da importação.
        Gravador = new GravadorRegistro(Repositorio, resolvedor, contexto, preparador, historico, Relogio);
        CriarRegistro = new CriarRegistro(UsuarioAtual, metadados, UnidadeDeTrabalho, Gravador, montador);
        AtualizarRegistro = new AtualizarRegistro(UsuarioAtual, metadados, Repositorio, UnidadeDeTrabalho, Gravador, montador);
        DecidirAprovacao = new DecidirAprovacao(UsuarioAtual, metadados, Repositorio, UnidadeDeTrabalho, historico, montador, Relogio);
        var obterConfiguracao = new ObterConfiguracaoAprovacao(UsuarioAtual, metadados, Equipes, Configuracoes, Usuarios);
        DefinirConfiguracaoAprovacao = new DefinirConfiguracaoAprovacao(
            UsuarioAtual, metadados, Equipes, Configuracoes, Repositorio, UnidadeDeTrabalho, historico, Relogio, obterConfiguracao);
        ListarPendentes = new ListarPendentes(UsuarioAtual, Repositorio, Usuarios, Equipes, metadados);
        ObterRegistro = new ObterRegistro(UsuarioAtual, metadados, Repositorio, montador);
        ExcluirRegistro = new ExcluirRegistro(UsuarioAtual, metadados, Repositorio, Modelos, Avaliacoes, historico);
        ListarEntidades = new ListarEntidades(UsuarioAtual, metadados, Configuracoes, Modelos);
        ListarRegistros = new ListarRegistros(new PreparadorConsultaRegistros(UsuarioAtual, metadados, Modelos), Repositorio, montador);

        CriarModelo = new CriarModeloAvaliacao(UsuarioAtual, metadados, Equipes, Modelos, UnidadeDeTrabalho, historico, montadorModelo);
        AtualizarModelo = new AtualizarModeloAvaliacao(UsuarioAtual, metadados, Equipes, Modelos, UnidadeDeTrabalho, historico, montadorModelo, Relogio);
        AlterarAtivacaoModelo = new AlterarAtivacaoModeloAvaliacao(
            UsuarioAtual, metadados, Equipes, Modelos, UnidadeDeTrabalho, historico, montadorModelo, Relogio);
        ObterModelo = new ObterModeloAvaliacao(UsuarioAtual, metadados, Equipes, Modelos, montadorModelo);
        AvaliarRegistro = new AvaliarRegistro(
            UsuarioAtual, metadados, Repositorio, contexto, Modelos, Avaliacoes, Usuarios, UnidadeDeTrabalho, historico, Relogio);
        RemoverMinhaAvaliacao = new RemoverMinhaAvaliacao(UsuarioAtual, metadados, Repositorio, contexto, Modelos, Avaliacoes, UnidadeDeTrabalho, historico);
        ListarAvaliacoes = new ListarAvaliacoesRegistro(UsuarioAtual, metadados, Repositorio, Modelos, Avaliacoes, Usuarios);
        ListarHistoricoAvaliacoes = new ListarHistoricoAvaliacoes(UsuarioAtual, metadados, Repositorio, Historico,
            new MontadorHistorico(Repositorio, Usuarios));
        ListarHistoricoRegistro = new ListarHistoricoRegistro(UsuarioAtual, metadados, Repositorio, Historico,
            new MontadorHistorico(Repositorio, Usuarios));
    }

    private readonly MetadadosProviderFalso _metadados;
    private readonly MontadorRegistroDto _montador;

    public GrupoDeTeste Grupo { get; } = new();
    public RegistroRepositoryFalso Repositorio { get; }
    public UnidadeDeTrabalhoFalsa UnidadeDeTrabalho { get; }
    public UsuarioAtualFalso UsuarioAtual { get; } = new();
    public EquipeRepositoryFalso Equipes { get; } = new();
    public UsuarioRepositoryFalso Usuarios { get; } = new();
    public HistoricoRepositoryFalso Historico { get; } = new();
    public RelogioFalso Relogio { get; } = new();
    public ConfiguracaoRepositoryFalso Configuracoes { get; } = new();
    public AvaliacoesEmMemoria BancoAvaliacoes { get; } = new();
    public ModeloAvaliacaoRepositoryFalso Modelos { get; }
    public AvaliacaoRepositoryFalso Avaliacoes { get; }

    /// <summary>Equipe usada por padrão nos registros por equipe criados pelos atalhos.</summary>
    public Equipe EquipeA { get; } = new() { Id = Guid.NewGuid(), Nome = "Equipe A" };
    public Equipe EquipeB { get; } = new() { Id = Guid.NewGuid(), Nome = "Equipe B" };

    /// <summary>Quem faz as próximas operações.</summary>
    public UsuarioAtual Usuario
    {
        get => UsuarioAtual.Usuario;
        set => UsuarioAtual.Usuario = value;
    }

    public GravadorRegistro Gravador { get; }
    public CriarRegistro CriarRegistro { get; }
    public AtualizarRegistro AtualizarRegistro { get; }
    public ObterRegistro ObterRegistro { get; }
    public ExcluirRegistro ExcluirRegistro { get; }
    public ListarEntidades ListarEntidades { get; }
    public ListarRegistros ListarRegistros { get; }
    public ListarHistoricoRegistro ListarHistoricoRegistro { get; }
    public DecidirAprovacao DecidirAprovacao { get; }
    public DefinirConfiguracaoAprovacao DefinirConfiguracaoAprovacao { get; }
    public ListarPendentes ListarPendentes { get; }
    public CriarModeloAvaliacao CriarModelo { get; }
    public AtualizarModeloAvaliacao AtualizarModelo { get; }
    public AlterarAtivacaoModeloAvaliacao AlterarAtivacaoModelo { get; }
    public ObterModeloAvaliacao ObterModelo { get; }
    public AvaliarRegistro AvaliarRegistro { get; }
    public RemoverMinhaAvaliacao RemoverMinhaAvaliacao { get; }
    public ListarAvaliacoesRegistro ListarAvaliacoes { get; }
    public ListarHistoricoAvaliacoes ListarHistoricoAvaliacoes { get; }

    /// <summary>Recebe o que a exportação escreveria no arquivo.</summary>
    public GeradorPlanilhaFalso Gerador { get; } = new();

    /// <summary>
    /// Exporta a entidade (parâmetros no formato da URL, ex.: "formato=csv&amp;nome.contem=a") e devolve a planilha
    /// que o gerador recebeu. Datas no fuso UTC, para os testes não dependerem do fuso.
    /// </summary>
    public async Task<(ArquivoExportado Arquivo, GeradorPlanilhaFalso Planilha)> Exportar(
        string entidade, string parametros = "formato=csv", int limiteLinhas = OpcoesExportacao.LimitePadrao)
    {
        var exportar = new ExportarRegistros(
            new PreparadorConsultaRegistros(UsuarioAtual, _metadados, Modelos), Repositorio, _montador, Modelos, Configuracoes,
            Equipes, [Gerador], new OpcoesExportacao(limiteLinhas, TimeZoneInfo.Utc), Relogio);
        var lista = parametros.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Select(p => KeyValuePair.Create(p[0], p.Length > 1 ? Uri.UnescapeDataString(p[1]) : ""))
            .ToList();

        var arquivo = await exportar.ExecutarAsync("testes", entidade, lista, CancellationToken.None);
        await arquivo.EscreverAsync(Stream.Null, CancellationToken.None);
        return (arquivo, Gerador);
    }

    /// <summary>Ativa a aprovação de uma entidade numa equipe direto no dublê (sem passar pelo caso de uso).</summary>
    public void AtivarAprovacao(Guid entidadeId, Guid equipeId) =>
        Configuracoes.Adicionar(new ConfiguracaoEntidadeEquipe { EntidadeId = entidadeId, EquipeId = equipeId, RequerAprovacao = true });

    public Task<RegistroDto> Decidir(string entidade, Guid id, string decisao, string? justificativa) =>
        DecidirAprovacao.ExecutarAsync("testes", entidade, id, new DecisaoEntrada(decisao, justificativa), CancellationToken.None);

    /// <summary>
    /// Cria um registro. Nas entidades por equipe, se o corpo não traz "equipeId" e o usuário é
    /// administrador, usa a Equipe A (o administrador sempre precisa informar a equipe, RN27).
    /// </summary>
    public Task<RegistroDto> Criar(string entidade, JsonObject dados)
    {
        var global = Grupo.Grupo.ObterEntidade(entidade)?.EhGlobal ?? false;
        if (!global && Usuario.EhAdministrador && !dados.ContainsKey("equipeId"))
            dados["equipeId"] = EquipeA.Id.ToString();

        return CriarRegistro.ExecutarAsync("testes", entidade, dados, CancellationToken.None);
    }

    public Task<RegistroDto> Atualizar(string entidade, Guid id, JsonObject dados) =>
        AtualizarRegistro.ExecutarAsync("testes", entidade, id, dados, CancellationToken.None);

    public Task Excluir(string entidade, Guid id) =>
        ExcluirRegistro.ExecutarAsync("testes", entidade, id, CancellationToken.None);

    public Task<RegistroDto> Obter(string entidade, Guid id) =>
        ObterRegistro.ExecutarAsync("testes", entidade, id, CancellationToken.None);

    public async Task<Guid> CriarProjeto(string nome = "Projeto") =>
        (await Criar("projeto", new JsonObject { ["nome"] = nome })).Id;

    public async Task<Guid> CriarRelatorio(Guid projeto) =>
        (await Criar("relatorio_teste", new JsonObject { ["projeto"] = projeto.ToString() })).Id;

    public async Task<Guid> CriarEndpoint(Guid projeto, string nome = "s") =>
        (await Criar("endpoint_servico", new JsonObject { ["projeto"] = projeto.ToString(), ["nome_servico"] = nome })).Id;

    public Task<RegistroDto> CriarCenario(Guid relatorio, Guid endpoint, int numero) =>
        Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, numero));

    public static JsonObject DadosDoCenario(Guid relatorio, Guid endpoint, int numero) => new()
    {
        ["relatorio_teste"] = relatorio.ToString(),
        ["endpoint_servico"] = endpoint.ToString(),
        ["numero_cenario"] = numero,
    };
}
