using Catalogo.Domain.Aprovacao;

namespace Catalogo.Domain.Registros;

/// <summary>
/// Um item cadastrado (tabela catalogo.Registro): um projeto, um relatório, um cenário...
/// Ver docs/modelo-de-dados.md.
/// </summary>
public sealed class Registro
{
    /// <summary>Gerado ao adicionar o registro (GUID sequencial gerado pelo EF Core).</summary>
    public Guid Id { get; set; }

    public Guid EntidadeId { get; set; }

    /// <summary>Equipe dona do registro. Nula nas entidades globais (RN27). Não muda depois de criado.</summary>
    public Guid? EquipeId { get; set; }

    /// <summary>Quem criou (RN32).</summary>
    public Guid? CriadoPorId { get; set; }

    /// <summary>Quem fez a última alteração (RN32).</summary>
    public Guid? AtualizadoPorId { get; set; }

    /// <summary>PENDENTE, APROVADO (padrão) ou REJEITADO (RN34). Muda só pelo <see cref="FluxoAprovacao"/>.</summary>
    public SituacaoAprovacao SituacaoAprovacao { get; set; } = SituacaoAprovacao.Aprovado;

    /// <summary>Última decisão: quem decidiu (RN36). Todas as decisões ficam no histórico.</summary>
    public Guid? DecididoPorId { get; set; }

    /// <summary>Última decisão: quando (UTC).</summary>
    public DateTime? DecididoEm { get; set; }

    /// <summary>Última decisão: a justificativa.</summary>
    public string? JustificativaDecisao { get; set; }

    /// <summary>
    /// JSON com os campos SIMPLES já validados e normalizados (RN07).
    /// Campos REFERENCIA nunca ficam aqui: ficam em <see cref="Referencias"/>.
    /// </summary>
    public string Dados { get; set; } = "{}";

    /// <summary>Preenchido pelo banco (DEFAULT SYSUTCDATETIME()). Sempre em UTC.</summary>
    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    /// <summary>rowversion do SQL Server: muda a cada UPDATE. Usado na concorrência otimista (RN12).</summary>
    public byte[] Versao { get; set; } = [];

    /// <summary>Campos REFERENCIA preenchidos neste registro (tabela catalogo.RegistroReferencia).</summary>
    public List<RegistroReferencia> Referencias { get; set; } = [];

    /// <summary>Hashes das restrições únicas deste registro (tabela catalogo.RegistroChaveUnica).</summary>
    public List<RegistroChaveUnica> ChavesUnicas { get; set; } = [];

    /// <summary>Valores tipados dos campos simples, para busca e ordenação (tabela catalogo.RegistroValor).</summary>
    public List<RegistroValor> Valores { get; set; } = [];
}
