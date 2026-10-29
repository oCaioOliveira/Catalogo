using System.Text.Json.Nodes;

namespace Catalogo.Application.Dtos;

// Administração de metadados (/api/v1/admin/metadados, RN50 a RN77). Os de ENTRADA têm tudo anulável de propósito:
// a validação (com mensagens em português, todas juntas) é do Domain (RegrasMetadados). Referências a outros
// metadados são sempre pela CHAVE, que é imutável (RN51). A "versao" é a do grupo ou da entidade que se altera (RN52).

/// <summary>POST de grupo (sem versão) e PUT (com a versão do grupo). A chave, no PUT, só é conferida (RN51).</summary>
public sealed record GrupoEntrada(string? Versao, string? Chave, string? Nome, string? Descricao);

/// <summary>PATCH .../ativacao de grupo, entidade ou campo: { "versao": "...", "ativo": false }.</summary>
public sealed record AtivacaoMetadadoEntrada(string? Versao, bool? Ativo);

/// <summary>
/// POST de entidade (com a versão do GRUPO) e PUT (com a versão da ENTIDADE). <see cref="CampoExibicao"/> é a chave de um
/// campo da entidade (só no PUT: a entidade nova não tem campos). <see cref="NomePlural"/>: opcional (no PUT, ausente ou vazio
/// = sem plural, e as telas usam o nome). <see cref="Icone"/>: opcional, um ícone do PrimeIcons ("pi-chart-line"; no PUT,
/// ausente ou vazio = sem ícone, e as telas usam um genérico, RN95).
/// </summary>
public sealed record EntidadeEntrada(
    string? Versao, string? Chave, string? Nome, string? Descricao, int? Ordem, bool? EscopoEquipe, string? CampoExibicao,
    string? NomePlural = null, string? Icone = null);

/// <summary>
/// POST e PUT de campo, com a versão da ENTIDADE. <see cref="ValorPadrao"/>: valor que preenche os registros existentes ao
/// criar um campo obrigatório (RN60) ou ao tornar um campo obrigatório (RN61, só os vazios). No PUT, chave, tipo, entidade
/// referenciada, ação de exclusão e ordem ausentes = os atuais; os demais atributos são substituídos.
/// </summary>
public sealed record CampoEntrada(
    string? Versao,
    string? Chave,
    string? Nome,
    string? TipoDado,
    bool? Obrigatorio,
    int? TamanhoMaximo,
    int? Precisao,
    int? Escala,
    IReadOnlyList<string?>? Opcoes,
    string? EntidadeReferenciada,
    string? AcaoExclusao,
    int? Ordem,
    JsonNode? ValorPadrao);

/// <summary>PUT .../campos/ordem: TODAS as chaves dos campos (ativos e inativos), na ordem nova.</summary>
public sealed record OrdemCamposEntrada(string? Versao, IReadOnlyList<string?>? Campos);

/// <summary>POST de restrição única: nome (no padrão das chaves) e de 1 a 5 chaves de campos.</summary>
public sealed record RestricaoEntrada(string? Versao, string? Nome, IReadOnlyList<string?>? Campos);

/// <summary>PUT .../campos/{campo}/dependencia (RN08): chaves do via (opcional), da origem e do alvo.</summary>
public sealed record DependenciaEntrada(string? Versao, string? CampoVia, string? CampoOrigem, string? CampoAlvo);

/// <summary>
/// PUT .../grupos/{grupo}/equipes (RN90, RN92): a lista COMPLETA das equipes habilitadas (ids), com a versão do grupo. Lista
/// vazia = nenhuma equipe (só o administrador vê a listagem, RN93).
/// </summary>
public sealed record EquipesListagemEntrada(string? Versao, IReadOnlyList<string?>? Equipes);

/// <summary>
/// GET .../grupos/{grupo}/equipes: TODAS as equipes (ativas e inativas), cada uma dizendo se está habilitada na listagem e
/// quantos itens tem nela, e a versão do grupo para o PUT.
/// </summary>
public sealed record EquipesListagemDto(string Versao, IReadOnlyList<EquipeNaListagemDto> Equipes);

/// <summary>Uma equipe na aba "Equipes" de uma listagem. <see cref="QuantidadeItens"/>: itens dela nas entidades da listagem.</summary>
public sealed record EquipeNaListagemDto(Guid Id, string Nome, bool Ativa, bool Habilitada, int QuantidadeItens);

