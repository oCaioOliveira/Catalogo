using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Painel;

namespace Catalogo.Application.Abstracoes;

/// <summary>
/// Consultas do painel gerencial (RN78 a RN88). TODAS agregam no banco, em LINQ (docs/adr/0031): o repositório devolve
/// só os números, nunca os registros. O <paramref name="escopo"/> de cada método é o mesmo da listagem (RN79), montado pelo
/// <see cref="Comum.PreparadorConsultaRegistros"/>: o painel não tem regra de visibilidade própria.
/// </summary>
public interface IPainelRepository
{
    /// <summary>RN81: total, por equipe, série de criados por mês (no fuso de negócio) e criados/alterados no período.</summary>
    Task<VolumeEntidade> ResumirVolumeAsync(Guid entidadeId, ConsultaRegistros escopo, PeriodoPainel periodo, CancellationToken cancellationToken);

    /// <summary>Pares (entidade, equipe) com aprovação ativa (RN33), só das entidades e equipes informadas (equipes nulo = todas).</summary>
    Task<IReadOnlyList<EntidadeEquipe>> ListarAprovacoesAtivasAsync(
        IReadOnlyCollection<Guid> entidadeIds, IReadOnlySet<Guid>? equipeIds, CancellationToken cancellationToken);

    /// <summary>RN82: por situação, pendentes antigos, tempo até a primeira decisão e decisões do período.</summary>
    /// <param name="pendenteCriadoAntesDe">Pendente criado antes disto (UTC) é "pendente antigo".</param>
    Task<AprovacaoEntidade> ResumirAprovacaoAsync(
        Guid entidadeId, ConsultaRegistros escopo, PeriodoPainel periodo, DateTime pendenteCriadoAntesDe, CancellationToken cancellationToken);

    /// <summary>RN83: por critério de estrelas (pela chave): média, quantidade e distribuição das notas, e o ranking dos itens.</summary>
    Task<IReadOnlyList<AvaliacaoCriterio>> ResumirAvaliacaoAsync(
        Guid entidadeId, ConsultaRegistros escopo, int minimoAvaliacoesRanking, int tamanhoRanking, CancellationToken cancellationToken);

    /// <summary>RN84: quem mais criou e alterou registros do grupo no período, pelo histórico.</summary>
    Task<IReadOnlyList<AtividadeUsuario>> ListarAtividadeAsync(
        FiltroAtividade filtro, PeriodoPainel periodo, int limite, CancellationToken cancellationToken);

    /// <summary>
    /// RN85 a RN87: a agregação configurável. <paramref name="consulta"/> é a consulta da listagem (filtros + escopo); o
    /// período filtra a data de criação. Devolve no máximo <see cref="ConsultaIndicadores.Limite"/> linhas.
    /// </summary>
    Task<ResultadoIndicadores> CalcularIndicadoresAsync(
        Guid entidadeId, ConsultaRegistros consulta, PeriodoPainel periodo, ConsultaIndicadores indicadores, CancellationToken cancellationToken);
}

public sealed record EntidadeEquipe(Guid EntidadeId, Guid EquipeId);

public sealed record QuantidadePorEquipe(Guid EquipeId, int Quantidade);

/// <summary>Mês no fuso de negócio.</summary>
public sealed record QuantidadePorMes(int Ano, int Mes, int Quantidade);

public sealed record VolumeEntidade(
    int Total, IReadOnlyList<QuantidadePorEquipe> PorEquipe, IReadOnlyList<QuantidadePorMes> CriadosPorMes,
    int CriadosNoPeriodo, int AlteradosNoPeriodo);

/// <param name="TempoMedioPrimeiraDecisaoSegundos">Nulo se nenhum registro teve a primeira decisão no período.</param>
/// <param name="PrimeirasDecisoes">Quantos registros tiveram a primeira decisão no período (a base do tempo médio).</param>
/// <param name="Aprovacoes">Decisões APROVACAO no período (todas, não só as primeiras).</param>
/// <param name="Rejeicoes">Decisões REJEICAO no período.</param>
public sealed record AprovacaoEntidade(
    IReadOnlyDictionary<SituacaoAprovacao, int> PorSituacao, int PendentesAntigos,
    double? TempoMedioPrimeiraDecisaoSegundos, int PrimeirasDecisoes, int Aprovacoes, int Rejeicoes);

/// <summary>Um item do ranking: a média das notas DELE no critério e quantas notas recebeu.</summary>
public sealed record ItemRanking(Guid RegistroId, double Media, int Quantidade);

/// <param name="Media">Média de TODAS as notas do critério (nulo sem notas).</param>
/// <param name="Quantidade">Quantas notas.</param>
/// <param name="Distribuicao">Quantas notas de 1, 2, 3, 4 e 5 estrelas (5 posições).</param>
public sealed record AvaliacaoCriterio(
    string Criterio, double? Media, int Quantidade, IReadOnlyList<int> Distribuicao,
    IReadOnlyList<ItemRanking> Melhores, IReadOnlyList<ItemRanking> Piores);

/// <summary>
/// De onde contar a atividade: entidades globais (todos os registros) e por equipe (só das <see cref="Equipes"/>; nulo = todas).
/// <see cref="FiltroEquipe"/> restringe as por equipe a uma equipe.
/// </summary>
public sealed record FiltroAtividade(
    IReadOnlyCollection<Guid> EntidadesGlobais, IReadOnlyCollection<Guid> EntidadesPorEquipe, IReadOnlySet<Guid>? Equipes, Guid? FiltroEquipe);

public sealed record AtividadeUsuario(Guid UsuarioId, string Matricula, int Criacoes, int Alteracoes);

/// <summary>
/// O valor de uma dimensão numa linha: só a propriedade do tipo da dimensão vem preenchida (<see cref="Id"/> para
/// REFERENCIA e equipe, <see cref="Texto"/> para LISTA e TEXTO, <see cref="Data"/> para os períodos: o primeiro dia dele).
/// Tudo nulo = registros sem valor. <see cref="Outros"/>: os valores de TEXTO fora dos mais frequentes (RN85).
/// </summary>
public sealed record ValorDimensao(
    Guid? Id = null, string? Texto = null, bool? Booleano = null, DateTime? Data = null, SituacaoAprovacao? Situacao = null, bool Outros = false);

/// <summary>Uma linha: um valor por dimensão, a contagem e um valor por métrica (na ordem pedida; nulo = sem valores).</summary>
public sealed record LinhaResultado(IReadOnlyList<ValorDimensao> Dimensoes, int Contagem, IReadOnlyList<decimal?> Metricas);

/// <param name="Truncado">Havia mais linhas que o limite.</param>
public sealed record ResultadoIndicadores(IReadOnlyList<LinhaResultado> Linhas, bool Truncado);
