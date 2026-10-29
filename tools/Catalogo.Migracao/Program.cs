using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Infrastructure;
using Catalogo.Migracao.Entrada;
using Catalogo.Migracao.Montagem;
using Catalogo.Migracao.Saidas;
using Catalogo.Migracao.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Gerador dos scripts da migração dos relatórios de testes de desempenho do SharePoint (docs/migracao-sharepoint.md).
//
//   dotnet run --project backend/tools/Catalogo.Migracao -- gerar --csv <arquivo.csv> --scripts <pasta> --relatorios <pasta>
//       [--base-sharepoint https://caixa.sharepoint.com] [--matricula C161587] [--equipe GEQTI]
//     Lê a configuração da área testes_desempenho do banco LOCAL (ConnectionStrings:Catalogo, o user-secrets da API) e escreve os
//     scripts 02_migracao_parte_NN.sql e 03_conferencia.sql, o relatorio_migracao.csv, o anomalias.md e o quantidades.md.
//
//   dotnet run --project backend/tools/Catalogo.Migracao -- conferir-banco --csv <arquivo.csv> --banco "<connection string>"
//     Lê de volta, de um banco onde a migração foi aplicada, o JSON Dados de cada relatório e confere que a linha original do CSV
//     está íntegra em dados_originais (0 perdas) e que as quantidades por tipo de item batem.
Console.OutputEncoding = Encoding.UTF8;
// O mesmo user-secrets da API (Catalogo.Api.csproj): a connection string do banco local de desenvolvimento.
const string UserSecretsDaApi = "catalogo-api-0b6c1e2a-5d3f-4c8e-9a71-2f4e8d9c6b10";
var argumentos = Argumentos.Ler(args);
var raiz = Repositorio.Raiz();

switch (argumentos.Comando)
{
    case "gerar":
        return await Gerar(argumentos, raiz);
    case "conferir-banco":
        return await ConferirBanco(argumentos);
    case "mostrar":
        return Mostrar(argumentos);
    default:
        Console.Error.WriteLine("Uso: gerar --csv <arquivo> --scripts <pasta> --relatorios <pasta> [--base-sharepoint <url>] [--matricula <m>] [--equipe <nome>]");
        Console.Error.WriteLine("     conferir-banco --csv <arquivo> --banco <connection string>");
        Console.Error.WriteLine("     mostrar --csv <arquivo> --linha <n>");
        return 2;
}

