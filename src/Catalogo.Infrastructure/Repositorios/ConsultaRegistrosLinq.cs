using System.Linq.Expressions;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Avaliacoes;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;

namespace Catalogo.Infrastructure.Repositorios;

/// <summary>
/// Traduz uma <see cref="ConsultaRegistros"/> (já validada no Domain) para LINQ sobre Registro, com
/// subconsultas em RegistroValor e RegistroReferencia (docs/adr/0011 e 0013). Nenhum texto da URL
/// vira SQL: os CampoId vêm dos metadados e os valores viram parâmetros do EF Core.
/// </summary>
internal static class ConsultaRegistrosLinq
{
    /// <summary>
    /// Registros da entidade que passam em TODAS as condições (E), sempre dentro do escopo de
    /// equipes de <see cref="ConsultaRegistros.EquipesPermitidas"/> (RN26). O escopo é aplicado AQUI,
    /// no WHERE, e nunca filtrando em memória depois.
    /// </summary>
    public static IQueryable<Registro> Filtrar(CatalogoDbContext contexto, Guid entidadeId, ConsultaRegistros consultaRegistros)
    {
        var consulta = contexto.Registros.Where(r => r.EntidadeId == entidadeId);

        if (consultaRegistros.EquipesPermitidas is { } permitidas)
        {
            var ids = permitidas.ToList();
            consulta = consulta.Where(r => r.EquipeId != null && ids.Contains(r.EquipeId.Value));
        }

        if (consultaRegistros.FiltroEquipe is Guid equipeId)
            consulta = consulta.Where(r => r.EquipeId == equipeId);

        // RN37/RN38: não aprovados só do autor indicado. Também no WHERE, nunca em memória.
        if (consultaRegistros.NaoAprovadosSomenteDoAutor is Guid autor)
            consulta = consulta.Where(r => r.SituacaoAprovacao == SituacaoAprovacao.Aprovado || r.CriadoPorId == autor);

        if (consultaRegistros.FiltroSituacao is SituacaoAprovacao situacao)
            consulta = consulta.Where(r => r.SituacaoAprovacao == situacao);

        foreach (var condicao in consultaRegistros.Condicoes)
            consulta = AplicarCondicao(contexto, consulta, condicao);

        foreach (var condicao in consultaRegistros.CondicoesAvaliacao ?? [])
            consulta = AplicarCondicaoAvaliacao(contexto, consulta, condicao);

        return consulta;
    }

    /// <summary>
    /// RN48: média do critério no registro (modelo ativo, critério ativo) comparada com o limite. A média é
    /// uma subconsulta correlacionada com AVG no banco; registro sem nota tem média NULL e não passa.
    /// </summary>
    private static IQueryable<Registro> AplicarCondicaoAvaliacao(
        CatalogoDbContext contexto, IQueryable<Registro> consulta, CondicaoAvaliacao condicao)
    {
        var notas = NotasDoCriterio(contexto, condicao.Criterio);
        var limite = (double)condicao.Valor;

        return condicao.Comparacao == Comparacao.MaiorOuIgual
            ? consulta.Where(r => notas.Where(n => n.RegistroId == r.Id).Average(n => (double?)n.Estrelas) >= limite)
            : consulta.Where(r => notas.Where(n => n.RegistroId == r.Id).Average(n => (double?)n.Estrelas) < limite);
    }

    /// <summary>
    /// Notas (estrelas) dadas a um critério, pela chave, nos modelos ATIVOS, com o registro avaliado. O modelo
    /// ativo de cada registro é o da equipe dele: a avaliação só existe nesse modelo.
    /// </summary>
    internal static IQueryable<NotaDoRegistro> NotasDoCriterio(CatalogoDbContext contexto, string chaveCriterio) =>
        from resposta in contexto.AvaliacoesRespostas
        join avaliacao in contexto.Avaliacoes on resposta.AvaliacaoId equals avaliacao.Id
        join criterio in contexto.CriteriosAvaliacao on resposta.CriterioAvaliacaoId equals criterio.Id
        join modelo in contexto.ModelosAvaliacao on avaliacao.ModeloAvaliacaoId equals modelo.Id
        where criterio.ModeloAvaliacaoId == modelo.Id
              && modelo.Ativo
              && criterio.Ativo
              && criterio.Chave == chaveCriterio
              && criterio.Tipo == TipoCriterio.Estrelas
              && resposta.Estrelas != null
        // AVG de tinyint seria inteiro no SQL Server: a conversão vira CAST(... AS float).
        select new NotaDoRegistro { RegistroId = avaliacao.RegistroId, Estrelas = (double)resposta.Estrelas!.Value };

