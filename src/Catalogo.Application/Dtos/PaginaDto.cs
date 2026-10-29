namespace Catalogo.Application.Dtos;

/// <summary>Resultado paginado (RN17). <see cref="Pagina"/> começa em 1.</summary>
public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total, int TotalPaginas);

/// <summary>Uma opção de campo REFERENCIA (RN21): o id do registro e o seu rótulo (RN20).</summary>
/// <summary>
/// Uma opção de campo REFERENCIA (RN21). <see cref="SituacaoAprovacao"/>: só aparecem itens APROVADOS e os não aprovados de quem
/// preenche (RN38); o front marca os outros como "(pendente)" ou "(rejeitado)".
/// </summary>
public sealed record OpcaoDto(Guid Id, string? Rotulo, string SituacaoAprovacao = "APROVADO");
