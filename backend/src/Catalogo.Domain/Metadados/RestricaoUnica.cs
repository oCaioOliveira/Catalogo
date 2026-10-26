namespace Catalogo.Domain.Metadados;

/// <summary>
/// Regra de unicidade de uma entidade: simples (1 campo) ou composta (vários campos).
/// Tabelas catalogo.RestricaoUnica + catalogo.RestricaoUnicaCampo. Ver RN09 em docs/regras-de-negocio.md.
/// </summary>
public sealed class RestricaoUnica
{
    public required Guid Id { get; init; }
    public required Guid EntidadeId { get; init; }

    /// <summary>Nome técnico da restrição (ex.: "uq_projeto_sigla_nome").</summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Campos da restrição, JÁ ORDENADOS por Campo.Ordem (depois por Chave).
    /// A tabela RestricaoUnicaCampo não tem coluna de ordem, então essa ordem
    /// é a usada para montar o hash. Não mude sem migrar os hashes já gravados.
    /// </summary>
    public required IReadOnlyList<Campo> Campos { get; init; }
}
