using System.Linq.Expressions;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Registros;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Repositorios;

namespace Catalogo.Infrastructure.Painel;

/// <summary>
/// Traduz uma <see cref="ConsultaIndicadores"/> (já validada no Domain) para UMA consulta LINQ que agrega no banco
/// (RN85 a RN87, docs/adr/0031). Nada é lido para a memória antes de agregar. Como ela é montada, em 3 passos:
///
///   1. <b>registros</b>: a MESMA consulta filtrada da listagem (<see cref="ConsultaRegistrosLinq.Filtrar"/>), mais o período;
///   2. <b>uma linha por registro</b> (<see cref="LinhaIndicador"/>): para cada dimensão e cada métrica, um LEFT JOIN com os
///      valores daquele campo (um método por tipo, abaixo, que devolve um <see cref="ValorDoRegistro"/> por registro);
///   3. <b>GROUP BY</b> pelas colunas das dimensões, com COUNT, AVG, SUM, MIN e MAX (<see cref="MontadorLinhas"/>).
///
/// SQL Server não aceita subconsulta dentro de GROUP BY nem de AVG: por isso os valores entram por JOIN (passo 2), e não
/// por subconsulta como na ordenação da listagem.
/// </summary>
internal static class ConsultaIndicadoresLinq
{
    private const string Id = nameof(ValorDoRegistro.Id);
    private const string Texto = nameof(ValorDoRegistro.Texto);
    private const string Booleano = nameof(ValorDoRegistro.Booleano);
    private const string Ano = nameof(ValorDoRegistro.Ano);
    private const string Mes = nameof(ValorDoRegistro.Mes);
    private const string Dia = nameof(ValorDoRegistro.Dia);
    private const string Situacao = nameof(ValorDoRegistro.Situacao);
    private const string Outros = nameof(ValorDoRegistro.Outros);

    public static IQueryable<LinhaAgregada> Montar(
        CatalogoDbContext contexto, IQueryable<Registro> registros, PeriodoPainel periodo, ConsultaIndicadores indicadores)
    {
        // 1. Registros: os da listagem, criados no período.
        registros = NoPeriodo(registros, periodo);

        // 2. Uma linha por registro, com um LEFT JOIN por dimensão e por métrica.
        var montador = new MontadorLinhas(registros);
        for (var i = 0; i < indicadores.Dimensoes.Count; i++)
        {
            var dimensao = indicadores.Dimensoes[i];
            montador.JuntarDimensao(i + 1, ValoresDaDimensao(contexto, registros, dimensao, periodo));
        }

        for (var j = 0; j < indicadores.Metricas.Count; j++)
        {
            var metrica = indicadores.Metricas[j];
            if (metrica.Funcao == FuncaoMetrica.MediaAvaliacao)
                montador.JuntarMediaAvaliacao(j + 1, MediasDeAvaliacao(contexto, metrica.Criterio!));
            else if (metrica.Funcao != FuncaoMetrica.Contagem)
                montador.JuntarNumero(j + 1, metrica.Funcao, Numeros(contexto, metrica.Campo!, metrica.Saltos ?? []));
        }

        // 3. GROUP BY, agregações, ordenação e limite (+1 para saber se truncou). Sem dimensões, a linha é uma só.
        var agrupadas = montador.Agrupar(indicadores);
        return indicadores.Dimensoes.Count == 0 ? agrupadas : agrupadas.Take(indicadores.Limite + 1);
    }

    /// <summary>Período do painel (RN80) sobre a data de criação: [início, fim) em UTC.</summary>
    public static IQueryable<Registro> NoPeriodo(IQueryable<Registro> registros, PeriodoPainel periodo)
    {
        if (periodo.InicioUtc is DateTime inicio)
            registros = registros.Where(r => r.CriadoEm >= inicio);
        if (periodo.FimUtc is DateTime fim)
            registros = registros.Where(r => r.CriadoEm < fim);
        return registros;
    }

    // ---- Dimensões: um método por tipo. Cada um devolve, por registro, o valor que agrupa. ----

