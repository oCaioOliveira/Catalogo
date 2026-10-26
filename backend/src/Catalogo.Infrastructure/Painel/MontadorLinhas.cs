using System.Linq.Expressions;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Painel;
using Catalogo.Domain.Registros;

namespace Catalogo.Infrastructure.Painel;

/// <summary>
/// Monta, por partes, a consulta da agregação configurável (passos 2 e 3 de <see cref="ConsultaIndicadoresLinq"/>):
///
///   <c>linhas = registros.Select(r =&gt; new LinhaIndicador { RegistroId = r.Id })</c>
///   cada <see cref="JuntarDimensao"/>/<see cref="JuntarNumero"/>: <c>linhas LEFT JOIN valores ON RegistroId</c>, copiando as
///   colunas já preenchidas e preenchendo as novas (ex.: <c>Texto1 = valor.Texto</c>);
///   <see cref="Agrupar"/>: <c>GROUP BY</c> as colunas das dimensões, com <c>COUNT(*)</c> e uma agregação por métrica.
///
/// Por que expressões montadas aqui, e não um LINQ escrito à mão: o número de JOINs depende do pedido (até 2 dimensões e 5
/// métricas), e o SELECT precisa listar só as colunas preenchidas (o EF Core não traduz a leitura de uma propriedade que não
/// foi atribuída). As expressões montadas são exatamente as que se escreveria à mão; o EF Core as traduz como qualquer LINQ.
/// </summary>
internal sealed class MontadorLinhas(IQueryable<Registro> registros)
{
    private IQueryable<LinhaIndicador> _linhas = registros.Select(r => new LinhaIndicador { RegistroId = r.Id });

    /// <summary>Colunas de <see cref="LinhaIndicador"/> já preenchidas (as que cada JOIN copia).</summary>
    private readonly List<string> _preenchidas = [nameof(LinhaIndicador.RegistroId)];

    /// <summary>Colunas de cada dimensão (ex.: ["Texto1", "Id1"]), na ordem de ordenação.</summary>
    private readonly List<IReadOnlyList<string>> _dimensoes = [];

    /// <summary>Para cada métrica (pela posição 1 a 5): a coluna agregada e a função.</summary>
    private readonly Dictionary<int, (string Coluna, FuncaoMetrica Funcao)> _metricas = [];

    /// <param name="posicao">1 ou 2.</param>
    public void JuntarDimensao(int posicao, ValoresDimensao valores)
    {
        var colunas = valores.Colunas.Select(c => (Destino: c + posicao, Origem: c)).ToList();
        Juntar(valores.Valores, colunas);
        _dimensoes.Add(colunas.Select(c => c.Destino).ToList());
    }

    /// <param name="posicao">1 a 5 (a posição da métrica no pedido).</param>
    public void JuntarNumero(int posicao, FuncaoMetrica funcao, IQueryable<ValorDoRegistro> valores)
    {
        var coluna = nameof(LinhaIndicador.Numero1)[..^1] + posicao;
        Juntar(valores, [(coluna, nameof(ValorDoRegistro.Numero))]);
        _metricas[posicao] = (coluna, funcao);
    }

    /// <param name="posicao">1 a 5 (a posição da métrica no pedido).</param>
    public void JuntarMediaAvaliacao(int posicao, IQueryable<ValorDoRegistro> valores)
    {
        var coluna = nameof(LinhaIndicador.Media1)[..^1] + posicao;
        Juntar(valores, [(coluna, nameof(ValorDoRegistro.Media))]);
        _metricas[posicao] = (coluna, FuncaoMetrica.Media);
    }

