using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure.Persistencia;

/// <summary>
/// Reconhece erros do SQL Server que viram regras de negócio.
/// </summary>
internal static partial class TradutorErrosSqlServer
{
    /// <summary>Erro 2601: linha duplicada num índice único.</summary>
    public const int ErroIndiceUnicoDuplicado = 2601;

    /// <summary>Erro 2627: violação de PRIMARY KEY ou UNIQUE constraint.</summary>
    public const int ErroRestricaoUnicaDuplicada = 2627;

    /// <summary>Constraint que barra hashes repetidos em catalogo.RegistroChaveUnica (RN09).</summary>
    public const string ConstraintValorHash = "UQ_RegistroChaveUnica_Valor";

    /// <summary>
    /// Indica se a falha do SaveChanges foi por chave duplicada (2601/2627). Quando a constraint
    /// violada é a <see cref="ConstraintValorHash"/>, devolve também o id da restrição única.
    /// </summary>
    public static bool EhViolacaoUnicidade(DbUpdateException excecao, out Guid? restricaoUnicaId)
    {
        restricaoUnicaId = null;

        if (excecao.InnerException is not SqlException erroSql
            || (erroSql.Number != ErroIndiceUnicoDuplicado && erroSql.Number != ErroRestricaoUnicaDuplicada))
        {
            return false;
        }

        // A mensagem tem o nome da constraint e o valor duplicado, ex.:
        //   "Violation of UNIQUE KEY constraint 'UQ_RegistroChaveUnica_Valor'. ...
        //    The duplicate key value is (8f1c...-..., 0x5A...)."
        // O primeiro GUID depois do nome da constraint é o RestricaoUnicaId (1ª coluna da constraint).
        // Nome da constraint e valores não são traduzidos, então funciona em qualquer idioma do servidor.
        var posicao = erroSql.Message.IndexOf(ConstraintValorHash, StringComparison.Ordinal);
        if (posicao >= 0)
        {
            var guid = GuidNaMensagem().Match(erroSql.Message, posicao);
            if (guid.Success)
                restricaoUnicaId = Guid.Parse(guid.Value);
        }

        return true;
    }

    /// <summary>Erro 547: violação de FOREIGN KEY (ou CHECK).</summary>
    public const int ErroViolacaoDeRestricao = 547;

    /// <summary>
    /// Indica se a falha foi o erro 547, por exemplo: durante uma exclusão, outra requisição criou
    /// uma referência para um dos registros. Procura o SqlException na própria exceção e nas internas.
    /// </summary>
    public static bool EhViolacaoDeReferencia(Exception excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is SqlException { Number: ErroViolacaoDeRestricao })
                return true;
        }

        return false;
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidNaMensagem();
}
