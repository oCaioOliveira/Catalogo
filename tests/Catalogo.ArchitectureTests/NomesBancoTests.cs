using System.Text.RegularExpressions;
using Catalogo.Infrastructure.Persistencia;

namespace Catalogo.ArchitectureTests;

/// <summary>
/// O de-para de nomes do padrão da CAIXA existe em dois lugares: no V013 (que renomeia o banco) e no NomesBanco (que o EF usa).
/// Os dois precisam ser iguais, senão a aplicação procura uma coluna que o banco não tem (docs/adr/0052).
/// </summary>
public class NomesBancoTests
{
    [Fact]
    public void NomesBanco_IgualAoDeParaDoV013()
    {
        var v013 = File.ReadAllText(Path.Combine(Repositorio.Raiz, "database", "scripts", "V013__padrao_caixa.sql"));

        var tabelas = Regex.Matches(v013, @"\(N'(?<antiga>[A-Za-z_]+)',\s*N'(?<nova>CATTB\d{3}_[A-Z_]+)'\)")
            .ToDictionary(m => m.Groups["antiga"].Value, m => m.Groups["nova"].Value);
        var colunas = Regex.Matches(v013, @"\(N'(?<tabela>[A-Za-z_]+)',\s*N'(?<antiga>[A-Za-z]+)',\s*N'(?<nova>[A-Z_]+)'\)")
            .Select(m => (Tabela: m.Groups["tabela"].Value, Antiga: m.Groups["antiga"].Value, Nova: m.Groups["nova"].Value))
            .ToList();

        Assert.Equal(23, tabelas.Count);
        Assert.Empty(tabelas.Keys.Except(NomesBanco.TabelasLogicas));
        foreach (var (logica, fisica) in tabelas)
            Assert.Equal(fisica, NomesBanco.Tabela(logica));

        Assert.Equal(colunas.Count, tabelas.Keys.Sum(t => NomesBanco.Colunas(t).Count));
        foreach (var (tabela, antiga, nova) in colunas)
            Assert.Equal(nova, NomesBanco.Coluna(tabela, antiga));
    }

    /// <summary>
    /// Tabelas criadas depois do V013 (ex.: CATTB025_CHAVE_JWT, V018) já nascem com o nome físico: cada uma do NomesBanco que não
    /// está no V013 precisa ser criada por um script V014 em diante, com todas as colunas do mapeamento.
    /// </summary>
    [Fact]
    public void NomesBanco_TabelasDepoisDoV013_CriadasPorUmScriptComAsMesmasColunas()
    {
        var pasta = Path.Combine(Repositorio.Raiz, "database", "scripts");
        var v013 = File.ReadAllText(Path.Combine(pasta, "V013__padrao_caixa.sql"));
        var doV013 = Regex.Matches(v013, @"\(N'(?<antiga>[A-Za-z_]+)',\s*N'CATTB\d{3}_[A-Z_]+'\)").Select(m => m.Groups["antiga"].Value).ToHashSet();
        var posteriores = Directory.EnumerateFiles(pasta, "V*.sql")
            .Where(f => int.Parse(Regex.Match(Path.GetFileName(f), @"^V(\d+)__").Groups[1].Value) > 13)
            .Select(File.ReadAllText)
            .ToList();

        var novas = NomesBanco.TabelasLogicas.Where(t => !doV013.Contains(t)).ToList();
        Assert.Contains("ChaveJwt", novas);
        foreach (var logica in novas)
        {
            var fisica = NomesBanco.Tabela(logica);
            Assert.Matches(@"^CATTB\d{3}_[A-Z_]+$", fisica);
            var script = posteriores.SingleOrDefault(s => s.Contains($"CREATE TABLE cat.{fisica}", StringComparison.Ordinal));
            Assert.True(script is not null, $"Nenhum script V014 em diante cria cat.{fisica} ({logica}).");
            foreach (var coluna in NomesBanco.Colunas(logica).Values)
                Assert.Matches($@"\b{coluna}\b", script);
        }
    }
}
