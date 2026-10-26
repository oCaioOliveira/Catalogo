namespace Catalogo.Application.Dtos;

// Respostas do painel gerencial (docs/api/painel.md). Só números e rótulos: nenhum registro inteiro sai daqui.

/// <summary>O período aplicado (RN80): datas no fuso de negócio, inclusivas. Nulo = sem limite daquele lado.</summary>
public sealed record PeriodoDto(DateOnly? De, DateOnly? Ate, string FusoHorario);

/// <summary>GET /api/v1/painel/grupos/{grupo}/visao-geral. Os blocos de aprovação e avaliação são nulos quando não estão ativos.</summary>
public sealed record VisaoGeralDto(
    string Grupo, PeriodoDto Periodo, Guid? EquipeId, VolumeDto Volume, AprovacaoPainelDto? Aprovacao, AvaliacaoPainelDto? Avaliacao,
    IReadOnlyList<AtividadeUsuarioDto> Atividade);

// ---- Volume (RN81) ----

public sealed record VolumeDto(
    int Total, int CriadosNoPeriodo, int AlteradosNoPeriodo, IReadOnlyList<VolumeEntidadeDto> PorEntidade,
    IReadOnlyList<QuantidadeEquipeDto> PorEquipe, IReadOnlyList<QuantidadeMesDto> CriadosPorMes);

public sealed record VolumeEntidadeDto(string Entidade, string Nome, int Total, int CriadosNoPeriodo, int AlteradosNoPeriodo);

public sealed record QuantidadeEquipeDto(Guid EquipeId, string? Nome, int Quantidade);

/// <param name="Mes">"aaaa-mm", no fuso de negócio.</param>
public sealed record QuantidadeMesDto(string Mes, int Quantidade);

// ---- Aprovação (RN82) ----

/// <param name="DiasPendenteAntigo">A partir de quantos dias um pendente é "antigo" ("Painel:DiasPendenteAntigo").</param>
public sealed record AprovacaoPainelDto(int DiasPendenteAntigo, NumerosAprovacaoDto Total, IReadOnlyList<AprovacaoEntidadeDto> PorEntidade);

public sealed record AprovacaoEntidadeDto(string Entidade, string Nome, NumerosAprovacaoDto Numeros);

/// <param name="TempoMedioPrimeiraDecisaoHoras">Média, em horas, de (primeira decisão − criação), das primeiras decisões do período.</param>
/// <param name="PrimeirasDecisoes">Quantos registros tiveram a primeira decisão no período (a base do tempo médio).</param>
/// <param name="TaxaRejeicao">rejeicoes / (aprovacoes + rejeicoes) das decisões do período, de 0 a 1. Nulo sem decisões.</param>
public sealed record NumerosAprovacaoDto(
    int Pendentes, int Aprovados, int Rejeitados, int PendentesAntigos, double? TempoMedioPrimeiraDecisaoHoras, int PrimeirasDecisoes,
    int Aprovacoes, int Rejeicoes, double? TaxaRejeicao);

// ---- Avaliação (RN83) ----

/// <param name="MinimoAvaliacoesRanking">Notas mínimas para um item entrar no ranking ("Painel:MinimoAvaliacoesRanking").</param>
public sealed record AvaliacaoPainelDto(int MinimoAvaliacoesRanking, IReadOnlyList<AvaliacaoEntidadeDto> PorEntidade);

public sealed record AvaliacaoEntidadeDto(string Entidade, string Nome, IReadOnlyList<AvaliacaoCriterioDto> Criterios);

/// <param name="Media">Média de todas as notas do critério (2 casas).</param>
/// <param name="Distribuicao">Quantas notas de cada valor: { "1": 0, "2": 1, "3": 4, "4": 10, "5": 7 }.</param>
/// <param name="Melhores">Os 5 itens de maior média (com o mínimo de notas).</param>
/// <param name="Piores">Os 5 itens de menor média (com o mínimo de notas).</param>
public sealed record AvaliacaoCriterioDto(
    string Criterio, string Nome, double? Media, int Quantidade, IReadOnlyDictionary<string, int> Distribuicao,
    IReadOnlyList<ItemRankingDto> Melhores, IReadOnlyList<ItemRankingDto> Piores);

/// <param name="Media">Média das notas do item no critério (2 casas).</param>
public sealed record ItemRankingDto(Guid Id, string? Rotulo, double Media, int Quantidade);

// ---- Atividade (RN84) ----

public sealed record AtividadeUsuarioDto(Guid UsuarioId, string Matricula, string? Nome, int Criacoes, int Alteracoes, int Total);

// ---- Agregação configurável (RN85 a RN87) ----

/// <summary>
/// GET .../entidades/{entidade}/indicadores. Cada linha é um objeto com uma propriedade por dimensão e por métrica (pela
/// chave), e sempre "contagem".
/// </summary>
public sealed record IndicadoresDto(
    IReadOnlyList<DimensaoIndicadorDto> Dimensoes, IReadOnlyList<MetricaIndicadorDto> Metricas,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Linhas, bool Truncado, PeriodoDto Periodo);

/// <param name="Tipo">LISTA, TEXTO, BOOLEANO, REFERENCIA, DATA, DATA_HORA, EQUIPE ou SITUACAO_APROVACAO.</param>
/// <param name="Periodo">dia, mes, trimestre ou ano nas dimensões de data; nulo nas demais.</param>
public sealed record DimensaoIndicadorDto(string Chave, string Nome, string Tipo, string? Periodo);

public sealed record MetricaIndicadorDto(string Chave, string Nome);

/// <summary>Valor de uma dimensão REFERENCIA ou "equipe" numa linha.</summary>
public sealed record ValorReferenciaDto(Guid Id, string? Rotulo);
