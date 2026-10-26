using Catalogo.Application.Dtos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.Abstracoes;

/// <summary>Acesso aos usuários. Só os métodos que os casos de uso usam.</summary>
public interface IUsuarioRepository
{
    /// <summary>Usuário com os vínculos de equipe, para LEITURA ou ALTERAÇÃO (gravado no SaveChanges).</summary>
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Usuário pela matrícula (sem diferenciar maiúsculas), para alteração.</summary>
    Task<Usuario?> ObterPorMatriculaAsync(string matricula, CancellationToken cancellationToken);

    Task<bool> ExisteMatriculaAsync(string matricula, CancellationToken cancellationToken);

    /// <summary>Quantos administradores ativos existem (RN30).</summary>
    Task<int> ContarAdministradoresAtivosAsync(CancellationToken cancellationToken);

    /// <summary>Usuários filtrados, ordenados por nome, com os vínculos de equipe.</summary>
    Task<PaginaDe<Usuario>> ListarAsync(FiltroUsuarios filtro, Paginacao paginacao, CancellationToken cancellationToken);

    /// <summary>Id, matrícula e nome de vários usuários, numa consulta só.</summary>
    Task<IReadOnlyDictionary<Guid, UsuarioResumoDto>> ObterResumosAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Adicionar(Usuario usuario);
}

/// <summary>Filtros da listagem de usuários (todos opcionais, combinados com E).</summary>
/// <param name="Busca">Contido na matrícula OU no nome, sem diferenciar maiúsculas nem acentos (collation do banco).</param>
/// <param name="Ordenacao">Nula = por nome, crescente.</param>
public sealed record FiltroUsuarios(
    Guid? EquipeId, Perfil? Perfil, bool? Ativo, string? Busca = null, OrdenacaoUsuarios? Ordenacao = null);

/// <summary>Colunas pelas quais a listagem de usuários pode ser ordenada.</summary>
public enum CampoOrdenacaoUsuario
{
    Nome,
    Matricula,
    UltimoAcessoEm,
    CriadoEm,
}

/// <summary>Ordenação da listagem de usuários ("ordenacao=-ultimoAcessoEm"). Desempate sempre pelo id.</summary>
public sealed record OrdenacaoUsuarios(CampoOrdenacaoUsuario Campo, bool Descendente);

/// <summary>Acesso às equipes.</summary>
public interface IEquipeRepository
{
    /// <summary>Equipe para leitura ou alteração (gravada no SaveChanges).</summary>
    Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Equipe>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Uma página de equipes, ordenadas por nome, e o total. <paramref name="ativa"/> nulo = todas; <paramref name="busca"/>
    /// contido no nome (sem diferenciar maiúsculas nem acentos), nulo = todas.
    /// </summary>
    Task<PaginaDe<Equipe>> ListarAsync(bool? ativa, string? busca, Paginacao paginacao, CancellationToken cancellationToken);

    /// <summary>Quantos usuários ATIVOS pertencem a cada equipe (contagem no banco). Equipe sem membros: 0.</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarMembrosAtivosAsync(IReadOnlyCollection<Guid> equipeIds, CancellationToken cancellationToken);

    /// <summary>Nome já usado (sem diferenciar maiúsculas nem acentos, pela collation)? Ignora a própria equipe.</summary>
    Task<bool> ExisteNomeAsync(string nome, Guid? ignorarEquipeId, CancellationToken cancellationToken);

    /// <summary>
    /// Usuários ativos não-administradores para quem esta é a ÚNICA equipe ativa: ficariam sem equipe
    /// se ela fosse desativada (RN31).
    /// </summary>
    Task<int> ContarUsuariosQueFicariamSemEquipeAsync(Guid equipeId, CancellationToken cancellationToken);

    void Adicionar(Equipe equipe);

    /// <summary>Todas as equipes, ativas e inativas, por nome (a aba "Equipes" de uma listagem, RN90).</summary>
    Task<IReadOnlyList<Equipe>> ListarTodasAsync(CancellationToken cancellationToken);

    /// <summary>As listagens (grupos) em que cada equipe está habilitada (RN90), por nome. Equipe sem nenhuma: não aparece.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ListagemHabilitada>>> ListarListagensAsync(
        IReadOnlyCollection<Guid> equipeIds, CancellationToken cancellationToken);
}

/// <summary>Uma listagem (grupo) em que uma equipe está habilitada (RN90).</summary>
public sealed record ListagemHabilitada(string Chave, string Nome, bool Ativo);

/// <summary>Acesso ao histórico de alterações (RN32).</summary>
public interface IHistoricoRepository
{
    /// <summary>Marca a linha para gravação no MESMO SaveChanges da operação.</summary>
    void Adicionar(HistoricoAlteracao historico);

    /// <summary>
    /// Histórico de um alvo, do mais recente para o mais antigo. <paramref name="entidadeId"/> restringe
    /// a uma entidade (usado nas configurações, cujo alvo é a equipe). <paramref name="dadosContendo"/>: só as linhas cujo antes ou
    /// depois contém o texto (usado no histórico de metadados de UMA entidade, cujo alvo é o grupo).
    /// </summary>
    Task<PaginaDe<HistoricoAlteracao>> ListarAsync(
        AlvoHistorico alvoTipo, Guid alvoId, Paginacao paginacao, CancellationToken cancellationToken, Guid? entidadeId = null,
        string? dadosContendo = null);

    /// <summary>Linha mais recente de um alvo (ex.: para saber a equipe de um registro já excluído).</summary>
    Task<HistoricoAlteracao?> ObterMaisRecenteAsync(AlvoHistorico alvoTipo, Guid alvoId, CancellationToken cancellationToken);
}

/// <summary>Uma página de itens e o total.</summary>
public sealed record PaginaDe<T>(IReadOnlyList<T> Itens, int Total);
