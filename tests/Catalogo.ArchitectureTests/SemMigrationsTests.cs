using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Catalogo.ArchitectureTests;

/// <summary>
/// O schema é gerido pelos scripts de database/scripts/, nunca pelo EF Core (docs/adr/0003).
/// </summary>
public class SemMigrationsTests
{
    [Fact]
    public void Infrastructure_NaoTemMigrationsNemModelSnapshot()
    {
        var tiposDeMigration = Repositorio.Infrastructure.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) || typeof(ModelSnapshot).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(tiposDeMigration.Count == 0, "Migrations encontradas: " + string.Join(", ", tiposDeMigration));
    }

    [Fact]
    public void CodigoFonte_NaoCriaNemApagaOSchemaPeloEf()
    {
        // EnsureCreated/EnsureDeleted/Migrate criariam ou apagariam tabelas fora dos scripts versionados.
        // Procura a CHAMADA (com parêntese), para não acusar os comentários que explicam a regra.
        string[] proibidos = ["EnsureCreated(", "EnsureCreatedAsync(", "EnsureDeleted(", "EnsureDeletedAsync(", ".Migrate(", ".MigrateAsync("];

        var ocorrencias = Directory.EnumerateFiles(Path.Combine(Repositorio.Raiz, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(Repositorio.Raiz, "tests"), "*.cs", SearchOption.AllDirectories))
            .Where(Repositorio.ForaDeBinObj)
            .Where(arquivo => !arquivo.EndsWith(nameof(SemMigrationsTests) + ".cs", StringComparison.Ordinal))
            .SelectMany(arquivo => proibidos
                .Where(p => File.ReadAllText(arquivo).Contains(p, StringComparison.Ordinal))
                .Select(p => $"{p} em {Path.GetRelativePath(Repositorio.Raiz, arquivo)}"))
            .ToList();

        Assert.True(ocorrencias.Count == 0, string.Join("; ", ocorrencias));
    }
}
