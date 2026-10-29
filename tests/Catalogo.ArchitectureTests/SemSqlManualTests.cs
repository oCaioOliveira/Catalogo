namespace Catalogo.ArchitectureTests;

/// <summary>
/// Toda consulta da aplicação é EF Core em LINQ (docs/adr/0011). SQL escrito à mão no código
/// abriria espaço para SQL injection e fugiria do mapeamento. Os scripts de database/scripts/
/// continuam em SQL: eles são o schema, não consultas da aplicação.
/// </summary>
public class SemSqlManualTests
{
    // Trechos que indicam SQL montado à mão ou micro-ORM.
    private static readonly string[] Proibidos = ["SqlQueryRaw", "SqlQuery<", "FromSql", "ExecuteSql", "Dapper"];

    [Fact]
    public void CodigoFonte_NaoUsaSqlEscritoAMaoNemDapper()
    {
        var ocorrencias = Directory.EnumerateFiles(Path.Combine(Repositorio.Raiz, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(Repositorio.ForaDeBinObj)
            .SelectMany(arquivo => File.ReadAllLines(arquivo)
                .Select((linha, indice) => (Linha: linha, Numero: indice + 1))
                .SelectMany(l => Proibidos
                    .Where(p => l.Linha.Contains(p, StringComparison.Ordinal))
                    .Select(p => $"{p} em {Path.GetRelativePath(Repositorio.Raiz, arquivo)}:{l.Numero}")))
            .ToList();

        Assert.True(ocorrencias.Count == 0, "SQL escrito à mão encontrado: " + string.Join("; ", ocorrencias));
    }

    [Fact]
    public void Infrastructure_NaoReferenciaDapper()
    {
        var referencias = Repositorio.Infrastructure.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain("Dapper", referencias);
    }
}
