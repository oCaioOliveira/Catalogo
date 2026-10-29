namespace Catalogo.Application.Dtos;

/// <summary>
/// Um item que o usuário criou ou alterou por último numa área de gestão (RN96): "Continuar de onde parou" na página Início.
/// <see cref="Entidade"/> é a chave do tipo de item; <see cref="Rotulo"/>, o valor do campo de exibição (nulo sem ele, RN20);
/// <see cref="OcorridoEm"/>, a última criação ou alteração feita por ELE (UTC).
/// </summary>
public sealed record RecenteDto(Guid Id, string Entidade, string? Rotulo, DateTime OcorridoEm);
