using System.Text.Json.Nodes;

namespace Catalogo.Domain.Historico;

/// <summary>Tipo do que foi alterado (coluna HistoricoAlteracao.AlvoTipo).</summary>
public enum AlvoHistorico
{
    Registro,
    Usuario,
    Equipe,

    /// <summary>Configuração de uma entidade para uma equipe (RN33): AlvoId = equipe, EntidadeId = entidade.</summary>
    Configuracao,

    /// <summary>Modelo de avaliação (RN40): AlvoId = equipe, EntidadeId = entidade. O id do modelo vai nos dados.</summary>
    ModeloAvaliacao,

    /// <summary>Avaliação de um item (RN45): AlvoId = registro avaliado; o autor é quem fez a operação.</summary>
    Avaliacao,

    /// <summary>
    /// Metadados (RN52): AlvoId = grupo e EntidadeId nulo (a FK impediria excluir a entidade). A entidade, o campo... e
    /// a quantidade de registros reescritos vão nos dados.
    /// </summary>
    Metadado,
}

/// <summary>O que aconteceu com o alvo (coluna HistoricoAlteracao.Operacao).</summary>
public enum OperacaoHistorico
{
    Criacao,
    Alteracao,
    Exclusao,

    /// <summary>Decisão de aprovar um registro pendente (RN36).</summary>
    Aprovacao,

    /// <summary>Decisão de rejeitar um registro pendente (RN36).</summary>
    Rejeicao,
}

/// <summary>
/// De onde veio uma gravação (coluna HistoricoAlteracao.Origem, V011). Sem origem (nulo) = cadastro manual, pela tela ou pela API.
/// </summary>
public enum OrigemHistorico
{
    /// <summary>Gravado por uma importação de planilha (RN97); o CorrelacaoId é o da importação.</summary>
    Importacao,

    /// <summary>
    /// Gravado pela migração dos relatórios de testes de desempenho do SharePoint (V017, docs/migracao-sharepoint.md); o
    /// CorrelacaoId é um só para toda a migração.
    /// </summary>
    MigracaoSharePoint,
}

/// <summary>Conversores para o texto do banco ("REGISTRO", "CRIACAO"...).</summary>
public static class HistoricoConversor
{
    public static string ParaTexto(AlvoHistorico alvo) => alvo switch
    {
        AlvoHistorico.Registro => "REGISTRO",
        AlvoHistorico.Usuario => "USUARIO",
        AlvoHistorico.Equipe => "EQUIPE",
        AlvoHistorico.Configuracao => "CONFIGURACAO",
        AlvoHistorico.ModeloAvaliacao => "MODELO_AVALIACAO",
        AlvoHistorico.Avaliacao => "AVALIACAO",
        AlvoHistorico.Metadado => "METADADO",
        _ => throw new ArgumentOutOfRangeException(nameof(alvo), alvo, null),
    };

    public static AlvoHistorico AlvoDeTexto(string texto) => texto switch
    {
        "REGISTRO" => AlvoHistorico.Registro,
        "USUARIO" => AlvoHistorico.Usuario,
        "EQUIPE" => AlvoHistorico.Equipe,
        "CONFIGURACAO" => AlvoHistorico.Configuracao,
        "MODELO_AVALIACAO" => AlvoHistorico.ModeloAvaliacao,
        "AVALIACAO" => AlvoHistorico.Avaliacao,
        "METADADO" => AlvoHistorico.Metadado,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, null),
    };

    public static string ParaTexto(OperacaoHistorico operacao) => operacao switch
    {
        OperacaoHistorico.Criacao => "CRIACAO",
        OperacaoHistorico.Alteracao => "ALTERACAO",
        OperacaoHistorico.Exclusao => "EXCLUSAO",
        OperacaoHistorico.Aprovacao => "APROVACAO",
        OperacaoHistorico.Rejeicao => "REJEICAO",
        _ => throw new ArgumentOutOfRangeException(nameof(operacao), operacao, null),
    };

    public static OperacaoHistorico OperacaoDeTexto(string texto) => texto switch
    {
        "CRIACAO" => OperacaoHistorico.Criacao,
        "ALTERACAO" => OperacaoHistorico.Alteracao,
        "EXCLUSAO" => OperacaoHistorico.Exclusao,
        "APROVACAO" => OperacaoHistorico.Aprovacao,
        "REJEICAO" => OperacaoHistorico.Rejeicao,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, null),
    };

    public static string ParaTexto(OrigemHistorico origem) => origem switch
    {
        OrigemHistorico.Importacao => "IMPORTACAO",
        OrigemHistorico.MigracaoSharePoint => "MIGRACAO_SHAREPOINT",
        _ => throw new ArgumentOutOfRangeException(nameof(origem), origem, null),
    };

    public static OrigemHistorico OrigemDeTexto(string texto) => texto switch
    {
        "IMPORTACAO" => OrigemHistorico.Importacao,
        "MIGRACAO_SHAREPOINT" => OrigemHistorico.MigracaoSharePoint,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, null),
    };
}

