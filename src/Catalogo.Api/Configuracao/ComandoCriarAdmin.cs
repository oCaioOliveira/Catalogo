using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Comando de linha para criar o PRIMEIRO administrador:
/// <code>dotnet run --project src/Catalogo.Api -- criar-admin --matricula C161587 --nome "Fulano"</code>
/// Não há senha: o administrador entra pelo login do Windows (AD), identificado pela matrícula (RN23). Só funciona se não houver
/// administrador ativo (RN30). Se a matrícula já existe (ex.: a pessoa abriu o Catálogo antes e virou EXTERNO, RN105), ela passa a
/// ADMINISTRADOR.
/// <para>
/// Com <c>--gerar-sql --saida &lt;arquivo.sql&gt;</c>, NÃO acessa o banco: escreve o script que cadastra esse administrador
/// (para o pacote de entrega dos DBAs, <see cref="GerarSqlAdministradorInicial"/>).
/// </para>
/// </summary>
public static class ComandoCriarAdmin
{
    public const string Nome = "criar-admin";

    public const string OpcaoGerarSql = "--gerar-sql";

    public static bool EhEsteComando(string[] args) => args.Length > 0 && args[0] == Nome;

    /// <returns>Código de saída do processo: 0 = sucesso.</returns>
    public static async Task<int> ExecutarAsync(IServiceProvider servicos, string[] args, TextWriter saida)
    {
        var matricula = LerOpcao(args, "--matricula");
        var nome = LerOpcao(args, "--nome");

        using var escopo = servicos.CreateScope();
        if (args.Contains(OpcaoGerarSql))
            return await GerarSqlAsync(escopo.ServiceProvider, args, matricula, nome, saida);

        var casoDeUso = escopo.ServiceProvider.GetRequiredService<CriarPrimeiroAdministrador>();

        try
        {
            var criado = await casoDeUso.ExecutarAsync(matricula, nome, CancellationToken.None);
            await saida.WriteLineAsync(
                $"Administrador '{criado.Matricula}' pronto (id {criado.Id}). Ele entra pelo login do Windows, com essa matrícula.");
            return 0;
        }
        catch (ValidacaoRegistroException ex)
        {
            await saida.WriteLineAsync("Erro: " + string.Join("; ", ex.Erros.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}")));
            return 1;
        }
        catch (ExcecaoDominio ex)
        {
            await saida.WriteLineAsync("Erro: " + ex.Message);
            return 1;
        }
    }

    private static async Task<int> GerarSqlAsync(
        IServiceProvider servicos, string[] args, string? matricula, string? nome, TextWriter saida)
    {
        var arquivo = LerOpcao(args, "--saida");
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            await saida.WriteLineAsync("Erro: informe o arquivo do script com --saida <arquivo.sql>.");
            return 1;
        }

        try
        {
            var script = servicos.GetRequiredService<GerarSqlAdministradorInicial>().Executar(matricula, nome);
            arquivo = Path.GetFullPath(arquivo);
            await File.WriteAllTextAsync(arquivo, script, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (IOException ex)
        {
            // "dotnet run" roda na pasta do projeto: um caminho relativo pode não ser o esperado. Prefira caminho absoluto.
            await saida.WriteLineAsync($"Erro: não foi possível gravar {arquivo}: {ex.Message}");
            return 1;
        }
        catch (ValidacaoRegistroException ex)
        {
            await saida.WriteLineAsync("Erro: " + string.Join("; ", ex.Erros.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}")));
            return 1;
        }

        await saida.WriteLineAsync($"Script gerado em {arquivo} (o banco não foi acessado).");
        return 0;
    }

    private static string? LerOpcao(string[] args, string nome)
    {
        var indice = Array.IndexOf(args, nome);
        return indice >= 0 && indice + 1 < args.Length ? args[indice + 1] : null;
    }
}
