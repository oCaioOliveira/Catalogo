namespace Catalogo.ArchitectureTests;

/// <summary>Pacotes proibidos (docs/adr/0004 e 0011) e Central Package Management.</summary>
public class PacotesTests
{
    [Fact]
    public void PacotesProibidos_NaoAparecemEmNenhumArquivoDeProjeto()
    {
        var violacoes = TodosOsPacotes()
            .Where(p => EhProibido(p.Nome, p.Versao))
            .Select(p => $"{p.Nome} {p.Versao} em {Path.GetRelativePath(Repositorio.Raiz, p.Arquivo)}")
            .ToList();

        Assert.True(violacoes.Count == 0, "Pacotes proibidos: " + string.Join("; ", violacoes));
    }

    [Theory]
    [InlineData("MediatR", "12.0.0", true)]
    [InlineData("MediatR.Contracts", "2.0.1", true)]
    [InlineData("AutoMapper", "13.0.1", true)]
    [InlineData("AutoMapper.Extensions.Microsoft.DependencyInjection", "12.0.1", true)]
    [InlineData("MassTransit", "8.2.0", true)]
    [InlineData("Dapper", "2.1.35", true)]
    [InlineData("FluentAssertions", "8.0.0", true)]
    [InlineData("FluentAssertions", "7.0.0", false)]
    [InlineData("FluentValidation", "11.9.0", false)]
    [InlineData("Shouldly", "4.2.1", false)]
    public void EhProibido_ConfereARegra(string nome, string versao, bool proibido)
    {
        Assert.Equal(proibido, EhProibido(nome, versao));
    }

    [Fact]
    public void CentralPackageManagement_VersoesSoNoDirectoryPackagesProps()
    {
        // <PackageReference Include="X" Version="1.0" /> num .csproj fura o controle central de versões.
        var comVersao = Repositorio.ArquivosCsproj()
            .SelectMany(csproj => Repositorio.LerXml(csproj).Descendants("PackageReference")
                .Where(p => p.Attribute("Version") is not null || p.Attribute("VersionOverride") is not null)
                .Select(p => $"{p.Attribute("Include")?.Value} em {Path.GetFileName(csproj)}"))
            .ToList();

        Assert.True(comVersao.Count == 0, "Versão fora do Directory.Packages.props: " + string.Join("; ", comVersao));
    }

    private static bool EhProibido(string nome, string? versao)
    {
        // Dapper: toda consulta é LINQ do EF Core (docs/adr/0011).
        string[] prefixosProibidos = ["MediatR", "AutoMapper", "MassTransit", "Dapper"];
        if (prefixosProibidos.Any(p => nome.Equals(p, StringComparison.OrdinalIgnoreCase)
                                       || nome.StartsWith(p + ".", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // FluentAssertions 8+ mudou para licença comercial.
        if (nome.StartsWith("FluentAssertions", StringComparison.OrdinalIgnoreCase))
            return versao is null || !int.TryParse(versao.Split('.')[0].Trim('[', '('), out var major) || major >= 8;

        return false;
    }

    private static IEnumerable<(string Nome, string? Versao, string Arquivo)> TodosOsPacotes()
    {
        var arquivos = Repositorio.ArquivosCsproj()
            .Append(Path.Combine(Repositorio.Raiz, "Directory.Packages.props"))
            .Append(Path.Combine(Repositorio.Raiz, "Directory.Build.props"));

        foreach (var arquivo in arquivos.Where(File.Exists))
        {
            foreach (var elemento in Repositorio.LerXml(arquivo).Descendants()
                         .Where(e => e.Name.LocalName is "PackageReference" or "PackageVersion"))
            {
                var nome = elemento.Attribute("Include")?.Value ?? elemento.Attribute("Update")?.Value;
                if (nome is null)
                    continue;

                var versao = elemento.Attribute("Version")?.Value ?? elemento.Attribute("VersionOverride")?.Value;
                yield return (nome, versao, arquivo);
            }
        }
    }
}
