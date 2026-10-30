namespace Catalogo.Infrastructure.Persistencia.Tabelas;

/// <summary>
/// cat.CATTB025_CHAVE_JWT (V018): a chave de assinatura do JWT, gerada pela própria API na primeira subida (docs/adr/0053).
/// Uma linha só, sempre com o id <see cref="IdUnico"/>: a chave primária impede que duas instâncias subindo juntas gravem duas chaves.
/// </summary>
public sealed class ChaveJwtTabela
{
    public static readonly Guid IdUnico = new("8c1f0e3a-4b5d-4e6f-9a7b-1c2d3e4f5a60");

    public Guid Id { get; set; }

    /// <summary>A chave, em Base64 (64 bytes aleatórios).</summary>
    public string Chave { get; set; } = "";

    public DateTime CriadoEm { get; set; }
}