    private static ValoresDimensao ValoresDaDimensao(
        CatalogoDbContext contexto, IQueryable<Registro> registros, Dimensao dimensao, PeriodoPainel periodo)
    {
        // Dimensões de sistema: colunas do próprio registro.
        switch (dimensao.Sistema)
        {
            case DimensaoSistema.Equipe:
                return new(PorEquipe(contexto, registros), [Texto, Id]);
            case DimensaoSistema.SituacaoAprovacao:
                return new(PorSituacao(registros), [Situacao]);
            case DimensaoSistema.CriadoEm:
                return TruncarData(PorDataDeCriacao(registros, periodo), dimensao.Periodo!.Value);
        }

        // Campos: o valor do tipo (já do registro apontado, se a dimensão é "referencia.campo"), depois o "Outros" do
        // TEXTO ou o período das datas.
        var valores = ValoresDoTipo(contexto, dimensao);
        if (dimensao.EhTextoLivre)
            return new(PorTexto(valores.Valores, registros), [Outros, Texto]);
        if (dimensao.Periodo is PeriodoAgrupamento periodoDaData)
            return TruncarData(valores.Valores, periodoDaData);
        return valores;
    }

    /// <summary>
    /// O valor do campo da dimensão, por registro consultado (atravessando os saltos do caminho, se houver). As colunas vêm na
    /// ordem em que a dimensão é ORDENADA.
    /// </summary>
    private static ValoresDimensao ValoresDoTipo(CatalogoDbContext contexto, Dimensao dimensao)
    {
        var campo = dimensao.Campo!;
        ValoresDimensao valores = campo.TipoDado switch
        {
            TipoDado.Lista or TipoDado.Texto => new(PorTextoDoCampo(contexto, campo), [Texto]),
            TipoDado.Booleano => new(PorBooleano(contexto, campo), [Booleano]),
            TipoDado.Referencia => new(PorReferencia(contexto, campo), [Texto, Id]),
            TipoDado.Data or TipoDado.DataHora => new(PorData(contexto, campo), [nameof(ValorDoRegistro.Data)]),
            _ => throw new ArgumentOutOfRangeException(nameof(dimensao), dimensao.Chave, "Dimensão sem tradução."),
        };

        return AtravessarSaltos(contexto, dimensao.Saltos, valores);
    }

    /// <summary>
    /// Caminho por referência (RN89): os valores são do registro do FIM do caminho; um JOIN com RegistroReferencia por salto, do
    /// último para o primeiro, troca cada registro apontado pelos que apontam para ele, até chegar aos registros consultados.
    /// Ex.: "relatorio_teste.projeto.sigla" em cenario_teste: sigla do projeto → projeto do relatório → relatório do cenário.
    /// </summary>
    private static ValoresDimensao AtravessarSaltos(CatalogoDbContext contexto, IReadOnlyList<Campo> saltos, ValoresDimensao valores)
    {
        for (var i = saltos.Count - 1; i >= 0; i--)
            valores = Atravessar(contexto, saltos[i], valores);
        return valores;
    }

    /// <summary>Equipe: o id e o nome (o nome serve para ordenar pela dimensão).</summary>
    private static IQueryable<ValorDoRegistro> PorEquipe(CatalogoDbContext contexto, IQueryable<Registro> registros) =>
        from registro in registros
        join equipe in contexto.Equipes on registro.EquipeId equals equipe.Id
        select new ValorDoRegistro { RegistroId = registro.Id, Id = equipe.Id, Texto = equipe.Nome };

    private static IQueryable<ValorDoRegistro> PorSituacao(IQueryable<Registro> registros) =>
        registros.Select(r => new ValorDoRegistro { RegistroId = r.Id, Situacao = r.SituacaoAprovacao });

    /// <summary>RN80: CriadoEm está em UTC; o deslocamento FIXO do fuso de negócio vira DATEADD(minute, ...) na consulta.</summary>
    private static IQueryable<ValorDoRegistro> PorDataDeCriacao(IQueryable<Registro> registros, PeriodoPainel periodo)
    {
        var minutos = periodo.Deslocamento.TotalMinutes;
        return registros.Select(r => new ValorDoRegistro { RegistroId = r.Id, Data = r.CriadoEm.AddMinutes(minutos) });
    }

    private static IQueryable<ValorDoRegistro> PorTextoDoCampo(CatalogoDbContext contexto, Campo campo) =>
        ValoresDoCampo(contexto, campo).Select(v => new ValorDoRegistro { RegistroId = v.RegistroId, Texto = v.ValorTexto });

