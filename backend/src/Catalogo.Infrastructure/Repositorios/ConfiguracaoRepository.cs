using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Aprovacao;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011).
internal sealed class ConfiguracaoRepository(CatalogoDbContext contexto) : IConfiguracaoRepository
{
    public Task<ConfiguracaoEntidadeEquipe?> ObterAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        contexto.ConfiguracoesEntidadesEquipes.FirstOrDefaultAsync(c => c.EntidadeId == entidadeId && c.EquipeId == equipeId, cancellationToken);

    public Task<bool> RequerAprovacaoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        contexto.ConfiguracoesEntidadesEquipes.AnyAsync(
            c => c.EntidadeId == entidadeId && c.EquipeId == equipeId && c.RequerAprovacao, cancellationToken);

    public Task<bool> RequerAprovacaoEmAlgumaAsync(Guid entidadeId, IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken)
    {
        var consulta = contexto.ConfiguracoesEntidadesEquipes.Where(c => c.EntidadeId == entidadeId && c.RequerAprovacao);
        if (equipeIds is not null)
            consulta = consulta.Where(c => equipeIds.Contains(c.EquipeId));

        return consulta.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> EntidadesComAprovacaoAsync(Guid equipeId, CancellationToken cancellationToken) =>
        (await contexto.ConfiguracoesEntidadesEquipes
            .Where(c => c.EquipeId == equipeId && c.RequerAprovacao)
            .Select(c => c.EntidadeId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<IReadOnlyList<(Guid EntidadeId, Guid EquipeId)>> AprovacoesAtivasAsync(
        IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken)
    {
        var consulta = contexto.ConfiguracoesEntidadesEquipes.AsNoTracking().Where(c => c.RequerAprovacao);
        if (equipeIds is not null)
            consulta = consulta.Where(c => equipeIds.Contains(c.EquipeId));

        return (await consulta.Select(c => new { c.EntidadeId, c.EquipeId }).ToListAsync(cancellationToken))
            .Select(c => (c.EntidadeId, c.EquipeId))
            .ToList();
    }

    public void Adicionar(ConfiguracaoEntidadeEquipe configuracao) => contexto.ConfiguracoesEntidadesEquipes.Add(configuracao);
}
