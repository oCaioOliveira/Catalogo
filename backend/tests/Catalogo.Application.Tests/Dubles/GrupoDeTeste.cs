using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Tests.Dubles;

/// <summary>
/// Versão reduzida do grupo "testes_desempenho" do seed, com o essencial para os casos de uso:
/// comunidade (nome único), projeto, endpoint (→ projeto), relatório (→ projeto) e
/// cenário (→ relatório, → endpoint, com a dependência do projeto e unicidade relatório + número).
/// </summary>
internal sealed class GrupoDeTeste
{
    public GrupoDeTeste()
    {
        ComunidadeNome = Campo(Comunidade, "nome", "Nome", TipoDado.Texto, ordem: 1, tamanho: 100);
        ComunidadeApelido = Campo(Comunidade, "apelido", "Apelido", TipoDado.Texto, ordem: 2, tamanho: 50, obrigatorio: false, ativo: false);
        ComunidadeDescricao = Campo(Comunidade, "descricao", "Descrição", TipoDado.TextoLongo, ordem: 3, obrigatorio: false);
        ProjetoNome = Campo(Projeto, "nome", "Nome", TipoDado.Texto, ordem: 1, tamanho: 150);
        EndpointProjeto = Referencia(Endpoint, "projeto", "Projeto", Projeto, ordem: 1);
        EndpointNome = Campo(Endpoint, "nome_servico", "Nome do serviço", TipoDado.Texto, ordem: 2, tamanho: 150);
        RelatorioProjeto = Referencia(Relatorio, "projeto", "Projeto", Projeto, ordem: 1);
        CenarioRelatorio = Referencia(Cenario, "relatorio_teste", "Relatório", Relatorio, ordem: 1, acao: AcaoExclusao.Cascata);
        CenarioEndpoint = Referencia(Cenario, "endpoint_servico", "Endpoint", Endpoint, ordem: 2);
        CenarioNumero = Campo(Cenario, "numero_cenario", "Nº do cenário", TipoDado.Inteiro, ordem: 3);

        UqComunidadeNome = Restricao(Comunidade, "uq_comunidade_nome", ComunidadeNome);
        UqCenario = Restricao(Cenario, "uq_cenario_relatorio_numero", CenarioRelatorio, CenarioNumero);
        UqProjetoNome = Restricao(Projeto, "uq_projeto_nome", ProjetoNome);

        Grupo = new Grupo
        {
            Id = Guid.NewGuid(),
            Chave = "testes",
            Nome = "Testes",
            EquipesHabilitadas = EquipesHabilitadas,
            Entidades =
            [
                // Comunidade é GLOBAL (como no seed); as demais são por equipe (RN27).
                Entidade(Comunidade, "comunidade", "Comunidade", [ComunidadeNome, ComunidadeApelido, ComunidadeDescricao], [UqComunidadeNome], global: true),
                Entidade(Projeto, "projeto", "Projeto", [ProjetoNome], [UqProjetoNome]),
                Entidade(Endpoint, "endpoint_servico", "Endpoint de Serviço", [EndpointProjeto, EndpointNome]),
                Entidade(Relatorio, "relatorio_teste", "Relatório de Teste", [RelatorioProjeto]),
                Entidade(Cenario, "cenario_teste", "Cenário de Teste", [CenarioRelatorio, CenarioEndpoint, CenarioNumero], [UqCenario],
                [
                    new CampoDependencia
                    {
                        Campo = CenarioEndpoint,
                        CampoVia = CenarioRelatorio,
                        CampoOrigem = RelatorioProjeto,
                        CampoAlvo = EndpointProjeto,
                    },
                ]),
            ],
        };
    }

    /// <summary>
    /// Equipes habilitadas no grupo (RN90). O <see cref="AmbienteDeTeste"/> habilita a Equipe A e a B; os testes de
    /// listagem por equipe tiram ou põem equipes aqui (o grupo guarda a mesma instância).
    /// </summary>
    public HashSet<Guid> EquipesHabilitadas { get; } = [];

    public Guid Comunidade { get; } = Guid.NewGuid();
    public Guid Projeto { get; } = Guid.NewGuid();
    public Guid Endpoint { get; } = Guid.NewGuid();
    public Guid Relatorio { get; } = Guid.NewGuid();
    public Guid Cenario { get; } = Guid.NewGuid();

    public Campo ComunidadeNome { get; }
    public Campo ComunidadeApelido { get; }
    public Campo ComunidadeDescricao { get; }
    public Campo ProjetoNome { get; }
    public Campo EndpointProjeto { get; }
    public Campo EndpointNome { get; }
    public Campo RelatorioProjeto { get; }
    public Campo CenarioRelatorio { get; }
    public Campo CenarioEndpoint { get; }
    public Campo CenarioNumero { get; }

    public RestricaoUnica UqComunidadeNome { get; }
    public RestricaoUnica UqCenario { get; }
    public RestricaoUnica UqProjetoNome { get; }

    public Grupo Grupo { get; }

    private static Campo Campo(Guid entidadeId, string chave, string nome, TipoDado tipo, int ordem,
        int? tamanho = null, bool obrigatorio = true, bool ativo = true) => new()
    {
        Id = Guid.NewGuid(),
        EntidadeId = entidadeId,
        Chave = chave,
        Nome = nome,
        TipoDado = tipo,
        Obrigatorio = obrigatorio,
        TamanhoMaximo = tamanho,
        Ordem = ordem,
        Ativo = ativo,
    };

    private static Campo Referencia(Guid entidadeId, string chave, string nome, Guid referenciada, int ordem,
        AcaoExclusao acao = AcaoExclusao.Restringir) => new()
    {
        Id = Guid.NewGuid(),
        EntidadeId = entidadeId,
        Chave = chave,
        Nome = nome,
        TipoDado = TipoDado.Referencia,
        Obrigatorio = true,
        EntidadeReferenciadaId = referenciada,
        AcaoExclusao = acao,
        Ordem = ordem,
    };

    private static RestricaoUnica Restricao(Guid entidadeId, string nome, params Campo[] campos) => new()
    {
        Id = Guid.NewGuid(),
        EntidadeId = entidadeId,
        Nome = nome,
        Campos = campos,
    };

    private Entidade Entidade(Guid id, string chave, string nome, IReadOnlyList<Campo> campos,
        IReadOnlyList<RestricaoUnica>? restricoes = null, IReadOnlyList<CampoDependencia>? dependencias = null, bool global = false) => new()
    {
        Id = id,
        EscopoEquipe = !global,
        GrupoId = Guid.Empty,
        Chave = chave,
        Nome = nome,
        // Rótulo: o primeiro campo que não é REFERENCIA; se só houver referências, a primeira (rótulo nulo, RN20).
        CampoExibicaoId = (campos.FirstOrDefault(c => !c.EhReferencia) ?? campos[0]).Id,
        Campos = campos,
        RestricoesUnicas = restricoes ?? [],
        Dependencias = dependencias ?? [],
    };
}
