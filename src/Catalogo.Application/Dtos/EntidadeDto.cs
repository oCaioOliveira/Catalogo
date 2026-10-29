namespace Catalogo.Application.Dtos;

// Metadados devolvidos para o front montar formulários. Só campos ATIVOS aparecem.

public sealed record EntidadeDto(
    Guid Id,
    string Chave,
    string Nome,
    string? Descricao,
    // Chave do campo usado como rótulo do registro em selects e listas.
    string? CampoExibicao,
    // true = cada registro pertence a uma equipe; false = registros globais, alterados só pelo administrador (RN27).
    bool EscopoEquipe,
    // false: desativada (ela ou o grupo). Só o administrador a vê, e os registros são somente leitura (RN75, RN77).
    bool Ativo,
    // A aprovação está ativa para a equipe informada em ?equipeId= (RN33). Nulo sem equipeId; false nas globais.
    bool? RequerAprovacao,
    // O formulário de avaliação ativo na equipe informada em ?equipeId= (RN40). Nulo sem equipeId ou sem modelo ativo.
    FormularioAvaliacaoDto? ModeloAvaliacao,
    IReadOnlyList<CampoDto> Campos,
    IReadOnlyList<RestricaoUnicaDto> RestricoesUnicas,
    // Ordem no menu (a lista já vem nessa ordem).
    int Ordem,
    // Nome no plural, para títulos de listagem e menu. Nulo = use o Nome.
    string? NomePlural,
    // As equipes QUE O USUÁRIO VÊ em que a aprovação está ativa para esta entidade (RN33). Vazia nas globais. O front decide
    // por ela onde mostrar situação, filtros e o "Novo" do EXTERNO, sem uma chamada por equipe (ADR 0045).
    IReadOnlyList<Guid>? EquipesComAprovacao = null,
    // Os critérios ESTRELAS ativos dos modelos ativos nas equipes que o usuário vê (sem repetir a chave): as colunas, a ordenação
    // e o filtro por média da listagem (RN48). Vazia sem modelo ativo.
    IReadOnlyList<CriterioListagemDto>? CriteriosAvaliacao = null,
    // Ícone do PrimeIcons ("pi-chart-line"), para o menu, os títulos e os atalhos da página Início. Nulo = um ícone genérico (RN95).
    string? Icone = null);

/// <summary>Um critério de estrelas que a listagem pode mostrar, ordenar e filtrar pela média (RN48).</summary>
public sealed record CriterioListagemDto(string Chave, string Nome);

public sealed record CampoDto(
    Guid Id,
    string Chave,
    string Nome,
    // Texto igual ao do banco: TEXTO, TEXTO_LONGO, INTEIRO, DECIMAL, BOOLEANO, DATA, DATA_HORA, LISTA, REFERENCIA, URL.
    string TipoDado,
    bool Obrigatorio,
    int Ordem,
    int? TamanhoMaximo,
    byte? Precisao,
    byte? Escala,
    IReadOnlyList<string>? Opcoes,
    // Chave da entidade apontada (só REFERENCIA).
    string? EntidadeReferenciada,
    // RESTRINGIR ou CASCATA (só REFERENCIA).
    string? AcaoExclusao,
    // Filtro das opções deste campo (RN08). Nulo se o campo não depende de outro.
    DependenciaDto? Dependencia,
    // Chave do campo DO FORMULÁRIO cujo valor o front envia em "dependeDe" ao buscar as opções (RN21).
    // Nulo se o campo não depende de outro.
    string? DependeDe,
    // Sempre true aqui: campos inativos não aparecem nos metadados de uso (RN02). Existe para o front não precisar supor.
    bool Ativo);

/// <summary>
/// "As opções deste campo só mostram registros cujo <see cref="CampoAlvo"/> seja igual ao
/// <see cref="CampoOrigem"/> do registro escolhido em <see cref="CampoVia"/>"
/// (ou do próprio registro, se <see cref="CampoVia"/> for nulo). Valores são chaves de campo.
/// </summary>
public sealed record DependenciaDto(string? CampoVia, string CampoOrigem, string CampoAlvo);

public sealed record RestricaoUnicaDto(string Nome, IReadOnlyList<string> Campos);

/// <summary>Um grupo (listagem) para o menu do front. A <see cref="Chave"/> é a usada nas URLs.</summary>
/// <param name="MinhasEquipes">
/// "As equipes do usuário nesta listagem" (RN91): as equipes ATIVAS dele habilitadas nela; para o ADMINISTRADOR, todas as
/// habilitadas e ativas. É o que o front oferece no select de equipe, na coluna e no filtro de equipe.
/// </param>
/// <param name="PossuiEquipes">Há alguma equipe habilitada (RN93: sem nenhuma, só o ADMINISTRADOR vê a listagem).</param>
public sealed record GrupoDto(
    string Chave, string Nome, string? Descricao, bool Ativo, IReadOnlyList<EquipeResumoDto> MinhasEquipes, bool PossuiEquipes);
