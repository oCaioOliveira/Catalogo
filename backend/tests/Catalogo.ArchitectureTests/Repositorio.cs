using System.Reflection;
using System.Xml.Linq;

namespace Catalogo.ArchitectureTests;

/// <summary>Acesso às assemblies e aos arquivos do repositório usados pelos testes de arquitetura.</summary>
internal static class Repositorio
{
    public static readonly Assembly Domain = typeof(Domain.Metadados.Campo).Assembly;
    public static readonly Assembly Application = typeof(Application.CasosDeUso.CriarRegistro).Assembly;
    public static readonly Assembly Infrastructure = typeof(Infrastructure.ConfiguracaoInfraestrutura).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;

    /// <summary>Pasta raiz (onde está o Catalogo.sln), achada subindo a partir da pasta do teste.</summary>
    public static string Raiz { get; } = AcharRaiz();

    public static IEnumerable<string> ArquivosCsproj() =>
        Directory.EnumerateFiles(Raiz, "*.csproj", SearchOption.AllDirectories).Where(ForaDeBinObj);

    public static XDocument LerXml(string caminho) => XDocument.Load(caminho);

    public static bool ForaDeBinObj(string caminho)
    {
        var partes = caminho.Split(Path.DirectorySeparatorChar);
        return !partes.Contains("bin") && !partes.Contains("obj");
    }

    private static string AcharRaiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Catalogo.sln")))
            pasta = pasta.Parent;

        return pasta?.FullName ?? throw new InvalidOperationException("Catalogo.sln não encontrado acima de " + AppContext.BaseDirectory);
    }
}
