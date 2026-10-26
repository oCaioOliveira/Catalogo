using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;

namespace Catalogo.Application.Tests.Dubles;

/// <summary>
/// "Banco" em memória dos modelos e avaliações, compartilhado pelos dois dublês abaixo. Como o EF, gera os
/// Ids ao adicionar; no "SaveChanges" (<see cref="AoSalvar"/>, chamado pela UnidadeDeTrabalhoFalsa) completa
/// Ids e FKs dos filhos novos e muda a Versao de quem foi alterado (como o rowversion).
/// </summary>
internal sealed class AvaliacoesEmMemoria
{
    private long _proximaVersao = 1000;

    public List<ModeloAvaliacao> Modelos { get; } = [];
    public List<Avaliacao> Avaliacoes { get; } = [];

    /// <summary>Objetos com versão esperada (alterados): ganham Versao nova ao salvar.</summary>
    public HashSet<object> Alterados { get; } = [];

    public byte[] NovaVersao() => BitConverter.GetBytes(_proximaVersao++);

    public void AoSalvar()
    {
        foreach (var modelo in Modelos)
        {
            foreach (var criterio in modelo.Criterios.Where(c => c.Id == Guid.Empty))
            {
                criterio.Id = Guid.NewGuid();
                criterio.ModeloAvaliacaoId = modelo.Id;
            }
        }

        foreach (var avaliacao in Avaliacoes)
        {
            foreach (var resposta in avaliacao.Respostas)
                resposta.AvaliacaoId = avaliacao.Id;
        }

        foreach (var alterado in Alterados)
        {
            switch (alterado)
            {
                case ModeloAvaliacao modelo: modelo.Versao = NovaVersao(); break;
                case Avaliacao avaliacao: avaliacao.Versao = NovaVersao(); break;
            }
        }

        Alterados.Clear();
    }
}

internal sealed class ModeloAvaliacaoRepositoryFalso(AvaliacoesEmMemoria banco) : IModeloAvaliacaoRepository
{
    public Task<ModeloAvaliacao?> ObterAtivoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(banco.Modelos.FirstOrDefault(m => m.EntidadeId == entidadeId && m.EquipeId == equipeId && m.Ativo));

    public Task<ModeloAvaliacao?> ObterAtivoParaAlteracaoAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        ObterAtivoAsync(entidadeId, equipeId, cancellationToken);

    public Task<ModeloAvaliacao?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(banco.Modelos.FirstOrDefault(m => m.Id == id));

