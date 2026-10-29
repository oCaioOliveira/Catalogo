namespace Catalogo.Domain.Metadados.Administracao;

// O que chega da API para criar ou alterar metadados (RN50 a RN77), SEM validação: textos crus e tudo anulável.
// As regras de configuração ficam em RegrasMetadados; as que dependem dos dados, em PlanejadorAlteracoes.
// Referências a outros metadados são sempre pela CHAVE (que é imutável, RN51), nunca pelo id.

/// <summary>Um grupo (listagem). Na alteração, a chave é só conferida: não muda (RN51).</summary>
public sealed record DefinicaoGrupo(string? Chave, string? Nome, string? Descricao);

/// <summary>
/// Uma entidade (tipo de item). <see cref="CampoExibicao"/> é a chave de um campo da própria entidade. <see cref="NomePlural"/>:
/// opcional (vazio = sem plural). <see cref="Icone"/>: opcional, o nome de um ícone do PrimeIcons ("pi-chart-line").
/// </summary>
public sealed record DefinicaoEntidade(
    string? Chave, string? Nome, string? Descricao, int? Ordem, bool? EscopoEquipe, string? CampoExibicao, string? NomePlural = null,
    string? Icone = null);

/// <summary>
/// Um campo. <see cref="EntidadeReferenciada"/> é a chave de uma entidade do mesmo grupo (só REFERENCIA).
/// Na alteração, chave, tipo (salvo TEXTO → TEXTO_LONGO) e entidade referenciada não mudam (RN51, RN66).
/// </summary>
public sealed record DefinicaoCampo(
    string? Chave,
    string? Nome,
    string? TipoDado,
    bool? Obrigatorio = null,
    int? TamanhoMaximo = null,
    int? Precisao = null,
    int? Escala = null,
    IReadOnlyList<string?>? Opcoes = null,
    string? EntidadeReferenciada = null,
    string? AcaoExclusao = null,
    int? Ordem = null);

/// <summary>Uma restrição única: o nome e as chaves dos campos.</summary>
public sealed record DefinicaoRestricao(string? Nome, IReadOnlyList<string?>? Campos);

/// <summary>
/// A dependência de um campo REFERENCIA (RN08). Chaves de campo:
///   - <see cref="CampoVia"/>: REFERENCIA da própria entidade (opcional);
///   - <see cref="CampoOrigem"/>: campo da entidade apontada pelo via (ou da própria entidade, sem via);
///   - <see cref="CampoAlvo"/>: campo da entidade referenciada pelo campo dependente.
/// </summary>
public sealed record DefinicaoDependencia(string? CampoVia, string? CampoOrigem, string? CampoAlvo);

/// <summary>Uma entidade já validada e normalizada (sem os campos, que têm operações próprias).</summary>
public sealed record EntidadeDefinida(
    string Chave, string Nome, string? Descricao, int Ordem, bool EscopoEquipe, Guid? CampoExibicaoId, string? NomePlural = null,
    string? Icone = null);

/// <summary>Uma restrição única já validada: nome e campos, na ordem do hash (Campo.Ordem, depois Chave).</summary>
public sealed record RestricaoDefinida(string Nome, IReadOnlyList<Campo> Campos);
