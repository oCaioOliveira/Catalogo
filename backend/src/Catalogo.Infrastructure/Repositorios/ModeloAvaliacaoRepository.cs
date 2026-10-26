using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011).
internal sealed class ModeloAvaliacaoRepository(CatalogoDbContext contexto) : IModeloAvaliacaoRepository
{
    public Task<ModeloAvaliacao?> ObterAtivoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        contexto.ModelosAvaliacao.AsNoTracking()
            .Include(m => m.Criterios)
            .FirstOrDefaultAsync(m => m.EntidadeId == entidadeId && m.EquipeId == equipeId && m.Ativo, cancellationToken);

    public Task<ModeloAvaliacao?> ObterAtivoParaAlteracaoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        contexto.ModelosAvaliacao
            .Include(m => m.Criterios)
            .FirstOrDefaultAsync(m => m.EntidadeId == entidadeId && m.EquipeId == equipeId && m.Ativo, cancellationToken);

    public Task<ModeloAvaliacao?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken) =>
        contexto.ModelosAvaliacao
            .Include(m => m.Criterios)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ModeloAvaliacao>> ListarAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        await contexto.ModelosAvaliacao.AsNoTracking()
            .Include(m => m.Criterios)
            .Where(m => m.EntidadeId == entidadeId && m.EquipeId == equipeId)
            .OrderByDescending(m => m.CriadoEm).ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModeloAvaliacao>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return [];

        return await contexto.ModelosAvaliacao.AsNoTracking()
            .Include(m => m.Criterios)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModeloAvaliacao>> ListarAtivosAsync(
        Guid? entidadeId, IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken)
    {
        var consulta = contexto.ModelosAvaliacao.AsNoTracking().Include(m => m.Criterios).Where(m => m.Ativo);
        if (entidadeId is Guid entidade)
            consulta = consulta.Where(m => m.EntidadeId == entidade);
        if (equipeIds is not null)
            consulta = consulta.Where(m => equipeIds.Contains(m.EquipeId));

        return await consulta.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> CriteriosComRespostasAsync(Guid modeloId, CancellationToken cancellationToken) =>
        (await (
                from resposta in contexto.AvaliacoesRespostas
                join criterio in contexto.CriteriosAvaliacao on resposta.CriterioAvaliacaoId equals criterio.Id
                where criterio.ModeloAvaliacaoId == modeloId
                select resposta.CriterioAvaliacaoId)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public Task<int> ContarAvaliacoesAsync(Guid modeloId, CancellationToken cancellationToken) =>
        contexto.Avaliacoes.CountAsync(a => a.ModeloAvaliacaoId == modeloId, cancellationToken);

    public void DefinirVersaoEsperada(ModeloAvaliacao modelo, byte[] versao) =>
        // Como em RegistroRepository: o UPDATE sai com WHERE Versao = @versaoLida.
        contexto.Entry(modelo).Property(m => m.Versao).OriginalValue = versao;

    public void Adicionar(ModeloAvaliacao modelo) => contexto.ModelosAvaliacao.Add(modelo);

    public void RemoverCriterios(IEnumerable<CriterioAvaliacao> criterios) => contexto.CriteriosAvaliacao.RemoveRange(criterios);
}
