using System.Text.Json.Nodes;

namespace Catalogo.Domain.Metadados.Administracao;

/// <summary>
/// Uma pergunta sobre os DADOS existentes, que uma alteração de metadados precisa fazer antes de ser gravada
/// (RN58). O Domain diz O QUE perguntar (<see cref="PlanejadorAlteracoes"/>); a Infrastructure responde em LINQ,
/// numa transação Serializable junto com a gravação. Cada uma vira um <see cref="ImpactoDados"/>.
/// </summary>
/// <param name="Tipo">Código estável do impacto, em MAIÚSCULAS (ex.: "TEXTO_ACIMA_DO_TAMANHO"): o front decide por ele.</param>
/// <param name="Descricao">Explicação para o administrador, em português.</param>
/// <param name="Bloqueia">
/// Verdadeiro: se algum registro for encontrado, a alteração é BLOQUEADA (409). Falso: só informa quantos
/// registros a alteração vai afetar (ex.: os que vão receber o valor padrão).
/// </param>
public abstract record VerificacaoDados(string Tipo, string Descricao, bool Bloqueia);

/// <summary>Registros da entidade (qualquer um). Ex.: mudar o escopo só é possível sem registros (RN74).</summary>
public sealed record RegistrosDaEntidade(Guid EntidadeId, string Tipo, string Descricao, bool Bloqueia = true)
    : VerificacaoDados(Tipo, Descricao, Bloqueia);

/// <summary>Registros da entidade SEM valor no campo (RN60, RN61). Campo novo: todos os registros.</summary>
public sealed record RegistrosSemValor(Campo Campo, string Tipo, string Descricao, bool Bloqueia)
    : VerificacaoDados(Tipo, Descricao, Bloqueia);

/// <summary>Registros COM valor no campo (RN70: só se exclui um campo que ninguém preencheu).</summary>
public sealed record RegistrosComValor(Campo Campo, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>Textos com mais de <see cref="Tamanho"/> caracteres (RN63).</summary>
public sealed record TextosAcimaDoTamanho(Campo Campo, int Tamanho, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>Números que não cabem em DECIMAL(<see cref="Precisao"/>, <see cref="Escala"/>) (RN65).</summary>
public sealed record NumerosForaDaPrecisao(Campo Campo, int Precisao, int Escala, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>
/// Registros com valor no campo que NÃO é uma URL válida (RN66, ADR 0042): bloqueia a mudança de TEXTO ou TEXTO_LONGO para URL.
/// A validade é a do UrlValidador.Conferir (a mesma da gravação), por isso a Infrastructure lê os valores em lotes.
/// </summary>
public sealed record ValoresQueNaoSaoUrl(Campo Campo, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>Registros que usam alguma das <see cref="Opcoes"/> de um campo LISTA (RN68).</summary>
public sealed record ValoresComOpcoes(Campo Campo, IReadOnlyList<string> Opcoes, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>
/// Grupos de registros que ficariam duplicados numa restrição única nova (RN71). A entidade passada já tem a
/// restrição: o hash é o MESMO da <see cref="ChavesUnicas.CalculadoraChaveUnica"/>, que define a unicidade (RN09).
/// </summary>
public sealed record DuplicatasNaRestricao(Entidade Entidade, RestricaoUnica Restricao, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>Registros que violam uma dependência nova ou alterada, com a semântica da RN08 (RN73).</summary>
public sealed record ViolacoesDaDependencia(CampoDependencia Dependencia, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>Linhas do histórico que apontam para a entidade: ela já teve registros (RN76).</summary>
public sealed record HistoricoDaEntidade(Guid EntidadeId, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>Configurações de equipe da entidade: aprovação ativa ou modelo de avaliação (RN74, RN76).</summary>
public sealed record ConfiguracoesDaEntidade(Guid EntidadeId, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: true);

/// <summary>
/// Registros da equipe nas entidades do grupo (RN92): os itens que ficam OCULTOS para os membros da equipe quando ela deixa de
/// ser habilitada na listagem. Não bloqueia: os itens não são apagados e voltam quando a equipe é habilitada de novo.
/// </summary>
public sealed record RegistrosDaEquipeNoGrupo(Guid GrupoId, Guid EquipeId, string Tipo, string Descricao)
    : VerificacaoDados(Tipo, Descricao, Bloqueia: false);

/// <summary>Um registro afetado, para o relatório de impacto.</summary>
public sealed record RegistroAfetado(Guid Id, string? Rotulo);

/// <summary>
/// A resposta de uma <see cref="VerificacaoDados"/>: quantos registros, até 10 exemplos e, para duplicatas, os
/// grupos de registros repetidos. Também usado para impactos de configuração (ex.: campo em uso numa restrição),
/// sem registros (<see cref="Quantidade"/> 0 e <see cref="Usos"/> preenchido).
/// </summary>
public sealed record ImpactoDados(
    string Tipo,
    string Descricao,
    bool Bloqueia,
    int Quantidade,
    IReadOnlyList<RegistroAfetado> Exemplos,
    IReadOnlyList<IReadOnlyList<RegistroAfetado>>? Grupos = null,
    IReadOnlyList<string>? Usos = null)
{
    public const int MaximoExemplos = 10;

    /// <summary>Bloqueia de fato: é bloqueante e encontrou algo (registros ou usos).</summary>
    public bool Impede => Bloqueia && (Quantidade > 0 || Usos is { Count: > 0 });
}

/// <summary>Uma escrita em massa nos registros, feita em lotes na mesma transação da alteração (RN58).</summary>
public abstract record EscritaRegistros;

/// <summary>
/// Grava <see cref="Valor"/> (já validado e normalizado) no campo, em Dados e RegistroValor (RN60, RN61).
/// <see cref="ApenasVazios"/>: só nos registros sem valor (tornar obrigatório).
/// </summary>
public sealed record PreencherValorPadrao(Guid EntidadeId, string ChaveCampo, JsonNode Valor, bool ApenasVazios) : EscritaRegistros;

/// <summary>
/// (Re)calcula as chaves únicas destas restrições em todos os registros, com a estrutura NOVA da entidade:
/// restrição nova (RN71), reordenação de campos e mudança de escala, que mudam o hash (RN09).
/// </summary>
public sealed record RecalcularChavesUnicas(Guid EntidadeId, IReadOnlyList<string> Restricoes) : EscritaRegistros;

/// <summary>
/// O que uma alteração exige: as verificações contra os dados e as escritas em massa, além dos usos que já se sabe,
/// pela configuração, que impedem a mudança (<see cref="Usos"/>, ex.: o campo é campo de exibição).
/// </summary>
public sealed record PlanoAlteracao(
    IReadOnlyList<VerificacaoDados> Verificacoes,
    IReadOnlyList<EscritaRegistros> Escritas,
    IReadOnlyList<ImpactoDados> Usos)
{
    public static PlanoAlteracao Vazio { get; } = new([], [], []);
}