    /// <summary>
    /// <c>linhas.GroupJoin(valores, ...).SelectMany(j =&gt; j.Valores.DefaultIfEmpty(), (j, v) =&gt; new LinhaIndicador { ... })</c>:
    /// o LEFT JOIN do LINQ. Cada registro tem no máximo um valor por campo, então o JOIN não multiplica linhas.
    /// </summary>
    private void Juntar(IQueryable<ValorDoRegistro> valores, IReadOnlyList<(string Destino, string Origem)> colunas)
    {
        var juntadas = _linhas.GroupJoin(valores, l => l.RegistroId, v => v.RegistroId, (l, vs) => new Juncao { Linha = l, Valores = vs });

        // (j, v) => new LinhaIndicador { RegistroId = j.Linha.RegistroId, ..., Texto1 = v.Texto }
        var juncao = Expression.Parameter(typeof(Juncao), "j");
        var valor = Expression.Parameter(typeof(ValorDoRegistro), "v");
        var linha = Expression.Property(juncao, nameof(Juncao.Linha));
        var atribuicoes = _preenchidas
            .Select(coluna => Atribuir<LinhaIndicador>(coluna, Expression.Property(linha, coluna)))
            .Concat(colunas.Select(c => Atribuir<LinhaIndicador>(c.Destino, Expression.Property(valor, c.Origem))));
        var seletor = Expression.Lambda<Func<Juncao, ValorDoRegistro, LinhaIndicador>>(
            Expression.MemberInit(Expression.New(typeof(LinhaIndicador)), atribuicoes), juncao, valor);

        _linhas = juntadas.SelectMany(j => j.Valores.DefaultIfEmpty(), seletor!);
        _preenchidas.AddRange(colunas.Select(c => c.Destino));
    }

    /// <summary>GROUP BY pelas colunas das dimensões; COUNT(*) e uma agregação por métrica; ORDER BY pedido (RN87).</summary>
    public IQueryable<LinhaAgregada> Agrupar(ConsultaIndicadores indicadores)
    {
        var colunasDimensoes = _dimensoes.SelectMany(c => c).ToList();

        // l => new ChaveIndicador { Texto1 = l.Texto1, Ano2 = l.Ano2, ... }. Sem dimensões: chave constante (uma linha só).
        var l = Expression.Parameter(typeof(LinhaIndicador), "l");
        var chave = Expression.Lambda<Func<LinhaIndicador, ChaveIndicador>>(
            Expression.MemberInit(
                Expression.New(typeof(ChaveIndicador)),
                colunasDimensoes.Count == 0
                    ? [Atribuir<ChaveIndicador>(nameof(ChaveIndicador.Constante), Expression.Constant(1))]
                    : colunasDimensoes.Select(c => Atribuir<ChaveIndicador>(c, Expression.Property(l, c)))),
            l);

        // g => new LinhaAgregada { Texto1 = g.Key.Texto1, ..., Contagem = g.Count(), Numero1 = g.Average(l => l.Numero1), ... }
        var g = Expression.Parameter(typeof(IGrouping<ChaveIndicador, LinhaIndicador>), "g");
        var dados = Expression.Property(g, nameof(IGrouping<ChaveIndicador, LinhaIndicador>.Key));
        var atribuicoes = colunasDimensoes
            .Select(c => Atribuir<LinhaAgregada>(c, Expression.Property(dados, c)))
            .Append(Atribuir<LinhaAgregada>(nameof(LinhaAgregada.Contagem), Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [typeof(LinhaIndicador)], g)))
            .Concat(_metricas.Select(m => Atribuir<LinhaAgregada>(m.Value.Coluna, Agregacao(g, m.Value.Coluna, m.Value.Funcao))));
        var seletor = Expression.Lambda<Func<IGrouping<ChaveIndicador, LinhaIndicador>, LinhaAgregada>>(
            Expression.MemberInit(Expression.New(typeof(LinhaAgregada)), atribuicoes), g);