    /// <summary>
    /// RN85: TEXTO livre: só os <see cref="ConsultaIndicadores.MaximoValoresTexto"/> valores mais frequentes (nos registros da
    /// consulta) viram grupos; os demais, "Outros" (Texto nulo e Outros = true). Os mais frequentes são uma subconsulta
    /// (GROUP BY + TOP) no MESMO SQL, ligada por LEFT JOIN: o valor que não casa com nenhum deles é "Outros".
    /// </summary>
    private static IQueryable<ValorDoRegistro> PorTexto(IQueryable<ValorDoRegistro> textos, IQueryable<Registro> registros)
    {
        var maisFrequentes = textos
            .Where(v => registros.Any(r => r.Id == v.RegistroId))
            .GroupBy(v => v.Texto)
            .Select(g => new { Valor = g.Key, Quantidade = g.Count() })
            .OrderByDescending(g => g.Quantidade).ThenBy(g => g.Valor)
            .Take(ConsultaIndicadores.MaximoValoresTexto)
            .Select(g => new ValorFrequente { Valor = g.Valor });

        return from texto in textos
               join frequente in maisFrequentes on texto.Texto equals frequente.Valor into frequentes
               from frequente in frequentes.DefaultIfEmpty()
               select new ValorDoRegistro { RegistroId = texto.RegistroId, Texto = frequente.Valor, Outros = frequente.Valor == null };
    }

    private sealed class ValorFrequente
    {
        public string? Valor { get; init; }
    }

    private static IQueryable<ValorDoRegistro> PorBooleano(CatalogoDbContext contexto, Campo campo) =>
        ValoresDoCampo(contexto, campo).Select(v => new ValorDoRegistro { RegistroId = v.RegistroId, Booleano = v.ValorBooleano });

    /// <summary>
    /// REFERENCIA: o registro apontado (RegistroReferencia, nunca o JSON) e, para ordenar pela dimensão, o valor de texto do
    /// campo de exibição dele. O rótulo exibido é montado depois, como na listagem.
    /// </summary>
    private static IQueryable<ValorDoRegistro> PorReferencia(CatalogoDbContext contexto, Campo campo) =>
        from referencia in contexto.RegistrosReferencias
        where referencia.CampoId == campo.Id
        join destino in contexto.Entidades on referencia.EntidadeDestinoId equals destino.Id
        join exibicao in contexto.RegistrosValores
            on new { RegistroId = referencia.RegistroDestinoId, CampoId = destino.CampoExibicaoId }
            equals new { exibicao.RegistroId, CampoId = (Guid?)exibicao.CampoId } into exibicoes
        from exibicao in exibicoes.DefaultIfEmpty()
        select new ValorDoRegistro { RegistroId = referencia.RegistroOrigemId, Id = referencia.RegistroDestinoId, Texto = exibicao.ValorTexto };

    /// <summary>
    /// Um salto de "referencia.campo" (RN89): troca o registro APONTADO pelo que aponta. Os valores de <paramref name="valores"/> são do
    /// registro apontado (ex.: o ambiente do relatório); o resultado é um por registro que aponta (cada cenário do relatório).
    ///   <c>from r in RegistroReferencia where r.CampoId == via join v in valores on r.RegistroDestinoId equals v.RegistroId
    ///   select new ValorDoRegistro { RegistroId = r.RegistroOrigemId, (as colunas de v) }</c>
    /// </summary>
    private static ValoresDimensao Atravessar(CatalogoDbContext contexto, Campo via, ValoresDimensao valores)
    {
        var pares =
            from referencia in contexto.RegistrosReferencias
            where referencia.CampoId == via.Id
            join valor in valores.Valores on referencia.RegistroDestinoId equals valor.RegistroId
            select new ValorApontado { RegistroId = referencia.RegistroOrigemId, Valor = valor };

        // p => new ValorDoRegistro { RegistroId = p.RegistroId, Texto = p.Valor.Texto, ... } (só as colunas preenchidas).
        var p = Expression.Parameter(typeof(ValorApontado), "p");
        var valorApontado = Expression.Property(p, nameof(ValorApontado.Valor));
        var atribuicoes = valores.Colunas
            .Select(c => Expression.Bind(typeof(ValorDoRegistro).GetProperty(c)!, Expression.Property(valorApontado, c)))
            .Prepend(Expression.Bind(typeof(ValorDoRegistro).GetProperty(nameof(ValorDoRegistro.RegistroId))!, Expression.Property(p, nameof(ValorApontado.RegistroId))));
        var seletor = Expression.Lambda<Func<ValorApontado, ValorDoRegistro>>(
            Expression.MemberInit(Expression.New(typeof(ValorDoRegistro)), atribuicoes), p);

        return new(pares.Select(seletor), valores.Colunas);
    }

    /// <summary>O valor de um registro apontado, com o registro que aponta (usado só em <see cref="Atravessar"/>).</summary>
    private sealed class ValorApontado
    {
        public Guid RegistroId { get; init; }
        public ValorDoRegistro Valor { get; init; } = null!;
    }

    /// <summary>DATA e DATA_HORA de campos: como estão gravadas (RN80), sem conversão de fuso.</summary>
    private static IQueryable<ValorDoRegistro> PorData(CatalogoDbContext contexto, Campo campo) =>
        ValoresDoCampo(contexto, campo).Select(v => new ValorDoRegistro { RegistroId = v.RegistroId, Data = v.ValorData });