static async Task<int> Gerar(Argumentos argumentos, string raiz)
{
    var csv = argumentos.Obrigatorio("csv");
    var pastaScripts = argumentos.Obrigatorio("scripts");
    var pastaRelatorios = argumentos.Obrigatorio("relatorios");
    var opcoes = new OpcoesMigracao(
        argumentos.Opcional("matricula") ?? "C161587",
        argumentos.Opcional("equipe") ?? "GEQTI",
        argumentos.Opcional("base-sharepoint") ?? Catalogo.Migracao.Conversao.RegrasConversao.BaseSharePointPadrao,
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));

    var linhas = LinhaSharePoint.DeRegistros(LeitorCsv.LerArquivo(csv));
    Console.WriteLine($"CSV: {linhas.Count} relatórios ({csv}).");

    // Configuração da área no banco local, pelo mesmo provedor de metadados da API.
    var configuracao = new ConfigurationBuilder().AddUserSecrets(UserSecretsDaApi).AddEnvironmentVariables().Build();
    var servicos = new ServiceCollection().AddSingleton<IConfiguration>(configuracao).AddLogging();
    servicos.AdicionarInfraestrutura();
    await using var provedor = servicos.BuildServiceProvider();
    using var escopo = provedor.CreateScope();
    var grupo = await escopo.ServiceProvider.GetRequiredService<IMetadadosProvider>().ObterGrupoAsync("testes_desempenho", CancellationToken.None)
        ?? throw new InvalidOperationException("A área testes_desempenho não existe no banco local.");

    var leitura = await File.ReadAllTextAsync(Path.Combine(raiz, "database", "ferramentas", "ler_area_cat.sql"));
    var trecho = await File.ReadAllTextAsync(Path.Combine(raiz, "database", "ferramentas", "impressao_digital_area.sql"));
    var area = await ImpressaoDaArea(ConfiguracaoInfraestrutura.ObterConnectionString(configuracao), leitura, trecho);
    Console.WriteLine($"Área no banco local: {area.Entidades} tipos de item, {area.Campos} campos, impressão {area.Impressao}.");

    var fabrica = new FabricaRegistros(grupo, FabricaRegistros.GuidDe("autor:" + opcoes.MatriculaAutor), opcoes.MatriculaAutor);
    var resultado = new MontadorMigracao(fabrica, opcoes).Montar(linhas);

    var arquivos = new EscritorScripts(resultado, grupo, opcoes, area, leitura, trecho).Gerar(pastaScripts);
    Directory.CreateDirectory(pastaRelatorios);
    RelatoriosMigracao.EscreverCsv(Path.Combine(pastaRelatorios, "relatorio_migracao.csv"), resultado);
    RelatoriosMigracao.EscreverAnomalias(Path.Combine(pastaRelatorios, "anomalias.md"), resultado, opcoes.BaseSharePoint);
    var integridade = RelatoriosMigracao.ConferirIntegridade(resultado);
    var quantidades = RelatoriosMigracao.Quantidades(resultado, arquivos, integridade);
    await File.WriteAllTextAsync(Path.Combine(pastaRelatorios, "quantidades.md"), quantidades);

    Console.WriteLine(quantidades);
    var perdas = integridade.Sum(c => c.Perdidos);
    Console.WriteLine(perdas == 0 ? "Conferência de integridade: 0 perdas." : $"Conferência de integridade: {perdas} PERDA(S)!");
    return perdas == 0 ? 0 : 1;
}

// Mostra o que sai da coluna de cenários de UM relatório (para conferir com o SharePoint): --linha é o número do relatório no CSV.
static int Mostrar(Argumentos argumentos)
{
    var linhas = LinhaSharePoint.DeRegistros(LeitorCsv.LerArquivo(argumentos.Obrigatorio("csv")));
    var linha = linhas[int.Parse(argumentos.Obrigatorio("linha")) - 1];
    var resultado = Catalogo.Migracao.Cenarios.ExtratorCenarios.Extrair(linha.Cenarios, linha.EndpointsFila);
    Console.WriteLine($"Relatório {linha.Numero}: {linha.Sigla} — {linha.Contexto} ({linha.Bateria}), {linha.DataExecucao}");
    Console.WriteLine($"Tabelas reconhecidas: {resultado.TabelasReconhecidas}; não reconhecidas: {resultado.TabelasNaoReconhecidas}; linhas não importadas: {resultado.LinhasNaoImportadas}");
    foreach (var c in resultado.Cenarios)
        Console.WriteLine($"  cenário {c.Numero}{(c.Descricao is null ? "" : $" \"{c.Descricao}\"")} | {c.Endpoint.Metodo} {c.Endpoint.Endpoint} ({c.Endpoint.Nome}) | VU {c.Usuarios} | vazão {c.Vazao} | médio {c.TempoMedio} | p95 {c.Percentil95?.ToString() ?? "-"} | erros {c.Erros}% | amostras {c.Amostras?.ToString() ?? "-"}");
    Console.WriteLine("Observações:");
    Console.WriteLine(resultado.TextoObservacoes ?? "(nenhuma)");
    return 0;
}

static async Task<AreaEsperada> ImpressaoDaArea(string connectionString, string leitura, string trecho)
{
    await using var conexao = new SqlConnection(connectionString);
    await conexao.OpenAsync();
    await using var comando = conexao.CreateCommand();
    comando.CommandText = "SET NOCOUNT ON; DECLARE @GrupoChave varchar(100) = 'testes_desempenho';\n" + leitura + "\n" + trecho + """

        SELECT (SELECT COUNT(*) FROM @Canonico WHERE Secao = 2), (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3),
               (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4), (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5),
               (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6), CONVERT(varchar(66), @Impressao, 1);
        """;
    await using var leitor = await comando.ExecuteReaderAsync();
    await leitor.ReadAsync();
    return new AreaEsperada(leitor.GetInt32(0), leitor.GetInt32(1), leitor.GetInt32(2), leitor.GetInt32(3), leitor.GetInt32(4), leitor.GetString(5));
}