        return Ordenar(_linhas.GroupBy(chave).Select(seletor), indicadores);
    }

    /// <summary><c>g.Average(l =&gt; l.Numero1)</c> (ou Sum, Min, Max): AVG, SUM, MIN e MAX ignoram os nulos (registros sem valor).</summary>
    private static MethodCallExpression Agregacao(ParameterExpression grupo, string coluna, FuncaoMetrica funcao)
    {
        var metodo = funcao switch
        {
            FuncaoMetrica.Media => nameof(Enumerable.Average),
            FuncaoMetrica.Soma => nameof(Enumerable.Sum),
            FuncaoMetrica.Minimo => nameof(Enumerable.Min),
            FuncaoMetrica.Maximo => nameof(Enumerable.Max),
            _ => throw new ArgumentOutOfRangeException(nameof(funcao), funcao, "Métrica sem agregação."),
        };

        var l = Expression.Parameter(typeof(LinhaIndicador), "l");
        var seletor = Expression.Lambda(Expression.Property(l, coluna), l);
        return Expression.Call(typeof(Enumerable), metodo, [typeof(LinhaIndicador)], grupo, seletor);
    }

    /// <summary>
    /// RN87: pela dimensão ou métrica pedida ("-" = decrescente) e, para desempatar, pelas dimensões em ordem crescente.
    /// Sem ordenarPor: pelas dimensões (ex.: a série por mês sai em ordem cronológica).
    /// </summary>
    private IQueryable<LinhaAgregada> Ordenar(IQueryable<LinhaAgregada> linhas, ConsultaIndicadores indicadores)
    {
        var ordem = new List<(string Coluna, bool Descendente)>();
        if (indicadores.Ordenacao is { } pedida)
            ordem.AddRange(ColunasDe(pedida.Chave, indicadores).Select(c => (c, pedida.Descendente)));
        foreach (var coluna in _dimensoes.SelectMany(c => c).Where(c => ordem.All(o => o.Coluna != c)))
            ordem.Add((coluna, false));

        for (var i = 0; i < ordem.Count; i++)
        {
            var x = Expression.Parameter(typeof(LinhaAgregada), "x");
            var propriedade = Expression.Property(x, ordem[i].Coluna);
            var metodo = (i == 0 ? "OrderBy" : "ThenBy") + (ordem[i].Descendente ? "Descending" : "");
            linhas = linhas.Provider.CreateQuery<LinhaAgregada>(Expression.Call(
                typeof(Queryable), metodo, [typeof(LinhaAgregada), propriedade.Type], linhas.Expression,
                Expression.Quote(Expression.Lambda(propriedade, x))));
        }

        return linhas;
    }

    /// <summary>As colunas de <see cref="LinhaAgregada"/> de uma chave de dimensão ou de métrica.</summary>
    private IReadOnlyList<string> ColunasDe(string chave, ConsultaIndicadores indicadores)
    {
        if (chave == ConsultaIndicadores.Contagem)
            return [nameof(LinhaAgregada.Contagem)];

        var dimensao = indicadores.Dimensoes.Select((d, i) => (d, i)).FirstOrDefault(x => x.d.Chave == chave);
        if (dimensao.d is not null)
            return _dimensoes[dimensao.i];

        var posicao = indicadores.Metricas.Select((m, i) => (m, i)).First(x => x.m.Chave == chave).i + 1;
        return _metricas.TryGetValue(posicao, out var metrica) ? [metrica.Coluna] : [nameof(LinhaAgregada.Contagem)];
    }

    /// <summary><c>Propriedade = valor</c> num <c>new T { ... }</c>, convertendo para o tipo da propriedade (ex.: bool → bool?).</summary>
    private static MemberAssignment Atribuir<T>(string propriedade, Expression valor)
    {
        var membro = typeof(T).GetProperty(propriedade) ?? throw new InvalidOperationException($"{typeof(T).Name} não tem '{propriedade}'.");
        return Expression.Bind(membro, valor.Type == membro.PropertyType ? valor : Expression.Convert(valor, membro.PropertyType));
    }

    /// <summary>O resultado do GroupJoin: a linha e os valores do registro dela (zero ou um).</summary>
    private sealed class Juncao
    {
        public LinhaIndicador Linha { get; init; } = null!;
        public IEnumerable<ValorDoRegistro> Valores { get; init; } = [];
    }
}

