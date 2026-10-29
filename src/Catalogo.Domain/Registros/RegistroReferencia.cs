namespace Catalogo.Domain.Registros;

/// <summary>
/// Valor de um campo REFERENCIA (tabela catalogo.RegistroReferencia): "o registro de origem,
/// no campo X, aponta para o registro de destino". Cada campo aponta para no máximo 1 registro.
/// As entidades de origem e destino ficam repetidas aqui para as FKs compostas do banco
/// garantirem que campo, origem e destino são das entidades certas.
/// </summary>
public sealed class RegistroReferencia
{
    public Guid RegistroOrigemId { get; set; }
    public Guid EntidadeOrigemId { get; set; }
    public Guid CampoId { get; set; }
    public Guid RegistroDestinoId { get; set; }
    public Guid EntidadeDestinoId { get; set; }
}
