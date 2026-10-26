using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Repositorios;

// Todas as consultas são LINQ do EF Core (docs/adr/0011). Médias e contagens são calculadas NO BANCO
// (GroupBy, Average, Count): as avaliações nunca vêm para a memória para isso (RN47, RN48).
internal sealed class AvaliacaoRepository(CatalogoDbContext contexto) : IAvaliacaoRepository
{
    public Task<Avaliacao?> ObterParaAlteracaoAsync(Guid registroId, Guid modeloId, Guid usuarioId, CancellationToken cancellationToken) =>
        contexto.Avaliacoes
            .Include(a => a.Respostas)
            .FirstOrDefaultAsync(
                a => a.RegistroId == registroId && a.ModeloAvaliacaoId == modeloId && a.UsuarioId == usuarioId, cancellationToken);

    public void DefinirVersaoEsperada(Avaliacao avaliacao, byte[] versao) =>
        contexto.Entry(avaliacao).Property(a => a.Versao).OriginalValue = versao;

    public void Adicionar(Avaliacao avaliacao) => contexto.Avaliacoes.Add(avaliacao);

    public void Remover(Avaliacao avaliacao) => contexto.Avaliacoes.Remove(avaliacao);

    public void RemoverResposta(AvaliacaoResposta resposta) => contexto.AvaliacoesRespostas.Remove(resposta);

    public async Task<IReadOnlyList<Guid>> ModelosAvaliadosAsync(Guid registroId, CancellationToken cancellationToken) =>
        await contexto.Avaliacoes
            .Where(a => a.RegistroId == registroId)
            .Select(a => a.ModeloAvaliacaoId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<ResumoNotas> ResumirAsync(Guid registroId, Guid modeloId, CancellationToken cancellationToken)
    {
        var avaliadores = await contexto.Avaliacoes
            .CountAsync(a => a.RegistroId == registroId && a.ModeloAvaliacaoId == modeloId, cancellationToken);

        // Notas dos critérios ESTRELAS ativos deste modelo neste registro.
        var notas =
            from resposta in contexto.AvaliacoesRespostas
            join avaliacao in contexto.Avaliacoes on resposta.AvaliacaoId equals avaliacao.Id
            join criterio in contexto.CriteriosAvaliacao on resposta.CriterioAvaliacaoId equals criterio.Id
            where avaliacao.RegistroId == registroId
                  && avaliacao.ModeloAvaliacaoId == modeloId
                  && criterio.Ativo
                  && criterio.Tipo == TipoCriterio.Estrelas
                  && resposta.Estrelas != null
            select new { resposta.CriterioAvaliacaoId, Estrelas = resposta.Estrelas!.Value };

        // AVG em tinyint seria inteiro no SQL Server: a conversão para double vira CAST(... AS float).
        var porCriterio = await notas
            .GroupBy(n => n.CriterioAvaliacaoId)
            .Select(g => new { CriterioId = g.Key, Media = g.Average(n => (double)n.Estrelas), Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        var distribuicao = await notas
            .GroupBy(n => new { n.CriterioAvaliacaoId, n.Estrelas })
            .Select(g => new { g.Key.CriterioAvaliacaoId, g.Key.Estrelas, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return new ResumoNotas(
            avaliadores,
            porCriterio
                .Select(c => new NotasCriterio(
                    c.CriterioId,
                    c.Media,
                    c.Quantidade,
                    distribuicao.Where(d => d.CriterioAvaliacaoId == c.CriterioId).ToDictionary(d => (int)d.Estrelas, d => d.Quantidade)))
                .ToList());
    }

    public async Task<PaginaDe<Avaliacao>> ListarDoRegistroAsync(Guid registroId, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var consulta = contexto.Avaliacoes.AsNoTracking().Where(a => a.RegistroId == registroId);

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .Include(a => a.Respostas)
            // Das mais recentes (criadas ou editadas) para as mais antigas; o Id desempata.
            .OrderByDescending(a => a.AtualizadoEm ?? a.CriadoEm).ThenByDescending(a => a.Id)
            .Skip(paginacao.Pular)
            .Take(paginacao.Tamanho)
            .ToListAsync(cancellationToken);

        return new PaginaDe<Avaliacao>(itens, total);
    }

    public async Task<IReadOnlyList<Avaliacao>> ListarDoUsuarioAsync(Guid registroId, Guid usuarioId, CancellationToken cancellationToken) =>
        await contexto.Avaliacoes.AsNoTracking()
            .Include(a => a.Respostas)
            .Where(a => a.RegistroId == registroId && a.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Avaliacao>> ListarDosRegistrosAsync(IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken)
    {
        if (registroIds.Count == 0)
            return [];

        return await contexto.Avaliacoes.AsNoTracking()
            .Include(a => a.Respostas)
            .Where(a => registroIds.Contains(a.RegistroId))
            .ToListAsync(cancellationToken);
    }

    public async Task<MediasDosRegistros> MediasAsync(
        IReadOnlyCollection<Guid> registroIds, IReadOnlyCollection<Guid> modeloIds, Guid usuarioId, CancellationToken cancellationToken)
    {
        if (registroIds.Count == 0 || modeloIds.Count == 0)
            return new MediasDosRegistros(new Dictionary<Guid, int>(), new Dictionary<(Guid, Guid), double>(), new HashSet<Guid>());

        var avaliacoes = contexto.Avaliacoes
            .Where(a => registroIds.Contains(a.RegistroId) && modeloIds.Contains(a.ModeloAvaliacaoId));

        var avaliadores = await avaliacoes
            .GroupBy(a => a.RegistroId)
            .Select(g => new { RegistroId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.RegistroId, x => x.Quantidade, cancellationToken);

        var medias = await (
                from resposta in contexto.AvaliacoesRespostas
                join avaliacao in avaliacoes on resposta.AvaliacaoId equals avaliacao.Id
                join criterio in contexto.CriteriosAvaliacao on resposta.CriterioAvaliacaoId equals criterio.Id
                where criterio.Ativo && criterio.Tipo == TipoCriterio.Estrelas && resposta.Estrelas != null
                group resposta by new { avaliacao.RegistroId, resposta.CriterioAvaliacaoId } into g
                select new { g.Key.RegistroId, g.Key.CriterioAvaliacaoId, Media = g.Average(r => (double)r.Estrelas!.Value) })
            .ToListAsync(cancellationToken);

        var doUsuario = await avaliacoes
            .Where(a => a.UsuarioId == usuarioId)
            .Select(a => a.RegistroId)
            .ToListAsync(cancellationToken);

        return new MediasDosRegistros(
            avaliadores,
            medias.ToDictionary(m => (m.RegistroId, m.CriterioAvaliacaoId), m => m.Media),
            doUsuario.ToHashSet());
    }
}
