using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Painel;

/// <summary>
/// Consultas do painel (RN81 a RN87). Todas são LINQ do EF Core e AGREGAM NO BANCO (docs/adr/0011 e 0031): cada método
/// começa da mesma consulta filtrada da listagem (<see cref="ConsultaRegistrosLinq.Filtrar"/>) e só traz números para a memória.
/// </summary>
internal sealed class PainelRepository(CatalogoDbContext contexto) : IPainelRepository
{
    // ---- Volume (RN81) ----

    public async Task<VolumeEntidade> ResumirVolumeAsync(
        Guid entidadeId, ConsultaRegistros escopo, PeriodoPainel periodo, CancellationToken cancellationToken)
    {
        var registros = ConsultaRegistrosLinq.Filtrar(contexto, entidadeId, escopo);
        var criados = ConsultaIndicadoresLinq.NoPeriodo(registros, periodo);

        var total = await registros.CountAsync(cancellationToken);

        var porEquipe = await registros
            .Where(r => r.EquipeId != null)
            .GroupBy(r => r.EquipeId!.Value)
            .Select(g => new QuantidadePorEquipe(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        // RN80: o mês é o do fuso de negócio: o deslocamento fixo vira DATEADD(minute, ...) antes do DATEPART.
        var minutos = periodo.Deslocamento.TotalMinutes;
        var porMes = await criados
            .Select(r => r.CriadoEm.AddMinutes(minutos))
            .GroupBy(d => new { d.Year, d.Month })
            .Select(g => new QuantidadePorMes(g.Key.Year, g.Key.Month, g.Count()))
            .ToListAsync(cancellationToken);

        // "Alterados" = registros com pelo menos uma ALTERACAO no histórico dentro do período (não só a última, que é AtualizadoEm).
        var alteracoes = NoPeriodo(Historico(entidadeId).Where(h => h.Operacao == OperacaoHistorico.Alteracao), periodo);
        var alterados = await registros.CountAsync(r => alteracoes.Any(h => h.AlvoId == r.Id), cancellationToken);

        return new VolumeEntidade(
            total,
            porEquipe.OrderByDescending(e => e.Quantidade).ToList(),
            porMes.OrderBy(m => m.Ano).ThenBy(m => m.Mes).ToList(),
            porMes.Sum(m => m.Quantidade),
            alterados);
    }

    // ---- Aprovação (RN82) ----

    public async Task<IReadOnlyList<EntidadeEquipe>> ListarAprovacoesAtivasAsync(
        IReadOnlyCollection<Guid> entidadeIds, IReadOnlySet<Guid>? equipeIds, CancellationToken cancellationToken)
    {
        var configuracoes = contexto.ConfiguracoesEntidadesEquipes.Where(c => c.RequerAprovacao && entidadeIds.Contains(c.EntidadeId));
        if (equipeIds is not null)
        {
            var equipes = equipeIds.ToList();
            configuracoes = configuracoes.Where(c => equipes.Contains(c.EquipeId));
        }

        return await configuracoes.Select(c => new EntidadeEquipe(c.EntidadeId, c.EquipeId)).ToListAsync(cancellationToken);
    }

    public async Task<AprovacaoEntidade> ResumirAprovacaoAsync(
        Guid entidadeId, ConsultaRegistros escopo, PeriodoPainel periodo, DateTime pendenteCriadoAntesDe, CancellationToken cancellationToken)
    {
        var registros = ConsultaRegistrosLinq.Filtrar(contexto, entidadeId, escopo);

        var porSituacao = await registros
            .GroupBy(r => r.SituacaoAprovacao)
            .Select(g => new { Situacao = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.Situacao, g => g.Quantidade, cancellationToken);

        var pendentesAntigos = await registros.CountAsync(
            r => r.SituacaoAprovacao == SituacaoAprovacao.Pendente && r.CriadoEm < pendenteCriadoAntesDe, cancellationToken);

        var decisoes = Historico(entidadeId).Where(h => h.Operacao == OperacaoHistorico.Aprovacao || h.Operacao == OperacaoHistorico.Rejeicao);

        // Tempo até a PRIMEIRA decisão: a menor data de APROVACAO/REJEICAO do registro, menos a criação. Conta para o
        // período a data dessa primeira decisão. JOIN com a tabela derivada (GROUP BY), porque AVG não aceita subconsulta.
        var primeiras = decisoes.GroupBy(h => h.AlvoId).Select(g => new { RegistroId = g.Key, Em = g.Min(h => h.OcorridoEm) });
        if (periodo.InicioUtc is DateTime inicio)
            primeiras = primeiras.Where(p => p.Em >= inicio);
        if (periodo.FimUtc is DateTime fim)
            primeiras = primeiras.Where(p => p.Em < fim);
        var tempos =
            from registro in registros
            join primeira in primeiras on registro.Id equals primeira.RegistroId
            select EF.Functions.DateDiffSecond(registro.CriadoEm, primeira.Em);
        var primeirasDecisoes = await tempos.CountAsync(cancellationToken);
        double? tempoMedio = primeirasDecisoes == 0 ? null : await tempos.AverageAsync(s => (double)s, cancellationToken);

        // Taxa de rejeição: TODAS as decisões do período (um registro rejeitado, corrigido e aprovado conta as duas).
        var porDecisao = await NoPeriodo(decisoes, periodo)
            .Where(h => registros.Any(r => r.Id == h.AlvoId))
            .GroupBy(h => h.Operacao)
            .Select(g => new { Operacao = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.Operacao, g => g.Quantidade, cancellationToken);

        return new AprovacaoEntidade(
            porSituacao, pendentesAntigos, tempoMedio, primeirasDecisoes,
            porDecisao.GetValueOrDefault(OperacaoHistorico.Aprovacao), porDecisao.GetValueOrDefault(OperacaoHistorico.Rejeicao));
    }

    // ---- Avaliação (RN83) ----

    public async Task<IReadOnlyList<AvaliacaoCriterio>> ResumirAvaliacaoAsync(
        Guid entidadeId, ConsultaRegistros escopo, int minimoAvaliacoesRanking, int tamanhoRanking, CancellationToken cancellationToken)
    {
        var registros = ConsultaRegistrosLinq.Filtrar(contexto, entidadeId, escopo);

        // As notas de estrelas dos modelos e critérios ATIVOS (como no filtro RN48), só dos registros que o usuário vê.
        var notas =
            from resposta in contexto.AvaliacoesRespostas
            join avaliacao in contexto.Avaliacoes on resposta.AvaliacaoId equals avaliacao.Id
            join criterio in contexto.CriteriosAvaliacao on resposta.CriterioAvaliacaoId equals criterio.Id
            join modelo in contexto.ModelosAvaliacao on criterio.ModeloAvaliacaoId equals modelo.Id
            where modelo.EntidadeId == entidadeId && modelo.Ativo && avaliacao.ModeloAvaliacaoId == modelo.Id
                  && criterio.Ativo && criterio.Tipo == TipoCriterio.Estrelas && resposta.Estrelas != null
                  && registros.Any(r => r.Id == avaliacao.RegistroId)
            select new { avaliacao.RegistroId, criterio.Chave, Estrelas = (int)resposta.Estrelas!.Value };

        var distribuicao = await notas
            .GroupBy(n => new { n.Chave, n.Estrelas })
            .Select(g => new { g.Key.Chave, g.Key.Estrelas, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        var resumos = new List<AvaliacaoCriterio>();
        foreach (var porCriterio in distribuicao.GroupBy(d => d.Chave).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            // Média e quantidade de TODAS as notas: saem da distribuição (já agregada no banco).
            var quantidade = porCriterio.Sum(d => d.Quantidade);
            var media = (double)porCriterio.Sum(d => d.Estrelas * d.Quantidade) / quantidade;
            var contagens = Enumerable.Range(1, 5).Select(e => porCriterio.Where(d => d.Estrelas == e).Sum(d => d.Quantidade)).ToList();

            // Ranking: a média de CADA item, só dos itens com o mínimo de notas.
            var chave = porCriterio.Key;
            var itens = notas
                .Where(n => n.Chave == chave)
                .GroupBy(n => n.RegistroId)
                .Select(g => new { RegistroId = g.Key, Media = g.Average(n => (double)n.Estrelas), Quantidade = g.Count() })
                .Where(i => i.Quantidade >= minimoAvaliacoesRanking);
            var melhores = await itens
                .OrderByDescending(i => i.Media).ThenByDescending(i => i.Quantidade).ThenBy(i => i.RegistroId)
                .Take(tamanhoRanking)
                .Select(i => new ItemRanking(i.RegistroId, i.Media, i.Quantidade))
                .ToListAsync(cancellationToken);
            var piores = await itens
                .OrderBy(i => i.Media).ThenByDescending(i => i.Quantidade).ThenBy(i => i.RegistroId)
                .Take(tamanhoRanking)
                .Select(i => new ItemRanking(i.RegistroId, i.Media, i.Quantidade))
                .ToListAsync(cancellationToken);

            resumos.Add(new AvaliacaoCriterio(chave, media, quantidade, contagens, melhores, piores));
        }

        return resumos;
    }

    // ---- Atividade (RN84) ----

    public async Task<IReadOnlyList<AtividadeUsuario>> ListarAtividadeAsync(
        FiltroAtividade filtro, PeriodoPainel periodo, int limite, CancellationToken cancellationToken)
    {
        var globais = filtro.EntidadesGlobais.ToList();
        var porEquipe = filtro.EntidadesPorEquipe.ToList();
        var historico = NoPeriodo(
            contexto.HistoricosAlteracao.Where(h => h.AlvoTipo == AlvoHistorico.Registro
                && (h.Operacao == OperacaoHistorico.Criacao || h.Operacao == OperacaoHistorico.Alteracao)),
            periodo);

        // Mesma visibilidade da listagem (RN79): entidades globais, todas; por equipe, só as equipes visíveis (e a filtrada).
        IQueryable<HistoricoAlteracao> dasEquipes = historico.Where(h => h.EntidadeId != null && porEquipe.Contains(h.EntidadeId.Value));
        if (filtro.Equipes is { } visiveis)
        {
            var equipes = visiveis.ToList();
            dasEquipes = dasEquipes.Where(h => h.EquipeId != null && equipes.Contains(h.EquipeId.Value));
        }

        if (filtro.FiltroEquipe is Guid equipeId)
            dasEquipes = dasEquipes.Where(h => h.EquipeId == equipeId);
        var visivel = historico
            .Where(h => h.EntidadeId != null && globais.Contains(h.EntidadeId.Value))
            .Concat(dasEquipes);

        return await visivel
            .GroupBy(h => h.UsuarioId)
            .Select(g => new
            {
                UsuarioId = g.Key,
                Matricula = g.Max(h => h.UsuarioMatricula)!,
                Criacoes = g.Count(h => h.Operacao == OperacaoHistorico.Criacao),
                Alteracoes = g.Count(h => h.Operacao == OperacaoHistorico.Alteracao),
            })
            .OrderByDescending(u => u.Criacoes + u.Alteracoes).ThenBy(u => u.Matricula)
            .Take(limite)
            .Select(u => new AtividadeUsuario(u.UsuarioId, u.Matricula, u.Criacoes, u.Alteracoes))
            .ToListAsync(cancellationToken);
    }

    // ---- Agregação configurável (RN85 a RN87) ----

    public async Task<ResultadoIndicadores> CalcularIndicadoresAsync(
        Guid entidadeId, ConsultaRegistros consulta, PeriodoPainel periodo, ConsultaIndicadores indicadores, CancellationToken cancellationToken)
    {
        var registros = ConsultaRegistrosLinq.Filtrar(contexto, entidadeId, consulta);
        var linhas = await ConsultaIndicadoresLinq.Montar(contexto, registros, periodo, indicadores).ToListAsync(cancellationToken);

        var truncado = linhas.Count > indicadores.Limite;
        return new ResultadoIndicadores(
            linhas.Take(indicadores.Limite).Select(l => ParaResultado(l, indicadores)).ToList(),
            truncado);
    }

    private static LinhaResultado ParaResultado(LinhaAgregada linha, ConsultaIndicadores indicadores)
    {
        var dimensoes = indicadores.Dimensoes
            .Select((_, i) => i == 0
                ? Valor(linha.Id1, linha.Texto1, linha.Booleano1, linha.Ano1, linha.Mes1, linha.Dia1, linha.Situacao1, linha.Outros1)
                : Valor(linha.Id2, linha.Texto2, linha.Booleano2, linha.Ano2, linha.Mes2, linha.Dia2, linha.Situacao2, linha.Outros2))
            .ToList();

        var metricas = indicadores.Metricas.Select((m, i) => m.Funcao switch
        {
            FuncaoMetrica.Contagem => linha.Contagem,
            FuncaoMetrica.MediaAvaliacao => (decimal?)Media(linha, i + 1),
            _ => Numero(linha, i + 1),
        }).ToList();

        return new LinhaResultado(dimensoes, linha.Contagem, metricas);
    }

    /// <summary>O período (ano, mês, dia) vira a data do primeiro dia dele; sem ano = registros sem data.</summary>
    private static ValorDimensao Valor(
        Guid? id, string? texto, bool? booleano, int? ano, int? mes, int? dia, SituacaoAprovacao? situacao, bool? outros) =>
        new(id, texto, booleano, ano is int a ? new DateTime(a, mes ?? 1, dia ?? 1) : null, situacao, outros == true);

    private static decimal? Numero(LinhaAgregada linha, int posicao) => posicao switch
    {
        1 => linha.Numero1,
        2 => linha.Numero2,
        3 => linha.Numero3,
        4 => linha.Numero4,
        _ => linha.Numero5,
    };

    private static double? Media(LinhaAgregada linha, int posicao) => posicao switch
    {
        1 => linha.Media1,
        2 => linha.Media2,
        3 => linha.Media3,
        4 => linha.Media4,
        _ => linha.Media5,
    };

    // ---- Apoio ----

    /// <summary>Histórico de registros da entidade.</summary>
    private IQueryable<HistoricoAlteracao> Historico(Guid entidadeId) =>
        contexto.HistoricosAlteracao.Where(h => h.AlvoTipo == AlvoHistorico.Registro && h.EntidadeId == entidadeId);

    private static IQueryable<HistoricoAlteracao> NoPeriodo(IQueryable<HistoricoAlteracao> historico, PeriodoPainel periodo)
    {
        if (periodo.InicioUtc is DateTime inicio)
            historico = historico.Where(h => h.OcorridoEm >= inicio);
        if (periodo.FimUtc is DateTime fim)
            historico = historico.Where(h => h.OcorridoEm < fim);
        return historico;
    }
}
