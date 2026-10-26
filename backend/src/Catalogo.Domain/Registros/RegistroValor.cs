namespace Catalogo.Domain.Registros;

/// <summary>
/// Cópia TIPADA de um campo simples de um registro (tabela catalogo.RegistroValor), usada só para
/// filtrar e ordenar em LINQ (docs/adr/0013). Exatamente uma coluna de valor é preenchida.
/// É SEMPRE gravada junto com <see cref="Registro.Dados"/> (ver <see cref="SincronizadorRegistro"/>).
/// </summary>
public sealed class RegistroValor
{
    public Guid RegistroId { get; set; }
    public Guid EntidadeId { get; set; }
    public Guid CampoId { get; set; }

    /// <summary>TEXTO, TEXTO_LONGO e LISTA.</summary>
    public string? ValorTexto { get; set; }

    /// <summary>INTEIRO e DECIMAL (decimal(38,10) no banco).</summary>
    public decimal? ValorNumero { get; set; }

    /// <summary>DATA (meia-noite) e DATA_HORA (em UTC).</summary>
    public DateTime? ValorData { get; set; }

    /// <summary>BOOLEANO.</summary>
    public bool? ValorBooleano { get; set; }
}