static async Task<int> ConferirBanco(Argumentos argumentos)
{
    var linhas = LinhaSharePoint.DeRegistros(LeitorCsv.LerArquivo(argumentos.Obrigatorio("csv")));
    await using var conexao = new SqlConnection(argumentos.Obrigatorio("banco"));
    await conexao.OpenAsync();
    await using var comando = conexao.CreateCommand();
    comando.CommandText = """
        SELECT r.NU_NSU_REGISTRO, r.DE_REGISTRO_DADO
        FROM cat.CATTB011_REGISTRO r
        JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = r.NU_NSU_CE_ENTIDADE_REG
        WHERE e.CO_CHAVE_ENT = 'relatorio_teste'
        """;
    var noBanco = new Dictionary<string, int>(StringComparer.Ordinal);
    await using (var leitor = await comando.ExecuteReaderAsync())
    {
        while (await leitor.ReadAsync())
        {
            var dados = JsonNode.Parse(leitor.GetString(1)) as JsonObject;
            var original = dados?["dados_originais"]?.GetValue<string>();
            if (original is not null)
                noBanco[original] = noBanco.GetValueOrDefault(original) + 1;
        }
    }

    var valores = 0;
    var perdidos = 0;
    var semRelatorio = 0;
    foreach (var linha in linhas)
    {
        var texto = linha.TextoOriginal();
        var naoVazios = LinhaSharePoint.Colunas.Count(c => linha[c].Trim().Length > 0);
        valores += naoVazios;
        if (noBanco.TryGetValue(texto, out var n) && n > 0)
        {
            noBanco[texto] = n - 1;
            foreach (var coluna in LinhaSharePoint.Colunas.Where(c => linha[c].Trim().Length > 0))
            {
                if (!RelatoriosMigracao.ContemColuna(texto, coluna, linha[coluna]))
                    perdidos++;
            }
        }
        else
        {
            semRelatorio++;
            perdidos += naoVazios;
        }
    }

    Console.WriteLine($"Banco: {linhas.Count - semRelatorio} de {linhas.Count} relatórios do CSV encontrados pelo texto original íntegro (dados_originais).");
    Console.WriteLine($"Valores não vazios do CSV: {valores}; perdidos: {perdidos}.");
    Console.WriteLine(perdidos == 0 ? "Conferência no banco: 0 perdas." : "Conferência no banco: HÁ PERDAS.");
    return perdidos == 0 ? 0 : 1;
}

/// <summary>Argumentos "comando --chave valor".</summary>
internal sealed class Argumentos
{
    private readonly Dictionary<string, string> _valores = new(StringComparer.OrdinalIgnoreCase);

    public string Comando { get; private init; } = "";

    public static Argumentos Ler(string[] args)
    {
        var argumentos = new Argumentos { Comando = args.Length > 0 ? args[0] : "" };
        for (var i = 1; i < args.Length - 1; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Argumento inesperado: {args[i]}");
            argumentos._valores[args[i][2..]] = args[i + 1];
        }

        return argumentos;
    }

    public string Obrigatorio(string nome) => _valores.TryGetValue(nome, out var valor) ? valor : throw new ArgumentException($"Informe --{nome}.");

    public string? Opcional(string nome) => _valores.GetValueOrDefault(nome);
}

/// <summary>A raiz do repositório (onde está a pasta database/), achada subindo a partir da pasta atual.</summary>
internal static class Repositorio
{
    public static string Raiz()
    {
        var pasta = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "database", "ferramentas")))
            pasta = pasta.Parent;
        return pasta?.FullName ?? throw new InvalidOperationException("Rode de dentro do repositório (não achei database/ferramentas).");
    }
}
