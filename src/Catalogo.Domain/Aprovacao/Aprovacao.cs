namespace Catalogo.Domain.Aprovacao;

/// <summary>Situação de aprovação de um registro (coluna Registro.SituacaoAprovacao, RN34).</summary>
public enum SituacaoAprovacao
{
    Pendente,
    Aprovado,
    Rejeitado,
}

/// <summary>Decisão do gestor sobre um registro pendente (RN36).</summary>
public enum DecisaoAprovacao
{
    Aprovar,
    Rejeitar,
}

/// <summary>Conversores para o texto do banco e da API ("PENDENTE", "APROVAR"...).</summary>
public static class AprovacaoConversor
{
    public static string ParaTexto(SituacaoAprovacao situacao) => situacao switch
    {
        SituacaoAprovacao.Pendente => "PENDENTE",
        SituacaoAprovacao.Aprovado => "APROVADO",
        SituacaoAprovacao.Rejeitado => "REJEITADO",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoAprovacao SituacaoDeTexto(string texto) => texto switch
    {
        "PENDENTE" => SituacaoAprovacao.Pendente,
        "APROVADO" => SituacaoAprovacao.Aprovado,
        "REJEITADO" => SituacaoAprovacao.Rejeitado,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, null),
    };

    /// <summary>Situação vinda da API (sem diferenciar maiúsculas). Falso se não for uma situação.</summary>
    public static bool TentarConverterSituacao(string? texto, out SituacaoAprovacao situacao)
    {
        situacao = default;
        switch (texto?.Trim().ToUpperInvariant())
        {
            case "PENDENTE": situacao = SituacaoAprovacao.Pendente; return true;
            case "APROVADO": situacao = SituacaoAprovacao.Aprovado; return true;
            case "REJEITADO": situacao = SituacaoAprovacao.Rejeitado; return true;
            default: return false;
        }
    }

    public static string ParaTexto(DecisaoAprovacao decisao) => decisao switch
    {
        DecisaoAprovacao.Aprovar => "APROVAR",
        DecisaoAprovacao.Rejeitar => "REJEITAR",
        _ => throw new ArgumentOutOfRangeException(nameof(decisao), decisao, null),
    };

    /// <summary>Decisão vinda da API ("APROVAR" ou "REJEITAR", sem diferenciar maiúsculas).</summary>
    public static bool TentarConverterDecisao(string? texto, out DecisaoAprovacao decisao)
    {
        decisao = default;
        switch (texto?.Trim().ToUpperInvariant())
        {
            case "APROVAR": decisao = DecisaoAprovacao.Aprovar; return true;
            case "REJEITAR": decisao = DecisaoAprovacao.Rejeitar; return true;
            default: return false;
        }
    }
}

/// <summary>
/// Configurações de um tipo de item (Entidade) para uma equipe (tabela catalogo.ConfiguracaoEntidadeEquipe, RN33).
/// Ausência de linha = tudo desativado. Só existe para entidades POR EQUIPE.
/// </summary>
public sealed class ConfiguracaoEntidadeEquipe
{
    public Guid EntidadeId { get; set; }
    public Guid EquipeId { get; set; }

    /// <summary>Registros criados por EXTERNOS nascem PENDENTE e precisam de decisão de um GESTOR (RN34).</summary>
    public bool RequerAprovacao { get; set; }

    public Guid AtualizadoPorId { get; set; }

    /// <summary>UTC.</summary>
    public DateTime AtualizadoEm { get; set; }
}
