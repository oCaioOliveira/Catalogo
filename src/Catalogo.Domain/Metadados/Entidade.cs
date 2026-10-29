namespace Catalogo.Domain.Metadados;

/// <summary>
/// Um "tipo de item" cadastrável (tabela catalogo.Entidade), ex.: comunidade, projeto, cenario_teste.
/// Modelo de leitura imutável (ver <see cref="Campo"/>).
/// </summary>
public sealed class Entidade
{
    public required Guid Id { get; init; }
    public required Guid GrupoId { get; init; }

    /// <summary>Identificador usado na URL (ex.: "cenario_teste").</summary>
    public required string Chave { get; init; }

    public required string Nome { get; init; }
    public string? Descricao { get; init; }

    /// <summary>Nome no plural, para títulos de listagem e menu ("Relatórios de Teste"). Nulo: as telas usam <see cref="Nome"/>.</summary>
    public string? NomePlural { get; init; }

    /// <summary>Nome de um ícone do PrimeIcons ("pi-chart-line"), mostrado nas telas. Nulo: as telas usam um ícone genérico.</summary>
    public string? Icone { get; init; }

    /// <summary>
    /// Verdadeiro (padrão): cada registro pertence a uma equipe. Falso: registros GLOBAIS, vistos por
    /// todos e alterados só pelo administrador (RN27, docs/adr/0017).
    /// </summary>
    public bool EscopoEquipe { get; init; } = true;

    public bool EhGlobal => !EscopoEquipe;

    /// <summary>Campo usado como rótulo do registro em selects e listas.</summary>
    public Guid? CampoExibicaoId { get; init; }

    /// <summary>
    /// Desativada: some para os não-administradores (404) e fica somente leitura para o administrador (RN57).
    /// O grupo desativado tem o mesmo efeito: use <see cref="Grupo"/>.Ativo junto.
    /// </summary>
    public bool Ativo { get; init; } = true;

    /// <summary>Ordem no menu.</summary>
    public int Ordem { get; init; }

    /// <summary>
    /// rowversion da entidade (Base64 na API). Muda em toda alteração da entidade E dos seus campos, restrições
    /// e dependências: é a versão da estrutura inteira (RN52).
    /// </summary>
    public byte[] Versao { get; init; } = [];

    /// <summary>Todos os campos, ativos e inativos, ordenados por <see cref="Campo.Ordem"/>.</summary>
    public required IReadOnlyList<Campo> Campos { get; init; }

    public IReadOnlyList<RestricaoUnica> RestricoesUnicas { get; init; } = [];

    /// <summary>Dependências dos campos desta entidade (Dependencia.Campo é sempre desta entidade).</summary>
    public IReadOnlyList<CampoDependencia> Dependencias { get; init; } = [];

    /// <summary>
    /// Procura um campo pela chave. A comparação é exata (diferencia maiúsculas):
    /// a chave enviada no JSON tem que ser igual à cadastrada.
    /// </summary>
    public Campo? ObterCampo(string chave) =>
        Campos.FirstOrDefault(c => string.Equals(c.Chave, chave, StringComparison.Ordinal));

    public Campo? ObterCampo(Guid campoId) => Campos.FirstOrDefault(c => c.Id == campoId);
}
