using System.Text.Json.Nodes;

namespace Catalogo.Application.Dtos;

/// <summary>
/// Registro devolvido pela API.
/// <see cref="Dados"/> é PLANO: campos simples e referências juntos no mesmo objeto. Cada referência
/// vem como <c>{ "id": "&lt;guid&gt;", "rotulo": "&lt;texto&gt;" }</c> (RN20). Na entrada (POST e PUT)
/// a referência continua sendo só o GUID. As colunas de sistema ficam fora de <see cref="Dados"/> (RN13).
/// </summary>
public sealed record RegistroDto(
    Guid Id,
    // Equipe dona do registro; nula nas entidades globais (RN27).
    Guid? EquipeId,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    // rowversion em Base64. É enviada de volta no corpo do PUT (concorrência otimista, RN12).
    string Versao,
    // Quem criou e quem alterou por último: { id, matricula, nome } (RN32).
    UsuarioResumoDto? CriadoPor,
    UsuarioResumoDto? AtualizadoPor,
    // PENDENTE, APROVADO ou REJEITADO (RN34).
    string SituacaoAprovacao,
    // Última decisão de aprovação, se houve (RN36). As anteriores estão no histórico.
    UltimaDecisaoDto? UltimaDecisao,
    // Médias das avaliações, quando a entidade tem modelo de avaliação ativo na equipe do registro (RN48). Nulo sem modelo.
    AvaliacaoRegistroDto? Avaliacao,
    JsonObject Dados);

/// <summary>Última decisão sobre o registro: "APROVAR" ou "REJEITAR", com a justificativa (RN36).</summary>
public sealed record UltimaDecisaoDto(string Decisao, string? Justificativa, UsuarioResumoDto? DecididoPor, DateTime DecididoEm);
