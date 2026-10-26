namespace Catalogo.Domain.Avaliacoes;

// Avaliação de itens (RN40 a RN48): tabelas ModeloAvaliacao, CriterioAvaliacao, Avaliacao e
// AvaliacaoResposta (V005), mapeadas direto pelo EF, como Registro.

/// <summary>Tipo de um critério (coluna CriterioAvaliacao.Tipo, RN42).</summary>
public enum TipoCriterio
{
    /// <summary>Nota de 1 a 5 estrelas, com comentário opcional ou obrigatório.</summary>
    Estrelas,

    /// <summary>Texto livre.</summary>
    Texto,
}

/// <summary>Se o avaliador justifica a nota em texto (coluna CriterioAvaliacao.Comentario). Só em ESTRELAS (RN42).</summary>
public enum ExigenciaComentario
{
    Nenhum,
    Opcional,
    Obrigatorio,
}

/// <summary>Conversores para o texto do banco e da API ("ESTRELAS", "OPCIONAL"...).</summary>
public static class AvaliacaoConversor
{
    public static string ParaTexto(TipoCriterio tipo) => tipo switch
    {
        TipoCriterio.Estrelas => "ESTRELAS",
        TipoCriterio.Texto => "TEXTO",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoCriterio TipoDeTexto(string texto) =>
        TentarConverterTipo(texto, out var tipo) ? tipo : throw new ArgumentOutOfRangeException(nameof(texto), texto, null);

    /// <summary>Tipo vindo da API (sem diferenciar maiúsculas). Falso se não for um tipo.</summary>
    public static bool TentarConverterTipo(string? texto, out TipoCriterio tipo)
    {
        tipo = default;
        switch (texto?.Trim().ToUpperInvariant())
        {
            case "ESTRELAS": tipo = TipoCriterio.Estrelas; return true;
            case "TEXTO": tipo = TipoCriterio.Texto; return true;
            default: return false;
        }
    }

    public static string ParaTexto(ExigenciaComentario comentario) => comentario switch
    {
        ExigenciaComentario.Nenhum => "NENHUM",
        ExigenciaComentario.Opcional => "OPCIONAL",
        ExigenciaComentario.Obrigatorio => "OBRIGATORIO",
        _ => throw new ArgumentOutOfRangeException(nameof(comentario), comentario, null),
    };

    public static ExigenciaComentario ComentarioDeTexto(string texto) =>
        TentarConverterComentario(texto, out var comentario) ? comentario : throw new ArgumentOutOfRangeException(nameof(texto), texto, null);

    /// <summary>Exigência de comentário vinda da API (sem diferenciar maiúsculas). Falso se não for uma das três.</summary>
    public static bool TentarConverterComentario(string? texto, out ExigenciaComentario comentario)
    {
        comentario = default;
        switch (texto?.Trim().ToUpperInvariant())
        {
            case "NENHUM": comentario = ExigenciaComentario.Nenhum; return true;
            case "OPCIONAL": comentario = ExigenciaComentario.Opcional; return true;
            case "OBRIGATORIO": comentario = ExigenciaComentario.Obrigatorio; return true;
            default: return false;
        }
    }
}

/// <summary>
/// O formulário de avaliação de uma entidade numa equipe (tabela catalogo.ModeloAvaliacao). No máximo um
/// ATIVO por entidade e equipe (RN41). Nunca é excluído: é desativado, e as avaliações dele continuam visíveis.
/// </summary>
public sealed class ModeloAvaliacao
{
    /// <summary>Gerado ao adicionar (GUID sequencial gerado pelo EF Core), para o histórico usar antes do SaveChanges.</summary>
    public Guid Id { get; set; }

    public Guid EntidadeId { get; set; }
    public Guid EquipeId { get; set; }
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public Guid CriadoPorId { get; set; }

    /// <summary>Preenchido pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; set; }

    public Guid? AtualizadoPorId { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>rowversion: concorrência otimista do PUT (mesmo padrão dos registros, RN12).</summary>
    public byte[] Versao { get; set; } = [];

    public List<CriterioAvaliacao> Criterios { get; set; } = [];

    /// <summary>Critérios ativos, na ordem do formulário.</summary>
    public IEnumerable<CriterioAvaliacao> CriteriosAtivos => Criterios.Where(c => c.Ativo).OrderBy(c => c.Ordem).ThenBy(c => c.Chave, StringComparer.Ordinal);

    public CriterioAvaliacao? ObterCriterio(string chave) => Criterios.FirstOrDefault(c => c.Chave == chave);
}

/// <summary>Uma pergunta do modelo (tabela catalogo.CriterioAvaliacao, RN42).</summary>
public sealed class CriterioAvaliacao
{
    public const int TamanhoMaximoTextoPadrao = 2000;
    public const int TamanhoMaximoTextoLimite = 4000;

    public Guid Id { get; set; }
    public Guid ModeloAvaliacaoId { get; set; }

    /// <summary>Identificador estável no modelo (padrão ^[a-z0-9_]+$). É a chave usada nas respostas e na listagem.</summary>
    public string Chave { get; set; } = "";

    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public TipoCriterio Tipo { get; set; }
    public bool Obrigatorio { get; set; }

    /// <summary>Só ESTRELAS. Em TEXTO é sempre NENHUM.</summary>
    public ExigenciaComentario Comentario { get; set; } = ExigenciaComentario.Nenhum;

    /// <summary>Tamanho máximo do texto livre ou do comentário (1 a 4000).</summary>
    public int TamanhoMaximoTexto { get; set; } = TamanhoMaximoTextoPadrao;

    public int Ordem { get; set; }

    /// <summary>Inativo: some do formulário e das médias, mas as respostas antigas ficam (RN43).</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Preenchido pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; set; }
}

/// <summary>
/// A avaliação de UM usuário sobre UM registro, num modelo (tabela catalogo.Avaliacao). Única por
/// (registro, modelo, usuário) (RN45). Excluída junto com o registro (CASCADE).
/// </summary>
public sealed class Avaliacao
{
    /// <summary>Gerado ao adicionar (GUID sequencial gerado pelo EF Core).</summary>
    public Guid Id { get; set; }

    public Guid RegistroId { get; set; }
    public Guid ModeloAvaliacaoId { get; set; }
    public Guid UsuarioId { get; set; }

    /// <summary>Preenchido pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    /// <summary>rowversion: concorrência otimista ao editar (RN45).</summary>
    public byte[] Versao { get; set; } = [];

    public List<AvaliacaoResposta> Respostas { get; set; } = [];
}

/// <summary>A resposta a um critério (tabela catalogo.AvaliacaoResposta).</summary>
public sealed class AvaliacaoResposta
{
    public Guid AvaliacaoId { get; set; }
    public Guid CriterioAvaliacaoId { get; set; }

    /// <summary>1 a 5 nos critérios ESTRELAS; nulo nos TEXTO.</summary>
    public byte? Estrelas { get; set; }

    /// <summary>A resposta de um critério TEXTO ou o comentário de um critério ESTRELAS.</summary>
    public string? Texto { get; set; }
}
