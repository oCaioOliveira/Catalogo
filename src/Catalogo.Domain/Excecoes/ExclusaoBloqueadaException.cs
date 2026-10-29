namespace Catalogo.Domain.Excecoes;

/// <summary>
/// A exclusão foi impedida por referências RESTRINGIR de registros que não seriam excluídos (RN16).
/// Nada foi excluído. Vira HTTP 409 com a lista <see cref="Dependencias"/>.
/// </summary>
public sealed class ExclusaoBloqueadaException(IReadOnlyList<DependenciaBloqueante> dependencias)
    : ExcecaoDominio("O registro não pode ser excluído porque outros registros dependem dele.", CodigosErro.ExclusaoBloqueada)
{
    public IReadOnlyList<DependenciaBloqueante> Dependencias { get; } = dependencias;
}

/// <summary>
/// Um grupo de registros que impede a exclusão: "N registros de {Entidade} apontam para cá pelo campo {Campo}".
/// </summary>
/// <param name="Entidade">Chave da entidade dos registros que apontam (ex.: "endpoint_servico").</param>
/// <param name="Campo">Chave do campo REFERENCIA com RESTRINGIR (ex.: "projeto").</param>
/// <param name="Quantidade">Quantos registros diferentes apontam.</param>
/// <param name="Exemplos">Até 5 rótulos desses registros (o id, quando não há rótulo).</param>
public sealed record DependenciaBloqueante(string Entidade, string Campo, int Quantidade, IReadOnlyList<string> Exemplos);
