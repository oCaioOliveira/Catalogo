using System.Reflection;
using NetArchTest.Rules;

namespace Catalogo.ArchitectureTests;

/// <summary>
/// Regra de dependência (docs/arquitetura.md):
///   Application → Domain
///   Infrastructure → Application, Domain
///   Api → Application, e → Infrastructure só no Program.cs (registro da DI)
/// </summary>
public class DependenciasEntreCamadasTests
{
    [Fact]
    public void Domain_NaoDependeDeOutrasCamadasNemDeFrameworks()
    {
        var resultado = Types.InAssembly(Repositorio.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Catalogo.Application", "Catalogo.Infrastructure", "Catalogo.Api",
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions")
            .GetResult();

        AssertSemViolacoes(resultado);
    }

    [Fact]
    public void Domain_SoReferenciaAssembliesDoDotNet()
    {
        // Pega até dependências que não aparecem como namespace (ex.: um pacote referenciado e pouco usado).
        AssertReferenciasPermitidas(Repositorio.Domain);
    }

    [Fact]
    public void Domain_CsprojNaoTemPacotesNemProjetos()
    {
        var csproj = Repositorio.ArquivosCsproj().Single(c => c.EndsWith("Catalogo.Domain.csproj", StringComparison.Ordinal));
        var xml = Repositorio.LerXml(csproj);

        Assert.Empty(xml.Descendants("PackageReference"));
        Assert.Empty(xml.Descendants("ProjectReference"));
    }

    [Fact]
    public void Application_SoDependeDoDomain()
    {
        var resultado = Types.InAssembly(Repositorio.Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Catalogo.Infrastructure", "Catalogo.Api",
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        AssertSemViolacoes(resultado);
        AssertReferenciasPermitidas(Repositorio.Application, "Catalogo.Domain");
    }

    [Fact]
    public void Infrastructure_NaoDependeDaApi()
    {
        var resultado = Types.InAssembly(Repositorio.Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("Catalogo.Api", "Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.Http")
            .GetResult();

        AssertSemViolacoes(resultado);
    }

    [Fact]
    public void Api_SoOProgramUsaAInfrastructure()
    {
        // Controllers e demais classes da Api conversam só com a Application.
        var resultado = Types.InAssembly(Repositorio.Api)
            .That().DoNotHaveName("Program")
            .ShouldNot()
            .HaveDependencyOnAny("Catalogo.Infrastructure", "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSemViolacoes(resultado);
    }

    [Fact]
    public void Controllers_FicamNaPastaControllersETerminamComController()
    {
        var resultado = Types.InAssembly(Repositorio.Api)
            .That().Inherit(typeof(Microsoft.AspNetCore.Mvc.ControllerBase))
            .Should().ResideInNamespace("Catalogo.Api.Controllers")
            .And().HaveNameEndingWith("Controller")
            .GetResult();

        AssertSemViolacoes(resultado);
    }

    private static void AssertSemViolacoes(TestResult resultado)
    {
        var tipos = resultado.FailingTypeNames ?? [];
        Assert.True(resultado.IsSuccessful, "Tipos que violam a regra: " + string.Join(", ", tipos));
    }

    private static void AssertReferenciasPermitidas(Assembly assembly, params string[] projetosPermitidos)
    {
        var naoPermitidas = assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(nome => !nome.StartsWith("System", StringComparison.Ordinal)
                           && nome is not "netstandard" and not "mscorlib"
                           && !projetosPermitidos.Contains(nome))
            .ToList();

        Assert.True(naoPermitidas.Count == 0,
            $"{assembly.GetName().Name} referencia assemblies não permitidas: {string.Join(", ", naoPermitidas)}");
    }
}