/// <summary>
/// Uma linha do histórico (tabela catalogo.HistoricoAlteracao, RN32): quem alterou o quê, quando, e o
/// objeto antes e depois. Gravada no MESMO SaveChanges da operação (docs/adr/0018).
/// Não tem FK para o alvo: sobrevive à exclusão do registro.
/// </summary>
public sealed class HistoricoAlteracao
{
    /// <summary>Gerado pelo banco (IDENTITY).</summary>
    public long Id { get; set; }

    public AlvoHistorico AlvoTipo { get; set; }
    public Guid AlvoId { get; set; }

    /// <summary>Só para <see cref="AlvoHistorico.Registro"/>.</summary>
    public Guid? EntidadeId { get; set; }

    /// <summary>Equipe do alvo no momento da alteração (nula para registros globais e usuários).</summary>
    public Guid? EquipeId { get; set; }

    public OperacaoHistorico Operacao { get; set; }

    /// <summary>Quem fez a alteração.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Cópia da matrícula de quem fez, para leitura fácil direto no banco.</summary>
    public string UsuarioMatricula { get; set; } = "";

    public DateTime OcorridoEm { get; set; }

    /// <summary>Agrupa as linhas de uma mesma operação (ex.: todos os registros de uma exclusão em cascata).</summary>
    public Guid CorrelacaoId { get; set; }

    /// <summary>De onde veio a gravação: nula no cadastro manual; <see cref="OrigemHistorico.Importacao"/> numa importação (V011).</summary>
    public OrigemHistorico? Origem { get; set; }

    /// <summary>Justificativa de uma APROVACAO ou REJEICAO (RN36). Nula nas demais operações.</summary>
    public string? Justificativa { get; set; }

    /// <summary>Objeto plano antes da alteração (nulo na criação). NUNCA contém senha.</summary>
    public string? DadosAntes { get; set; }

    /// <summary>Objeto plano depois da alteração (nulo na exclusão). NUNCA contém senha.</summary>
    public string? DadosDepois { get; set; }
}

/// <summary>Uma diferença entre o antes e o depois de um campo.</summary>
public sealed record MudancaCampo(string Campo, JsonNode? De, JsonNode? Para);

/// <summary>Calcula as mudanças por campo entre dois objetos planos (RN32).</summary>
public static class ComparadorAlteracoes
{
    /// <summary>
    /// Campos cujo valor mudou, foram incluídos ou removidos. Nulo e ausente são a mesma coisa.
    /// Ordem: os campos na ordem do "antes", depois os que só existem no "depois".
    /// </summary>
    public static IReadOnlyList<MudancaCampo> Comparar(JsonObject? antes, JsonObject? depois)
    {
        var chaves = (antes?.Select(p => p.Key) ?? [])
            .Concat(depois?.Select(p => p.Key) ?? [])
            .Distinct(StringComparer.Ordinal);

        var mudancas = new List<MudancaCampo>();
        foreach (var chave in chaves)
        {
            var de = antes?[chave];
            var para = depois?[chave];
            if (!JsonNode.DeepEquals(de, para))
                mudancas.Add(new MudancaCampo(chave, de?.DeepClone(), para?.DeepClone()));
        }

        return mudancas;
    }
}