/// <summary>
/// Resposta de TODA escrita da administração.
///   - Gravada: <see cref="Permitido"/> true, <see cref="Simulado"/> false, os impactos (ex.: quantos registros receberam o
///     valor padrão) e o grupo como ficou (com as versões novas).
///   - Simulada (?simular=true, RN56): nada é gravado; <see cref="Permitido"/> diz se a mudança passaria, e
///     <see cref="Impactos"/> traz o MESMO relatório do bloqueio. <see cref="Grupo"/> vem nulo.
/// Bloqueada sem simulação: 409 com codigo ALTERACAO_INCOMPATIVEL_COM_DADOS (ou METADADO_EM_USO) e o relatório em detalhes.
/// </summary>
public sealed record ResultadoAdministracaoDto(bool Permitido, bool Simulado, IReadOnlyList<ImpactoDto> Impactos, GrupoAdminDto? Grupo);

/// <summary>
/// O efeito de uma mudança nos dados (RN58): quantos registros, até 10 exemplos e, em duplicatas, os grupos de repetidos.
/// <see cref="Bloqueia"/> false: só informa (ex.: registros que vão receber o valor padrão). <see cref="Usos"/>: onde um
/// campo ou entidade é usado (ex.: "restrição única 'uq_nome'"), quando é isso que impede a mudança.
/// </summary>
public sealed record ImpactoDto(
    string Tipo,
    string Descricao,
    bool Bloqueia,
    int Quantidade,
    IReadOnlyList<RegistroAfetadoDto> Exemplos,
    IReadOnlyList<IReadOnlyList<RegistroAfetadoDto>>? Grupos,
    IReadOnlyList<string>? Usos);

/// <summary>Um registro afetado: id e rótulo (RN20).</summary>
public sealed record RegistroAfetadoDto(Guid Id, string? Rotulo);

/// <summary>Um grupo na lista da administração (inclui os inativos), com quantos tipos de item e itens cadastrados tem.</summary>
/// <param name="QuantidadeEquipes">Equipes habilitadas (RN90). Zero = só o administrador vê a listagem (RN93).</param>
public sealed record GrupoResumoAdminDto(
    string Chave, string Nome, string? Descricao, bool Ativo, string Versao, int QuantidadeEntidades, int QuantidadeRegistros,
    int QuantidadeEquipes);

/// <summary>A estrutura completa de um grupo, como o administrador a edita: entidades e campos ATIVOS E INATIVOS.</summary>
/// <param name="QuantidadeEquipes">Equipes habilitadas (RN90). Zero = só o administrador vê a listagem (RN93).</param>
public sealed record GrupoAdminDto(
    string Chave, string Nome, string? Descricao, bool Ativo, string Versao, IReadOnlyList<EntidadeAdminDto> Entidades,
    int QuantidadeEquipes);

/// <summary>Uma entidade na administração. <see cref="Versao"/> é a que toda escrita na entidade (e nos seus campos) envia.</summary>
public sealed record EntidadeAdminDto(
    string Chave,
    string Nome,
    string? Descricao,
    int Ordem,
    bool EscopoEquipe,
    bool Ativo,
    string Versao,
    string? CampoExibicao,
    int QuantidadeRegistros,
    IReadOnlyList<CampoAdminDto> Campos,
    IReadOnlyList<RestricaoUnicaDto> RestricoesUnicas,
    // Nome no plural (nulo = sem plural).
    string? NomePlural,
    // Ícone do PrimeIcons (nulo = sem ícone, RN95).
    string? Icone = null);

/// <summary>Um campo na administração, ativo ou não, com a dependência (se tiver).</summary>
public sealed record CampoAdminDto(
    string Chave,
    string Nome,
    string TipoDado,
    bool Obrigatorio,
    int? TamanhoMaximo,
    byte? Precisao,
    byte? Escala,
    IReadOnlyList<string>? Opcoes,
    string? EntidadeReferenciada,
    string? AcaoExclusao,
    int Ordem,
    bool Ativo,
    DependenciaAdminDto? Dependencia);

/// <summary>A dependência de um campo (RN08), pelas chaves: o via e a origem na entidade do via (ou nesta), o alvo na referenciada.</summary>
public sealed record DependenciaAdminDto(string? CampoVia, string CampoOrigem, string CampoAlvo);