    /// <summary>Uma nota com o registro avaliado (projeção usada só dentro das consultas acima).</summary>
    internal sealed class NotaDoRegistro
    {
        public Guid RegistroId { get; init; }
        public double Estrelas { get; init; }
    }

    /// <summary>Ordena pela ordenação pedida e, para desempatar de forma estável, pelo Id.</summary>
    public static IQueryable<Registro> Ordenar(CatalogoDbContext contexto, IQueryable<Registro> consulta, Ordenacao ordenacao)
    {
        var descendente = ordenacao.Descendente;

        IOrderedQueryable<Registro> ordenada = ordenacao switch
        {
            { CriterioAvaliacao: string criterio } => OrdenarPorAvaliacao(contexto, consulta, criterio, descendente),
            { Campo: Campo referencia, CampoExibicaoReferenciada: Campo exibicao } =>
                OrdenarPorReferencia(contexto, consulta, referencia, exibicao, descendente),
            { Campo: Campo campo } => OrdenarPorCampo(contexto, consulta, campo, descendente),
            { CampoSistema: CampoSistema.AtualizadoEm } => OrdenarPor(consulta, r => r.AtualizadoEm, descendente),
            _ => OrdenarPor(consulta, r => r.CriadoEm, descendente),
        };

        return ordenada.ThenBy(r => r.Id);
    }

    private static IQueryable<Registro> AplicarCondicao(CatalogoDbContext contexto, IQueryable<Registro> consulta, CondicaoFiltro condicao)
    {
        if (condicao.Saltos is { Count: > 0 } saltos)
            return AplicarCondicaoPorCaminho(contexto, consulta, condicao, saltos);

        var campoId = condicao.Campo.Id;

        // RN18: filtro por REFERENCIA usa RegistroReferencia, nunca o JSON.
        if (condicao.Campo.EhReferencia)
        {
            var destinoId = condicao.Referencia!.Value;
            return consulta.Where(r => contexto.RegistrosReferencias
                .Any(rr => rr.RegistroOrigemId == r.Id && rr.CampoId == campoId && rr.RegistroDestinoId == destinoId));
        }

        // Os demais tipos usam a coluna tipada de RegistroValor. A subconsulta abaixo é montada
        // separadamente e o EF Core a incorpora no WHERE EXISTS (...) da consulta principal.
        var valores = FiltrarValores(contexto.RegistrosValores.Where(v => v.CampoId == campoId), condicao);
        return consulta.Where(r => valores.Any(v => v.RegistroId == r.Id));
    }

