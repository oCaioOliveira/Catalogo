using Catalogo.Domain.Aprovacao;

namespace Catalogo.Application.Abstracoes;

/// <summary>Configurações de uma entidade por equipe (RN33). Ausência de linha = tudo desativado.</summary>
public interface IConfiguracaoRepository
{
    /// <summary>Configuração para ALTERAÇÃO (gravada no SaveChanges), ou nula se não existe.</summary>
    Task<ConfiguracaoEntidadeEquipe?> ObterAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>A aprovação está ativa para a entidade nesta equipe?</summary>
    Task<bool> RequerAprovacaoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>
    /// A aprovação está ativa para a entidade em ALGUMA destas equipes? <paramref name="equipeIds"/> nulo = qualquer equipe.
    /// </summary>
    Task<bool> RequerAprovacaoEmAlgumaAsync(Guid entidadeId, IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken);

    /// <summary>Entidades com aprovação ativa nesta equipe, numa consulta só.</summary>
    Task<IReadOnlySet<Guid>> EntidadesComAprovacaoAsync(Guid equipeId, CancellationToken cancellationToken);

    /// <summary>
    /// Pares (entidade, equipe) com a aprovação ativa, nas equipes informadas (nulo = todas), numa consulta só. Usado pela lista
    /// de entidades, para o front saber em que equipes do usuário cada entidade tem aprovação (ADR 0045).
    /// </summary>
    Task<IReadOnlyList<(Guid EntidadeId, Guid EquipeId)>> AprovacoesAtivasAsync(
        IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken);

    void Adicionar(ConfiguracaoEntidadeEquipe configuracao);
}

/// <summary>Filtros da fila de aprovação (todos opcionais, combinados com E).</summary>
/// <param name="EquipesPermitidas">Escopo de segurança: só pendentes destas equipes. Nulo = todas (ADMINISTRADOR).</param>
/// <param name="IncluirListagensDesativadas">Falso: só listagens ativas (RN77). Em todo caso, só itens de equipes HABILITADAS na
/// listagem do item (RN90, RN91): um item oculto pela remoção da equipe (RN92) não aparece na fila.</param>
public sealed record FiltroPendentes(
    string? GrupoChave, string? EntidadeChave, Guid? EquipeId, IReadOnlySet<Guid>? EquipesPermitidas, bool IncluirListagensDesativadas);

/// <summary>Um registro pendente na fila de aprovação, com o grupo e a entidade dele.</summary>
public sealed record PendenteResumo(
    Guid RegistroId, string GrupoChave, string EntidadeChave, string EntidadeNome, Guid? EquipeId, Guid? CriadoPorId, DateTime CriadoEm);