    public Task<IReadOnlyList<ModeloAvaliacao>> ListarAsync(Guid entidadeId, Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModeloAvaliacao>>(
            banco.Modelos.Where(m => m.EntidadeId == entidadeId && m.EquipeId == equipeId).OrderByDescending(m => m.CriadoEm).ToList());

    public Task<IReadOnlyList<ModeloAvaliacao>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModeloAvaliacao>>(banco.Modelos.Where(m => ids.Contains(m.Id)).ToList());

    public Task<IReadOnlyList<ModeloAvaliacao>> ListarAtivosAsync(
        Guid? entidadeId, IReadOnlyCollection<Guid>? equipeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModeloAvaliacao>>(banco.Modelos
            .Where(m => m.Ativo && (entidadeId is null || m.EntidadeId == entidadeId) && (equipeIds is null || equipeIds.Contains(m.EquipeId)))
            .ToList());

    public Task<IReadOnlySet<Guid>> CriteriosComRespostasAsync(Guid modeloId, CancellationToken cancellationToken)
    {
        var doModelo = banco.Modelos.Single(m => m.Id == modeloId).Criterios.Select(c => c.Id).ToHashSet();
        return Task.FromResult<IReadOnlySet<Guid>>(banco.Avaliacoes
            .SelectMany(a => a.Respostas)
            .Select(r => r.CriterioAvaliacaoId)
            .Where(doModelo.Contains)
            .ToHashSet());
    }

    public Task<int> ContarAvaliacoesAsync(Guid modeloId, CancellationToken cancellationToken) =>
        Task.FromResult(banco.Avaliacoes.Count(a => a.ModeloAvaliacaoId == modeloId));

    public void DefinirVersaoEsperada(ModeloAvaliacao modelo, byte[] versao) => banco.Alterados.Add(modelo);

    public void Adicionar(ModeloAvaliacao modelo)
    {
        modelo.Id = Guid.NewGuid();
        modelo.CriadoEm = DateTime.UtcNow;
        modelo.Versao = banco.NovaVersao();
        foreach (var criterio in modelo.Criterios)
        {
            criterio.Id = Guid.NewGuid();
            criterio.ModeloAvaliacaoId = modelo.Id;
        }

        banco.Modelos.Add(modelo);
    }

    // Os critérios já saíram da lista do modelo (RegrasModeloAvaliacao.Aplicar).
    public void RemoverCriterios(IEnumerable<CriterioAvaliacao> criterios)
    {
    }
}

internal sealed class AvaliacaoRepositoryFalso(AvaliacoesEmMemoria banco) : IAvaliacaoRepository
{
    public Task<Avaliacao?> ObterParaAlteracaoAsync(Guid registroId, Guid modeloId, Guid usuarioId, CancellationToken cancellationToken) =>
        Task.FromResult(banco.Avaliacoes.FirstOrDefault(
            a => a.RegistroId == registroId && a.ModeloAvaliacaoId == modeloId && a.UsuarioId == usuarioId));

    public void DefinirVersaoEsperada(Avaliacao avaliacao, byte[] versao) => banco.Alterados.Add(avaliacao);

    public void Adicionar(Avaliacao avaliacao)
    {
        avaliacao.Id = Guid.NewGuid();
        avaliacao.CriadoEm = DateTime.UtcNow;
        avaliacao.Versao = banco.NovaVersao();
        banco.Avaliacoes.Add(avaliacao);
    }

    public void Remover(Avaliacao avaliacao) => banco.Avaliacoes.Remove(avaliacao);

    // A resposta já saiu da lista da avaliação (RegrasAvaliacao.AplicarRespostas).
    public void RemoverResposta(AvaliacaoResposta resposta)
    {
    }

    public Task<IReadOnlyList<Guid>> ModelosAvaliadosAsync(Guid registroId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            banco.Avaliacoes.Where(a => a.RegistroId == registroId).Select(a => a.ModeloAvaliacaoId).Distinct().ToList());

    public Task<ResumoNotas> ResumirAsync(Guid registroId, Guid modeloId, CancellationToken cancellationToken)
    {
        var avaliacoes = banco.Avaliacoes.Where(a => a.RegistroId == registroId && a.ModeloAvaliacaoId == modeloId).ToList();
        var criterios = CriteriosEstrelasAtivos();
        var notas = avaliacoes
            .SelectMany(a => a.Respostas)
            .Where(r => r.Estrelas is not null && criterios.Contains(r.CriterioAvaliacaoId))
            .GroupBy(r => r.CriterioAvaliacaoId)
            .Select(g => new NotasCriterio(
                g.Key,
                g.Average(r => (double)r.Estrelas!.Value),
                g.Count(),
                g.GroupBy(r => (int)r.Estrelas!.Value).ToDictionary(e => e.Key, e => e.Count())))
            .ToList();

        return Task.FromResult(new ResumoNotas(avaliacoes.Count, notas));
    }

    public Task<PaginaDe<Avaliacao>> ListarDoRegistroAsync(Guid registroId, Paginacao paginacao, CancellationToken cancellationToken)
    {
        var todas = banco.Avaliacoes
            .Where(a => a.RegistroId == registroId)
            .OrderByDescending(a => a.AtualizadoEm ?? a.CriadoEm)
            .ToList();
        return Task.FromResult(new PaginaDe<Avaliacao>(todas.Skip(paginacao.Pular).Take(paginacao.Tamanho).ToList(), todas.Count));
    }

    public Task<IReadOnlyList<Avaliacao>> ListarDoUsuarioAsync(Guid registroId, Guid usuarioId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Avaliacao>>(banco.Avaliacoes.Where(a => a.RegistroId == registroId && a.UsuarioId == usuarioId).ToList());

    public Task<IReadOnlyList<Avaliacao>> ListarDosRegistrosAsync(IReadOnlyCollection<Guid> registroIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Avaliacao>>(banco.Avaliacoes.Where(a => registroIds.Contains(a.RegistroId)).ToList());

    public Task<MediasDosRegistros> MediasAsync(
        IReadOnlyCollection<Guid> registroIds, IReadOnlyCollection<Guid> modeloIds, Guid usuarioId, CancellationToken cancellationToken)
    {
        var avaliacoes = banco.Avaliacoes.Where(a => registroIds.Contains(a.RegistroId) && modeloIds.Contains(a.ModeloAvaliacaoId)).ToList();
        var criterios = CriteriosEstrelasAtivos();
        var medias = avaliacoes
            .SelectMany(a => a.Respostas.Select(r => (a.RegistroId, Resposta: r)))
            .Where(x => x.Resposta.Estrelas is not null && criterios.Contains(x.Resposta.CriterioAvaliacaoId))
            .GroupBy(x => (x.RegistroId, x.Resposta.CriterioAvaliacaoId))
            .ToDictionary(g => g.Key, g => g.Average(x => (double)x.Resposta.Estrelas!.Value));

        return Task.FromResult(new MediasDosRegistros(
            avaliacoes.GroupBy(a => a.RegistroId).ToDictionary(g => g.Key, g => g.Count()),
            medias,
            avaliacoes.Where(a => a.UsuarioId == usuarioId).Select(a => a.RegistroId).ToHashSet()));
    }

    private HashSet<Guid> CriteriosEstrelasAtivos() =>
        banco.Modelos.SelectMany(m => m.Criterios).Where(c => c.Ativo && c.Tipo == TipoCriterio.Estrelas).Select(c => c.Id).ToHashSet();
}
