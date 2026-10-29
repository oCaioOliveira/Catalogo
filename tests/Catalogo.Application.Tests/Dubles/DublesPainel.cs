using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Painel;

namespace Catalogo.Application.Tests.Dubles;

/// <summary>
/// Painel em memória: guarda o que o caso de uso PEDIU (entidade, escopo, período) e devolve números prontos. A conta de
/// verdade é do banco (testes de integração); aqui se testa o que a Application faz em volta dela.
/// </summary>
internal sealed class PainelRepositoryFalso : IPainelRepository
{
    public List<(Guid EntidadeId, ConsultaRegistros Escopo)> PedidosDeVolume { get; } = [];
    public List<(Guid EntidadeId, ConsultaRegistros Escopo)> PedidosDeAprovacao { get; } = [];
    public List<FiltroAtividade> PedidosDeAtividade { get; } = [];
    public List<(ConsultaRegistros Consulta, PeriodoPainel Periodo, ConsultaIndicadores Indicadores)> PedidosDeIndicadores { get; } = [];

    public List<EntidadeEquipe> AprovacoesAtivas { get; } = [];
    public Dictionary<Guid, VolumeEntidade> Volumes { get; } = [];
    public Dictionary<Guid, AprovacaoEntidade> Aprovacoes { get; } = [];
    public ResultadoIndicadores Indicadores { get; set; } = new([], false);

    public Task<VolumeEntidade> ResumirVolumeAsync(Guid entidadeId, ConsultaRegistros escopo, PeriodoPainel periodo, CancellationToken cancellationToken)
    {
        PedidosDeVolume.Add((entidadeId, escopo));
        return Task.FromResult(Volumes.GetValueOrDefault(entidadeId) ?? new VolumeEntidade(0, [], [], 0, 0));
    }

    public Task<IReadOnlyList<EntidadeEquipe>> ListarAprovacoesAtivasAsync(
        IReadOnlyCollection<Guid> entidadeIds, IReadOnlySet<Guid>? equipeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EntidadeEquipe>>(AprovacoesAtivas
            .Where(a => entidadeIds.Contains(a.EntidadeId) && (equipeIds is null || equipeIds.Contains(a.EquipeId)))
            .ToList());

    public Task<AprovacaoEntidade> ResumirAprovacaoAsync(
        Guid entidadeId, ConsultaRegistros escopo, PeriodoPainel periodo, DateTime pendenteCriadoAntesDe, CancellationToken cancellationToken)
    {
        PedidosDeAprovacao.Add((entidadeId, escopo));
        return Task.FromResult(Aprovacoes.GetValueOrDefault(entidadeId) ?? new AprovacaoEntidade(new Dictionary<Domain.Aprovacao.SituacaoAprovacao, int>(), 0, null, 0, 0, 0));
    }

    public Task<IReadOnlyList<AvaliacaoCriterio>> ResumirAvaliacaoAsync(
        Guid entidadeId, ConsultaRegistros escopo, int minimoAvaliacoesRanking, int tamanhoRanking, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AvaliacaoCriterio>>([]);

    public Task<IReadOnlyList<AtividadeUsuario>> ListarAtividadeAsync(
        FiltroAtividade filtro, PeriodoPainel periodo, int limite, CancellationToken cancellationToken)
    {
        PedidosDeAtividade.Add(filtro);
        return Task.FromResult<IReadOnlyList<AtividadeUsuario>>([]);
    }

    public Task<ResultadoIndicadores> CalcularIndicadoresAsync(
        Guid entidadeId, ConsultaRegistros consulta, PeriodoPainel periodo, ConsultaIndicadores indicadores, CancellationToken cancellationToken)
    {
        PedidosDeIndicadores.Add((consulta, periodo, indicadores));
        return Task.FromResult(Indicadores);
    }
}
