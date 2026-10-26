using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Historico;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011).
internal sealed class HistoricoRepository(CatalogoDbContext contexto) : IHistoricoRepository
{
    public void Adicionar(HistoricoAlteracao historico) => contexto.HistoricosAlteracao.Add(historico);

    public async Task<PaginaDe<HistoricoAlteracao>> ListarAsync(
        AlvoHistorico alvoTipo, Guid alvoId, Paginacao paginacao, CancellationToken cancellationToken, Guid? entidadeId = null,
        string? dadosContendo = null)
    {
        var consulta = contexto.HistoricosAlteracao.AsNoTracking().Where(h => h.AlvoTipo == alvoTipo && h.AlvoId == alvoId);
        if (entidadeId is Guid entidade)
            consulta = consulta.Where(h => h.EntidadeId == entidade);
        // LINQ: vira LIKE com o texto como parâmetro (os caracteres especiais do LIKE são escapados pelo EF).
        if (dadosContendo is not null)
            consulta = consulta.Where(h => (h.DadosAntes != null && h.DadosAntes.Contains(dadosContendo))
                || (h.DadosDepois != null && h.DadosDepois.Contains(dadosContendo)));

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            // Mais recente primeiro; o Id desempata alterações no mesmo instante.
            .OrderByDescending(h => h.OcorridoEm).ThenByDescending(h => h.Id)
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .ToListAsync(cancellationToken);

        return new PaginaDe<HistoricoAlteracao>(itens, total);
    }

    public Task<HistoricoAlteracao?> ObterMaisRecenteAsync(AlvoHistorico alvoTipo, Guid alvoId, CancellationToken cancellationToken) =>
        contexto.HistoricosAlteracao.AsNoTracking()
            .Where(h => h.AlvoTipo == alvoTipo && h.AlvoId == alvoId)
            .OrderByDescending(h => h.OcorridoEm).ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