    /// <summary>
    /// A data vira o período: ano, mês (o primeiro do trimestre, no trimestre) e dia, como NÚMEROS (DATEPART no SQL). Todos os
    /// registros do mesmo mês têm o mesmo ano e mês. Números porque o SQL Server agrupa por eles sem conversão.
    /// </summary>
    private static ValoresDimensao TruncarData(IQueryable<ValorDoRegistro> valores, PeriodoAgrupamento periodo) => periodo switch
    {
        PeriodoAgrupamento.Dia => new(valores.Select(v => new ValorDoRegistro
        {
            RegistroId = v.RegistroId, Ano = v.Data!.Value.Year, Mes = v.Data.Value.Month, Dia = v.Data.Value.Day,
        }), [Ano, Mes, Dia]),
        PeriodoAgrupamento.Mes => new(valores.Select(v => new ValorDoRegistro
        {
            RegistroId = v.RegistroId, Ano = v.Data!.Value.Year, Mes = v.Data.Value.Month,
        }), [Ano, Mes]),
        PeriodoAgrupamento.Trimestre => new(valores.Select(v => new ValorDoRegistro
        {
            RegistroId = v.RegistroId, Ano = v.Data!.Value.Year, Mes = (v.Data.Value.Month - 1) / 3 * 3 + 1,
        }), [Ano, Mes]),
        _ => new(valores.Select(v => new ValorDoRegistro { RegistroId = v.RegistroId, Ano = v.Data!.Value.Year }), [Ano]),
    };

    // ---- Métricas ----

    /// <summary>
    /// INTEIRO e DECIMAL: a coluna tipada ValorNumero. Registro sem valor não tem linha: a média o ignora (RN86). Por caminho (RN89),
    /// cada registro consultado recebe o número do registro apontado: a média pesa por registro consultado (dois cenários do mesmo
    /// relatório contam o número do relatório duas vezes).
    /// </summary>
    private static IQueryable<ValorDoRegistro> Numeros(CatalogoDbContext contexto, Campo campo, IReadOnlyList<Campo> saltos) =>
        AtravessarSaltos(contexto, saltos, new(
            ValoresDoCampo(contexto, campo).Select(v => new ValorDoRegistro { RegistroId = v.RegistroId, Numero = v.ValorNumero }),
            [nameof(ValorDoRegistro.Numero)])).Valores;

    /// <summary>
    /// RN86: a média das notas de CADA item no critério (modelos e critérios ativos, como no filtro RN48). Na agregação, a
    /// média do grupo é a média dessas médias: cada item pesa igual, tenha 1 ou 50 avaliações.
    /// </summary>
    private static IQueryable<ValorDoRegistro> MediasDeAvaliacao(CatalogoDbContext contexto, string criterio) =>
        ConsultaRegistrosLinq.NotasDoCriterio(contexto, criterio)
            .GroupBy(n => n.RegistroId)
            .Select(g => new ValorDoRegistro { RegistroId = g.Key, Media = g.Average(n => n.Estrelas) });

    private static IQueryable<RegistroValor> ValoresDoCampo(CatalogoDbContext contexto, Campo campo) =>
        contexto.RegistrosValores.Where(v => v.CampoId == campo.Id);
}

/// <summary>Os valores de uma dimensão (um por registro) e quais colunas de <see cref="ValorDoRegistro"/> eles preenchem.</summary>
internal sealed record ValoresDimensao(IQueryable<ValorDoRegistro> Valores, IReadOnlyList<string> Colunas);

/// <summary>
/// O valor de um registro para uma dimensão ou métrica: só as propriedades do tipo vêm preenchidas (as outras nem entram no
/// SELECT, por isso quem usa precisa saber quais são: <see cref="ValoresDimensao.Colunas"/>).
/// </summary>
internal sealed class ValorDoRegistro
{
    public Guid RegistroId { get; init; }
    public Guid? Id { get; init; }
    public string? Texto { get; init; }
    public bool? Booleano { get; init; }

    /// <summary>A data, antes de virar período (<see cref="Ano"/>, <see cref="Mes"/>, <see cref="Dia"/>).</summary>
    public DateTime? Data { get; init; }
    public int? Ano { get; init; }
    public int? Mes { get; init; }
    public int? Dia { get; init; }
    public SituacaoAprovacao? Situacao { get; init; }
    public bool Outros { get; init; }
    public decimal? Numero { get; init; }
    public double? Media { get; init; }
}
