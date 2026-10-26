using Catalogo.Domain.Metadados;

namespace Catalogo.Application.Abstracoes;

/// <summary>
/// Fornece os metadados (grupo, entidades, campos, restrições, dependências).
/// Implementado na Infrastructure com cache em memória: os objetos devolvidos são
/// compartilhados entre requisições e NÃO podem ser alterados.
/// </summary>
public interface IMetadadosProvider
{
    /// <summary>
    /// Grupo com todas as suas entidades, campos (ativos e inativos), restrições únicas e
    /// dependências. Nulo se o grupo não existe. A chave não diferencia maiúsculas.
    /// </summary>
    Task<Grupo?> ObterGrupoAsync(string grupoChave, CancellationToken cancellationToken);

    /// <summary>Todos os grupos (listagens), por nome, sem as entidades. Para o menu do front.</summary>
    Task<IReadOnlyList<ResumoGrupo>> ListarGruposAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Descarta o cache de um grupo. Chame sempre que os metadados desse grupo forem alterados,
    /// senão a aplicação continua usando a versão antiga até o cache expirar.
    /// </summary>
    void InvalidarCache(string grupoChave);
}

/// <summary>Um grupo sem as entidades (ver <see cref="IMetadadosProvider.ListarGruposAsync"/>), com as equipes habilitadas (RN90).</summary>
public sealed record ResumoGrupo(string Chave, string Nome, string? Descricao, bool Ativo, IReadOnlySet<Guid> EquipesHabilitadas);
