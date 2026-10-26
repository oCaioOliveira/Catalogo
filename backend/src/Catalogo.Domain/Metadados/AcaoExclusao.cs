namespace Catalogo.Domain.Metadados;

/// <summary>
/// O que acontece com quem aponta para um registro quando ele é excluído (RN11).
/// Só existe em campos REFERENCIA (CHECK CK_Campo_Referencia).
/// </summary>
public enum AcaoExclusao
{
    /// <summary>Bloqueia a exclusão enquanto houver registros apontando para ele.</summary>
    Restringir,

    /// <summary>Exclui junto os registros que apontam para ele (feito pela aplicação).</summary>
    Cascata,
}

/// <summary>Converte <see cref="AcaoExclusao"/> de e para o texto do banco ("RESTRINGIR", "CASCATA").</summary>
public static class AcaoExclusaoConversor
{
    public static string ParaTexto(AcaoExclusao acao) => acao switch
    {
        AcaoExclusao.Restringir => "RESTRINGIR",
        AcaoExclusao.Cascata => "CASCATA",
        _ => throw new ArgumentOutOfRangeException(nameof(acao), acao, "AcaoExclusao sem texto correspondente."),
    };

    public static AcaoExclusao DeTexto(string texto) => texto switch
    {
        "RESTRINGIR" => AcaoExclusao.Restringir,
        "CASCATA" => AcaoExclusao.Cascata,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, "Texto de AcaoExclusao desconhecido."),
    };
}
