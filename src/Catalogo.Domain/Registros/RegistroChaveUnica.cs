namespace Catalogo.Domain.Registros;

/// <summary>
/// Hash de uma restrição única de um registro (tabela catalogo.RegistroChaveUnica).
/// O banco tem UNIQUE (RestricaoUnicaId, ValorHash): dois registros com os mesmos
/// valores normalizados na mesma restrição são barrados mesmo com concorrência (RN09).
/// </summary>
public sealed class RegistroChaveUnica
{
    public Guid RestricaoUnicaId { get; set; }
    public Guid EntidadeId { get; set; }
    public Guid RegistroId { get; set; }

    /// <summary>SHA-256 (32 bytes) calculado pela <c>CalculadoraChaveUnica</c>.</summary>
    public byte[] ValorHash { get; set; } = [];
}