    /// <summary>
    /// RN89: filtro num campo de um registro APONTADO ("relatorio_teste.tipo_teste=Carga" em cenario_teste). Primeiro, os registros
    /// do fim do caminho que passam na condição; depois, um JOIN com RegistroReferencia por salto, do último para o primeiro, até os
    /// registros consultados. Fica tudo num "Id IN (SELECT ...)" do mesmo SQL. O escopo de equipes continua sendo o dos registros
    /// consultados (no WHERE de <see cref="Filtrar"/>); os apontados são da mesma equipe ou globais (RN29).
    /// </summary>
    private static IQueryable<Registro> AplicarCondicaoPorCaminho(
        CatalogoDbContext contexto, IQueryable<Registro> consulta, CondicaoFiltro condicao, IReadOnlyList<Campo> saltos)
    {
        var campoId = condicao.Campo.Id;
        IQueryable<Guid> ids;
        if (condicao.Campo.EhReferencia)
        {
            var destinoId = condicao.Referencia!.Value;
            ids = contexto.RegistrosReferencias
                .Where(rr => rr.CampoId == campoId && rr.RegistroDestinoId == destinoId)
                .Select(rr => rr.RegistroOrigemId);
        }
        else
        {
            ids = FiltrarValores(contexto.RegistrosValores.Where(v => v.CampoId == campoId), condicao).Select(v => v.RegistroId);
        }

        for (var i = saltos.Count - 1; i >= 0; i--)
        {
            var saltoId = saltos[i].Id;
            var apontados = ids;
            ids = from referencia in contexto.RegistrosReferencias
                  where referencia.CampoId == saltoId
                  join apontado in apontados on referencia.RegistroDestinoId equals apontado
                  select referencia.RegistroOrigemId;
        }

        return consulta.Where(r => ids.Contains(r.Id));
    }

    private static IQueryable<RegistroValor> FiltrarValores(IQueryable<RegistroValor> valores, CondicaoFiltro condicao)
    {
        switch (condicao.Campo.TipoDado)
        {
            case TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Lista or TipoDado.Url:
                // Igualdade e "contém" seguem a collation da coluna (CI_AI): sem diferenciar maiúsculas
                // nem acentos. O valor vai como parâmetro: % e _ são buscados literalmente.
                var texto = condicao.Texto!;
                return condicao.Comparacao == Comparacao.Contem
                    ? valores.Where(v => v.ValorTexto!.Contains(texto))
                    : valores.Where(v => v.ValorTexto == texto);

            case TipoDado.Inteiro or TipoDado.Decimal:
                var numero = condicao.Numero!.Value;
                return condicao.Comparacao switch
                {
                    Comparacao.MaiorOuIgual => valores.Where(v => v.ValorNumero >= numero),
                    Comparacao.MenorOuIgual => valores.Where(v => v.ValorNumero <= numero),
                    _ => valores.Where(v => v.ValorNumero == numero),
                };

            case TipoDado.Data or TipoDado.DataHora:
                var data = condicao.Data!.Value;
                return condicao.Comparacao switch
                {
                    Comparacao.MaiorOuIgual => valores.Where(v => v.ValorData >= data),
                    Comparacao.MenorOuIgual => valores.Where(v => v.ValorData <= data),
                    Comparacao.Menor => valores.Where(v => v.ValorData < data),
                    _ => valores.Where(v => v.ValorData == data),
                };

            case TipoDado.Booleano:
                var booleano = condicao.Booleano!.Value;
                return valores.Where(v => v.ValorBooleano == booleano);

            default:
                throw new ArgumentOutOfRangeException(nameof(condicao), condicao.Campo.TipoDado, "Tipo sem filtro.");
        }
    }

    /// <summary>
    /// RN19: ordena pelo valor TIPADO do campo (9 antes de 10; datas como datas), lido numa subconsulta
    /// correlacionada em RegistroValor. Registros sem valor (nulo) vêm primeiro na ordem crescente.
    /// </summary>
    private static IOrderedQueryable<Registro> OrdenarPorCampo(
        CatalogoDbContext contexto, IQueryable<Registro> consulta, Campo campo, bool descendente)
    {
        var campoId = campo.Id;
        var valores = contexto.RegistrosValores;

        return campo.TipoDado switch
        {
            TipoDado.Inteiro or TipoDado.Decimal => OrdenarPor(consulta,
                r => valores.Where(v => v.RegistroId == r.Id && v.CampoId == campoId).Select(v => v.ValorNumero).FirstOrDefault(),
                descendente),
            TipoDado.Data or TipoDado.DataHora => OrdenarPor(consulta,
                r => valores.Where(v => v.RegistroId == r.Id && v.CampoId == campoId).Select(v => v.ValorData).FirstOrDefault(),
                descendente),
            TipoDado.Booleano => OrdenarPor(consulta,
                r => valores.Where(v => v.RegistroId == r.Id && v.CampoId == campoId).Select(v => v.ValorBooleano).FirstOrDefault(),
                descendente),
            _ => OrdenarPor(consulta,
                r => valores.Where(v => v.RegistroId == r.Id && v.CampoId == campoId).Select(v => v.ValorTexto).FirstOrDefault(),
                descendente),
        };
    }

