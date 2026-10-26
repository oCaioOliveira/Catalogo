using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Painel;

// A agregação configurável do painel (RN85 a RN87), já INTERPRETADA e validada pelo InterpretadorIndicadores: só objetos de
// metadados e valores tipados, nunca texto cru da URL. A Infrastructure traduz isto para LINQ (ConsultaIndicadoresLinq).

/// <summary>Período de agrupamento de uma dimensão de data (":dia", ":mes", ":trimestre", ":ano").</summary>
public enum PeriodoAgrupamento
{
    Dia,
    Mes,
    Trimestre,
    Ano,
}

/// <summary>Dimensões que não são campos: as colunas de sistema do registro.</summary>
public enum DimensaoSistema
{
    Equipe,
    SituacaoAprovacao,

    /// <summary>Data de criação, no fuso de negócio (RN80).</summary>
    CriadoEm,
}

/// <summary>
/// Uma dimensão de agrupamento: um campo (LISTA, BOOLEANO, REFERENCIA, TEXTO, ou DATA/DATA_HORA com período) ou uma dimensão de
/// sistema. <see cref="Chave"/> é como ela vem na URL e aparece nas linhas do resultado (ex.: "data_execucao:mes").
/// </summary>
/// <param name="Tipo">Tipo exibido ao front: o do campo ("REFERENCIA"), ou "EQUIPE", "SITUACAO_APROVACAO", "DATA_HORA".</param>
/// <param name="Saltos">
/// Campos REFERENCIA (até 2) pelos quais se chega ao <see cref="Campo"/>, que é então do registro APONTADO (RN89): em
/// cenario_teste, "relatorio_teste.ambiente" agrupa cada cenário pelo ambiente do relatório dele. Vazio = campo da própria entidade.
/// </param>
public sealed record Dimensao(
    string Chave, string Nome, string Tipo, Campo? Campo, DimensaoSistema? Sistema, PeriodoAgrupamento? Periodo, IReadOnlyList<Campo> Saltos)
{
    public Dimensao(string chave, string nome, string tipo, Campo? campo, DimensaoSistema? sistema, PeriodoAgrupamento? periodo)
        : this(chave, nome, tipo, campo, sistema, periodo, [])
    {
    }

    /// <summary>TEXTO livre: só os valores mais frequentes viram grupos; o resto vai para "Outros" (RN85).</summary>
    public bool EhTextoLivre => Campo?.TipoDado == TipoDado.Texto;
}

/// <summary>A função de uma métrica.</summary>
public enum FuncaoMetrica
{
    Contagem,
    Media,
    Soma,
    Minimo,
    Maximo,

    /// <summary>Média das médias dos itens num critério de estrelas do modelo de avaliação ativo (RN86).</summary>
    MediaAvaliacao,
}

/// <summary>
/// Uma métrica: "contagem", "media:campo", "soma:campo", "minimo:campo", "maximo:campo" ou "media_avaliacao:criterio". O campo pode
/// estar num registro apontado (RN89): "media:relatorio_teste.duracao" tem o salto relatorio_teste (<see cref="Saltos"/>).
/// </summary>
public sealed record Metrica(
    string Chave, string Nome, FuncaoMetrica Funcao, Campo? Campo = null, string? Criterio = null, IReadOnlyList<Campo>? Saltos = null);

/// <summary>Ordenação do resultado por uma dimensão ou métrica (pela chave), crescente ou decrescente.</summary>
public sealed record OrdenacaoIndicador(string Chave, bool Descendente);

/// <summary>A consulta inteira: até 2 dimensões, até 5 métricas, a ordenação e o limite de linhas.</summary>
public sealed record ConsultaIndicadores(
    IReadOnlyList<Dimensao> Dimensoes, IReadOnlyList<Metrica> Metricas, OrdenacaoIndicador? Ordenacao, int Limite)
{
    public const int MaximoDimensoes = 2;
    public const int MaximoMetricas = 5;
    public const int LimitePadrao = 100;
    public const int LimiteMaximo = 500;

    /// <summary>Numa dimensão TEXTO, só os N valores mais frequentes viram grupos (RN85).</summary>
    public const int MaximoValoresTexto = 50;

    /// <summary>Chave da contagem: está em TODA linha do resultado, pedida ou não.</summary>
    public const string Contagem = "contagem";
}
