namespace Catalogo.Domain.Metadados;

/// <summary>
/// Agrupa as entidades de uma mesma listagem (tabela catalogo.Grupo), ex.: "testes_desempenho".
/// </summary>
public sealed class Grupo
{
    public required Guid Id { get; init; }

    /// <summary>Identificador usado na URL (ex.: "testes_desempenho").</summary>
    public required string Chave { get; init; }

    public required string Nome { get; init; }
    public string? Descricao { get; init; }

    /// <summary>Desativado: some para os não-administradores; as entidades ficam somente leitura (RN57).</summary>
    public bool Ativo { get; init; } = true;

    /// <summary>rowversion do grupo (Base64 na API): concorrência otimista na administração (RN52).</summary>
    public byte[] Versao { get; init; } = [];

    public required IReadOnlyList<Entidade> Entidades { get; init; }

    /// <summary>
    /// Equipes habilitadas nesta listagem (catalogo.GrupoEquipe, RN90): só os membros delas veem a listagem, e dentro dela
    /// "suas equipes" é só a interseção (RN91). Vazio = só o ADMINISTRADOR vê (listagem nova, RN93).
    /// </summary>
    public IReadOnlySet<Guid> EquipesHabilitadas { get; init; } = new HashSet<Guid>();

    /// <summary>
    /// Procura uma entidade pela chave da URL, sem diferenciar maiúsculas
    /// (igual à collation CI do banco, onde UQ_Entidade_Grupo_Chave também não diferencia).
    /// </summary>
    public Entidade? ObterEntidade(string chave) =>
        Entidades.FirstOrDefault(e => string.Equals(e.Chave, chave, StringComparison.OrdinalIgnoreCase));

    public Entidade? ObterEntidade(Guid entidadeId) => Entidades.FirstOrDefault(e => e.Id == entidadeId);
}