    /// <summary>
    /// RN19: por um campo REFERENCIA, pelo valor do campo de EXIBIÇÃO do registro apontado (o rótulo), com o tipo dele: texto em
    /// ordem alfabética (collation do banco), número como número, data como data. O valor vem de uma subconsulta correlacionada:
    /// RegistroReferencia (o apontado pelo campo) → RegistroValor (o campo de exibição dele). Registros sem referência, ou com
    /// o rótulo vazio, ficam SEMPRE por último (primeiro "tem valor?", depois o valor).
    /// </summary>
    private static IOrderedQueryable<Registro> OrdenarPorReferencia(
        CatalogoDbContext contexto, IQueryable<Registro> consulta, Campo referencia, Campo exibicao, bool descendente)
    {
        var campoId = referencia.Id;
        var exibicaoId = exibicao.Id;
        var rotulos =
            from rr in contexto.RegistrosReferencias
            where rr.CampoId == campoId
            join v in contexto.RegistrosValores on rr.RegistroDestinoId equals v.RegistroId
            where v.CampoId == exibicaoId
            select new { rr.RegistroOrigemId, v.ValorTexto, v.ValorNumero, v.ValorData, v.ValorBooleano };

        var comRotuloPrimeiro = consulta.OrderBy(r => rotulos.Any(x => x.RegistroOrigemId == r.Id) ? 0 : 1);
        return exibicao.TipoDado switch
        {
            TipoDado.Inteiro or TipoDado.Decimal => descendente
                ? comRotuloPrimeiro.ThenByDescending(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorNumero).FirstOrDefault())
                : comRotuloPrimeiro.ThenBy(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorNumero).FirstOrDefault()),
            TipoDado.Data or TipoDado.DataHora => descendente
                ? comRotuloPrimeiro.ThenByDescending(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorData).FirstOrDefault())
                : comRotuloPrimeiro.ThenBy(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorData).FirstOrDefault()),
            TipoDado.Booleano => descendente
                ? comRotuloPrimeiro.ThenByDescending(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorBooleano).FirstOrDefault())
                : comRotuloPrimeiro.ThenBy(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorBooleano).FirstOrDefault()),
            _ => descendente
                ? comRotuloPrimeiro.ThenByDescending(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorTexto).FirstOrDefault())
                : comRotuloPrimeiro.ThenBy(r => rotulos.Where(x => x.RegistroOrigemId == r.Id).Select(x => x.ValorTexto).FirstOrDefault()),
        };
    }

    /// <summary>
    /// RN48: pela média do critério, com os itens SEM nota sempre por último (nos dois sentidos): primeiro
    /// "tem nota?" e depois a média.
    /// </summary>
    private static IOrderedQueryable<Registro> OrdenarPorAvaliacao(
        CatalogoDbContext contexto, IQueryable<Registro> consulta, string criterio, bool descendente)
    {
        var notas = NotasDoCriterio(contexto, criterio);
        var comNotaPrimeiro = consulta.OrderBy(r => notas.Any(n => n.RegistroId == r.Id) ? 0 : 1);

        return descendente
            ? comNotaPrimeiro.ThenByDescending(r => notas.Where(n => n.RegistroId == r.Id).Average(n => (double?)n.Estrelas))
            : comNotaPrimeiro.ThenBy(r => notas.Where(n => n.RegistroId == r.Id).Average(n => (double?)n.Estrelas));
    }

    private static IOrderedQueryable<Registro> OrdenarPor<TChave>(
        IQueryable<Registro> consulta, Expression<Func<Registro, TChave>> chave, bool descendente) =>
        descendente ? consulta.OrderByDescending(chave) : consulta.OrderBy(chave);
}
