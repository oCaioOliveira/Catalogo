using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011).
internal sealed class EquipeRepository(CatalogoDbContext contexto) : IEquipeRepository
{
    public Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        contexto.Equipes.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Equipe>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return [];

        return await contexto.Equipes.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);
    }

    public async Task<PaginaDe<Equipe>> ListarAsync(bool? ativa, string? busca, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var consulta = contexto.Equipes.AsNoTracking();
        if (ativa is bool valor)
            consulta = consulta.Where(e => e.Ativa == valor);
        if (busca is not null)
            consulta = consulta.Where(e => e.Nome.Contains(busca));

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .OrderBy(e => e.Nome).ThenBy(e => e.Id)
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .ToListAsync(cancellationToken);
        return new PaginaDe<Equipe>(itens, total);
    }

    // A comparação segue a collation do banco (CI_AI), a mesma do UNIQUE do nome.
    public Task<bool> ExisteNomeAsync(string nome, Guid? ignorarEquipeId, CancellationToken cancellationToken) =>
        contexto.Equipes.AnyAsync(e => e.Nome == nome && (ignorarEquipeId == null || e.Id != ignorarEquipeId), cancellationToken);

    public Task<int> ContarUsuariosQueFicariamSemEquipeAsync(Guid equipeId, CancellationToken cancellationToken) =>
        contexto.Usuarios.CountAsync(
            u => u.Ativo
                 && u.Perfil != Perfil.Administrador
                 && u.Equipes.Any(v => v.EquipeId == equipeId)
                 && !u.Equipes.Any(v => v.EquipeId != equipeId && contexto.Equipes.Any(e => e.Id == v.EquipeId && e.Ativa)),
            cancellationToken);

    /// <summary>Membros ativos por equipe: um GROUP BY nos vínculos, só dos usuários ativos.</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> ContarMembrosAtivosAsync(
        IReadOnlyCollection<Guid> equipeIds, CancellationToken cancellationToken)
    {
        if (equipeIds.Count == 0)
            return new Dictionary<Guid, int>();

        var ids = equipeIds.ToList();
        return await contexto.Usuarios
            .Where(u => u.Ativo)
            .SelectMany(u => u.Equipes)
            .Where(v => ids.Contains(v.EquipeId))
            .GroupBy(v => v.EquipeId)
            .Select(g => new { EquipeId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.EquipeId, g => g.Quantidade, cancellationToken);
    }

    public void Adicionar(Equipe equipe) => contexto.Equipes.Add(equipe);

    public async Task<IReadOnlyList<Equipe>> ListarTodasAsync(CancellationToken cancellationToken) =>
        await contexto.Equipes.AsNoTracking().OrderBy(e => e.Nome).ThenBy(e => e.Id).ToListAsync(cancellationToken);

    /// <summary>Listagens habilitadas por equipe (RN90): um JOIN de GrupoEquipe com Grupo, numa consulta só.</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ListagemHabilitada>>> ListarListagensAsync(
        IReadOnlyCollection<Guid> equipeIds, CancellationToken cancellationToken)
    {
        if (equipeIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<ListagemHabilitada>>();

        var ids = equipeIds.ToList();
        var linhas = await (
                from vinculo in contexto.GruposEquipes
                join grupo in contexto.Grupos on vinculo.GrupoId equals grupo.Id
                where ids.Contains(vinculo.EquipeId)
                orderby grupo.Nome, grupo.Chave
                select new { vinculo.EquipeId, grupo.Chave, grupo.Nome, grupo.Ativo })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return linhas
            .GroupBy(l => l.EquipeId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ListagemHabilitada>)g.Select(l => new ListagemHabilitada(l.Chave, l.Nome, l.Ativo)).ToList());
    }
}
