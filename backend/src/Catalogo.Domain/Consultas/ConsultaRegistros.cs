using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Consultas;

// Consulta de registros já INTERPRETADA e validada: só tem objetos de metadados e valores
// tipados, nunca texto cru da URL. A Infrastructure traduz isto para LINQ (docs/adr/0011).

/// <summary>Como comparar o valor do campo com o valor do filtro.</summary>
public enum Comparacao
{
    Igual,
    Contem,
    MaiorOuIgual,
    MenorOuIgual,
    /// <summary>Usado só internamente: "ate" com data sem hora num campo DATA_HORA vira "&lt; dia seguinte".</summary>
    Menor,
}

/// <summary>
/// Uma condição sobre um campo. Só a propriedade do tipo do campo vem preenchida:
/// <see cref="Texto"/> (TEXTO, TEXTO_LONGO, LISTA), <see cref="Numero"/> (INTEIRO, DECIMAL),
/// <see cref="Data"/> (DATA, DATA_HORA em UTC), <see cref="Booleano"/> ou <see cref="Referencia"/>.
/// </summary>
/// <param name="Saltos">
/// Campos REFERENCIA atravessados até o <see cref="Campo"/> (RN89): em cenario_teste, "relatorio_teste.tipo_teste=Carga" tem o salto
/// relatorio_teste e o campo tipo_teste do relatório. Nulo ou vazio = campo da própria entidade.
/// </param>
public sealed record CondicaoFiltro(
    Campo Campo,
    Comparacao Comparacao,
    string? Texto = null,
    decimal? Numero = null,
    DateTime? Data = null,
    bool? Booleano = null,
    Guid? Referencia = null,
    IReadOnlyList<Campo>? Saltos = null);

/// <summary>Colunas de sistema que podem ser usadas na ordenação.</summary>
public enum CampoSistema
{
    CriadoEm,
    AtualizadoEm,
}

/// <summary>
/// Condição sobre a MÉDIA de um critério ESTRELAS do modelo de avaliação ativo (RN48): "avaliacao.qualidade.de=4".
/// Itens sem nota nesse critério não passam no filtro. <see cref="Valor"/> é um limite sobre a média EXATA
/// (MaiorOuIgual ou Menor), já ajustado pelo interpretador para valer sobre a média exibida (uma casa).
/// </summary>
public sealed record CondicaoAvaliacao(string Criterio, Comparacao Comparacao, decimal Valor);

/// <summary>
/// Ordenação por um campo, por uma coluna de sistema OU pela média de um critério de avaliação
/// (<see cref="CriterioAvaliacao"/>, RN48: itens sem nota sempre por último).
/// </summary>
/// <param name="CampoExibicaoReferenciada">
/// Só quando <see cref="Campo"/> é REFERENCIA (RN19): o campo de exibição da entidade apontada, cujo valor (com o tipo dele)
/// ordena os registros. Registros sem referência (ou com o rótulo vazio) ficam sempre por último.
/// </param>
public sealed record Ordenacao(
    Campo? Campo, CampoSistema? CampoSistema, bool Descendente, string? CriterioAvaliacao = null, Campo? CampoExibicaoReferenciada = null)
{
    /// <summary>Padrão: mais recentes primeiro ("-criadoEm").</summary>
    public static Ordenacao Padrao { get; } = new(null, Consultas.CampoSistema.CriadoEm, Descendente: true);
}

/// <summary>Página pedida (começa em 1).</summary>
public sealed record Paginacao(int Pagina, int Tamanho)
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public static Paginacao Padrao { get; } = new(1, TamanhoPadrao);

    /// <summary>Quantos registros pular (Skip).</summary>
    public int Pular => (Pagina - 1) * Tamanho;

    public int TotalPaginas(int total) => total == 0 ? 0 : (total + Tamanho - 1) / Tamanho;
}

/// <summary>Tudo o que a listagem precisa: filtros (combinados com E), ordenação e página.</summary>
/// <param name="FiltroEquipe">Filtro "equipeId=..." pedido pelo cliente (RN27). Nulo = todas as visíveis.</param>
/// <param name="EquipesPermitidas">
/// ESCOPO DE SEGURANÇA (RN26): só registros dessas equipes. Nulo = sem restrição (administrador ou
/// entidade global). Preenchido SEMPRE pelo caso de uso a partir do usuário, nunca a partir da URL.
/// </param>
/// <param name="FiltroSituacao">Filtro "situacaoAprovacao=..." pedido pelo cliente (RN37).</param>
/// <param name="NaoAprovadosSomenteDoAutor">
/// ESCOPO DE SEGURANÇA (RN37/RN38): se preenchido, registros que não estão APROVADOS só entram se foram
/// criados por este usuário. Preenchido SEMPRE pelo caso de uso.
/// </param>
public sealed record ConsultaRegistros(
    IReadOnlyList<CondicaoFiltro> Condicoes,
    Ordenacao Ordenacao,
    Paginacao Paginacao,
    Guid? FiltroEquipe = null,
    IReadOnlySet<Guid>? EquipesPermitidas = null,
    Aprovacao.SituacaoAprovacao? FiltroSituacao = null,
    Guid? NaoAprovadosSomenteDoAutor = null,
    IReadOnlyList<CondicaoAvaliacao>? CondicoesAvaliacao = null);