/// <summary>
/// Uma linha por registro, com os valores usados na agregação. Dimensão N (1 ou 2) usa as colunas terminadas em N, conforme
/// o tipo (ex.: LISTA usa Texto1; REFERENCIA, Id1 e Texto1; data por mês, Ano1 e Mes1); métrica N (1 a 5), Numero N ou Media N.
/// </summary>
internal sealed class LinhaIndicador
{
    public Guid RegistroId { get; init; }

    public Guid? Id1 { get; init; }
    public string? Texto1 { get; init; }
    public bool? Booleano1 { get; init; }
    public int? Ano1 { get; init; }
    public int? Mes1 { get; init; }
    public int? Dia1 { get; init; }
    public SituacaoAprovacao? Situacao1 { get; init; }
    public bool? Outros1 { get; init; }

    public Guid? Id2 { get; init; }
    public string? Texto2 { get; init; }
    public bool? Booleano2 { get; init; }
    public int? Ano2 { get; init; }
    public int? Mes2 { get; init; }
    public int? Dia2 { get; init; }
    public SituacaoAprovacao? Situacao2 { get; init; }
    public bool? Outros2 { get; init; }

    public decimal? Numero1 { get; init; }
    public decimal? Numero2 { get; init; }
    public decimal? Numero3 { get; init; }
    public decimal? Numero4 { get; init; }
    public decimal? Numero5 { get; init; }

    public double? Media1 { get; init; }
    public double? Media2 { get; init; }
    public double? Media3 { get; init; }
    public double? Media4 { get; init; }
    public double? Media5 { get; init; }
}

/// <summary>A chave do GROUP BY: as colunas das dimensões (<see cref="Constante"/> só quando não há dimensão).</summary>
internal sealed class ChaveIndicador
{
    public int Constante { get; init; }

    public Guid? Id1 { get; init; }
    public string? Texto1 { get; init; }
    public bool? Booleano1 { get; init; }
    public int? Ano1 { get; init; }
    public int? Mes1 { get; init; }
    public int? Dia1 { get; init; }
    public SituacaoAprovacao? Situacao1 { get; init; }
    public bool? Outros1 { get; init; }

    public Guid? Id2 { get; init; }
    public string? Texto2 { get; init; }
    public bool? Booleano2 { get; init; }
    public int? Ano2 { get; init; }
    public int? Mes2 { get; init; }
    public int? Dia2 { get; init; }
    public SituacaoAprovacao? Situacao2 { get; init; }
    public bool? Outros2 { get; init; }
}

/// <summary>Uma linha do resultado: as colunas das dimensões, a contagem e as métricas agregadas.</summary>
internal sealed class LinhaAgregada
{
    public Guid? Id1 { get; init; }
    public string? Texto1 { get; init; }
    public bool? Booleano1 { get; init; }
    public int? Ano1 { get; init; }
    public int? Mes1 { get; init; }
    public int? Dia1 { get; init; }
    public SituacaoAprovacao? Situacao1 { get; init; }
    public bool? Outros1 { get; init; }

    public Guid? Id2 { get; init; }
    public string? Texto2 { get; init; }
    public bool? Booleano2 { get; init; }
    public int? Ano2 { get; init; }
    public int? Mes2 { get; init; }
    public int? Dia2 { get; init; }
    public SituacaoAprovacao? Situacao2 { get; init; }
    public bool? Outros2 { get; init; }

    public int Contagem { get; init; }

    public decimal? Numero1 { get; init; }
    public decimal? Numero2 { get; init; }
    public decimal? Numero3 { get; init; }
    public decimal? Numero4 { get; init; }
    public decimal? Numero5 { get; init; }

    public double? Media1 { get; init; }
    public double? Media2 { get; init; }
    public double? Media3 { get; init; }
    public double? Media4 { get; init; }
    public double? Media5 { get; init; }
}
