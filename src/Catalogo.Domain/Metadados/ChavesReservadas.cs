namespace Catalogo.Domain.Metadados;

/// <summary>
/// Chaves que nenhum <see cref="Campo"/> pode usar (RN14).
///   - Colunas de sistema do registro: id, versao, criadoEm, atualizadoEm, equipeId, criadoPor,
///     atualizadoPor, situacaoAprovacao, ultimaDecisao, avaliacao. Enviadas no corpo, as três últimas são ignoradas
///     (aprovação e avaliação têm endpoints próprios). O corpo do POST/PUT traz "versao" e "equipeId" junto com os campos, então um
///     campo com esses nomes seria ambíguo.
///   - Parâmetros da listagem e das opções: pagina, tamanho, ordenacao, busca, dependeDe. Um campo
///     com esse nome não poderia ser usado como filtro na URL.
/// A comparação não diferencia maiúsculas, igual à collation do banco para Campo.Chave.
/// </summary>
public static class ChavesReservadas
{
    public const string Id = "id";
    public const string Versao = "versao";
    public const string CriadoEm = "criadoEm";
    public const string AtualizadoEm = "atualizadoEm";
    public const string EquipeId = "equipeId";
    public const string CriadoPor = "criadoPor";
    public const string AtualizadoPor = "atualizadoPor";
    public const string SituacaoAprovacao = "situacaoAprovacao";
    public const string UltimaDecisao = "ultimaDecisao";

    /// <summary>Resumo das avaliações no registro (RN48) e prefixo da ordenação/filtro por média ("avaliacao.qualidade").</summary>
    public const string Avaliacao = "avaliacao";

    public const string Pagina = "pagina";
    public const string Tamanho = "tamanho";
    public const string Ordenacao = "ordenacao";
    public const string Busca = "busca";
    public const string DependeDe = "dependeDe";

    /// <summary>Formato da exportação da listagem: "csv" ou "xlsx".</summary>
    public const string Formato = "formato";

    // Parâmetros do painel (RN80, RN85 a RN87): reservados para não colidir com um filtro de campo de mesmo nome.
    public const string AgruparPor = "agruparPor";
    public const string Metricas = "metricas";
    public const string OrdenarPor = "ordenarPor";
    public const string Limite = "limite";
    public const string De = "de";
    public const string Ate = "ate";

    /// <summary>
    /// Colunas de sistema do registro. No corpo do POST/PUT não são campos: a "versao" é lida pelo PUT,
    /// o "equipeId" pelo POST (RN27) e as demais são ignoradas.
    /// </summary>
    public static readonly IReadOnlySet<string> DoRegistro =
        new HashSet<string>([Id, Versao, CriadoEm, AtualizadoEm, EquipeId, CriadoPor, AtualizadoPor, SituacaoAprovacao, UltimaDecisao, Avaliacao], StringComparer.OrdinalIgnoreCase);

    /// <summary>Parâmetros de URL que não são filtros.</summary>
    public static readonly IReadOnlySet<string> DaConsulta =
        new HashSet<string>([Pagina, Tamanho, Ordenacao, Busca, DependeDe, Formato, AgruparPor, Metricas, OrdenarPor, Limite, De, Ate],
            StringComparer.OrdinalIgnoreCase);

    public static bool EhReservada(string chave) => DoRegistro.Contains(chave) || DaConsulta.Contains(chave);

    /// <summary>
    /// Falha com mensagem clara se algum campo do grupo usa uma chave reservada.
    /// Chamado ao carregar os metadados: um metadado assim é erro de configuração.
    /// </summary>
    public static void ValidarCampos(Grupo grupo)
    {
        var invalidos = grupo.Entidades
            .SelectMany(e => e.Campos.Select(c => (Entidade: e, Campo: c)))
            .Where(x => EhReservada(x.Campo.Chave))
            .Select(x => $"{x.Entidade.Chave}.{x.Campo.Chave}")
            .ToList();

        if (invalidos.Count > 0)
        {
            throw new InvalidOperationException(
                $"Metadados inválidos no grupo '{grupo.Chave}': os campos {string.Join(", ", invalidos)} usam chave reservada " +
                $"({string.Join(", ", DoRegistro.Concat(DaConsulta))}). Renomeie o campo com um script em database/scripts (RN14).");
        }
    }
}
