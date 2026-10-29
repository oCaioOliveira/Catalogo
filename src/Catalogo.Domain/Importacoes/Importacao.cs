namespace Catalogo.Domain.Importacoes;

/// <summary>Situação de uma importação (coluna Importacao.Situacao). Ver RN97.</summary>
public enum SituacaoImportacao
{
    /// <summary>Arquivo guardado e colunas detectadas; ainda sem prévia.</summary>
    Enviada,

    /// <summary>Prévia feita (simulação completa, sem gravar).</summary>
    Validada,

    /// <summary>Itens gravados.</summary>
    Concluida,

    /// <summary>Itens criados pela importação removidos (RN103).</summary>
    Desfeita,

    /// <summary>Nunca executada e com mais de 7 dias (RN104).</summary>
    Expirada,
}

/// <summary>Formato do arquivo enviado (coluna Importacao.Formato).</summary>
public enum FormatoImportacao
{
    Xlsx,
    Csv,
}

/// <summary>Conversores para o texto do banco ("ENVIADA", "XLSX"...).</summary>
public static class ImportacaoConversor
{
    public static string ParaTexto(SituacaoImportacao situacao) => situacao switch
    {
        SituacaoImportacao.Enviada => "ENVIADA",
        SituacaoImportacao.Validada => "VALIDADA",
        SituacaoImportacao.Concluida => "CONCLUIDA",
        SituacaoImportacao.Desfeita => "DESFEITA",
        SituacaoImportacao.Expirada => "EXPIRADA",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoImportacao SituacaoDeTexto(string texto) => texto switch
    {
        "ENVIADA" => SituacaoImportacao.Enviada,
        "VALIDADA" => SituacaoImportacao.Validada,
        "CONCLUIDA" => SituacaoImportacao.Concluida,
        "DESFEITA" => SituacaoImportacao.Desfeita,
        "EXPIRADA" => SituacaoImportacao.Expirada,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, null),
    };

    public static string ParaTexto(FormatoImportacao formato) => formato switch
    {
        FormatoImportacao.Xlsx => "XLSX",
        FormatoImportacao.Csv => "CSV",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, null),
    };

    public static FormatoImportacao FormatoDeTexto(string texto) => texto switch
    {
        "XLSX" => FormatoImportacao.Xlsx,
        "CSV" => FormatoImportacao.Csv,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, null),
    };
}

/// <summary>
/// Uma importação de itens de UM tipo de item (tabela catalogo.Importacao, V011). Mapeada direto pelo EF, como o Registro.
/// Opções, mapeamento e resumo ficam em JSON (formatos em <see cref="OpcoesImportacao"/>, <see cref="MapeamentoImportacao"/> e
/// <see cref="ResumoImportacao"/>). O arquivo enviado fica em <see cref="ImportacaoArquivo"/>.
/// </summary>
public sealed class Importacao
{
    public Guid Id { get; set; }
    public Guid GrupoId { get; set; }
    public Guid EntidadeId { get; set; }

    /// <summary>Quem importou.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Equipe das linhas sem a coluna Equipe (escolhida nas opções).</summary>
    public Guid? EquipePadraoId { get; set; }

    public string NomeArquivo { get; set; } = "";
    public FormatoImportacao Formato { get; set; }
    public SituacaoImportacao Situacao { get; set; } = SituacaoImportacao.Enviada;

    /// <summary>JSON de <see cref="OpcoesImportacao"/> usado na última prévia.</summary>
    public string? Opcoes { get; set; }

    /// <summary>JSON de <see cref="MapeamentoImportacao"/> usado na última prévia.</summary>
    public string? Mapeamento { get; set; }

    /// <summary>JSON de <see cref="ResumoImportacao"/>: os totais e o resultado de cada linha.</summary>
    public string? Resumo { get; set; }

    /// <summary>O mesmo CorrelacaoId das linhas de histórico dos itens gravados por esta importação.</summary>
    public Guid CorrelacaoId { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? ConcluidaEm { get; set; }
    public DateTime? DesfeitaEm { get; set; }

    /// <summary>rowversion: protege a execução e o desfazer de dois cliques ao mesmo tempo.</summary>
    public byte[] Versao { get; set; } = [];
}

/// <summary>O arquivo enviado (tabela catalogo.ImportacaoArquivo). Apagado pela limpeza depois de 7 dias (RN104).</summary>
public sealed class ImportacaoArquivo
{
    public Guid ImportacaoId { get; set; }
    public byte[] Conteudo { get; set; } = [];
}
