namespace Catalogo.Application.Dtos;

// DTOs da aprovação de registros (RN33 a RN38). Os de ENTRADA têm propriedades anuláveis de propósito:
// a validação é do caso de uso, com mensagens em português.

/// <summary>PUT .../aprovacao: { "ativa": true | false }.</summary>
public sealed record ConfiguracaoAprovacaoEntrada(bool? Ativa);

/// <summary>Configuração de aprovação de uma entidade numa equipe. Sem configuração gravada, ativa = false.</summary>
public sealed record ConfiguracaoAprovacaoDto(
    Guid EquipeId, string Grupo, string Entidade, bool Ativa, UsuarioResumoDto? AtualizadoPor, DateTime? AtualizadoEm);

/// <summary>POST .../registros/{id}/aprovacao: { "decisao": "APROVAR" | "REJEITAR", "justificativa": "..." }.</summary>
public sealed record DecisaoEntrada(string? Decisao, string? Justificativa);

public sealed record EntidadeResumoDto(string Chave, string Nome);

/// <summary>Um item da fila de aprovação (GET /api/aprovacoes/pendentes).</summary>
public sealed record PendenteDto(
    Guid Id, string Grupo, EntidadeResumoDto Entidade, EquipeResumoDto? Equipe, string? Rotulo, UsuarioResumoDto? CriadoPor, DateTime CriadoEm);
