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
        var v013 = File.ReadAllText(Path.Combine(Repositorio.Raiz, "..", "database", "scripts", "V013__padrao_caixa.sql"));

        var tabelas = Regex.Matches(v013, @"\(N'(?<antiga>[A-Za-z_]+)',\s*N'(?<nova>CATTB\d{3}_[A-Z_]+)'\)")
            .ToDictionary(m => m.Groups["antiga"].Value, m => m.Groups["nova"].Value);
        var colunas = Regex.Matches(v013, @"\(N'(?<tabela>[A-Za-z_]+)',\s*N'(?<antiga>[A-Za-z]+)',\s*N'(?<nova>[A-Z_]+)'\)")
            .Select(m => (Tabela: m.Groups["tabela"].Value, Antiga: m.Groups["antiga"].Value, Nova: m.Groups["nova"].Value))
            .ToList();

        Assert.Equal(23, tabelas.Count);
        Assert.Equal(tabelas.Keys.Order(), NomesBanco.TabelasLogicas.Order());
        foreach (var (logica, fisica) in tabelas)
            Assert.Equal(fisica, NomesBanco.Tabela(logica));

        Assert.Equal(colunas.Count, NomesBanco.TabelasLogicas.Sum(t => NomesBanco.Colunas(t).Count));
        foreach (var (tabela, antiga, nova) in colunas)
            Assert.Equal(nova, NomesBanco.Coluna(tabela, antiga));
    }
}
