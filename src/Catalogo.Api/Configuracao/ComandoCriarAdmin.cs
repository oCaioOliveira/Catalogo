using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Comando de linha para criar o PRIMEIRO administrador:
/// <code>CATALOGO_ADMIN_SENHA='...' dotnet run --project src/Catalogo.Api -- criar-admin --matricula admin --nome "Fulano"</code>
/// A senha vem da variável de ambiente (nunca de argumento, que fica no histórico do terminal) e
/// precisa ser trocada no primeiro acesso. Só funciona se não houver administrador ativo (RN30).
/// <para>
/// Com <c>--gerar-sql --saida &lt;arquivo.sql&gt;</c>, NÃO acessa o banco: escreve o script que cadastra esse administrador
/// (para o pacote de entrega dos DBAs), com SOMENTE o hash da senha (<see cref="GerarSqlAdministradorInicial"/>).
/// <c>--aceitar-senha-fora-da-politica</c> aceita uma senha provisória fora da política, só nesse modo.
/// </para>
/// </summary>
public static class ComandoCriarAdmin
{
    public const string Nome = "criar-admin";
    public const string VariavelSenha = "CATALOGO_ADMIN_SENHA";

    public const string OpcaoGerarSql = "--gerar-sql";
    public const string OpcaoAceitarSenhaForaDaPolitica = "--aceitar-senha-fora-da-politica";

    public static bool EhEsteComando(string[] args) => args.Length > 0 && args[0] == Nome;

    /// <returns>Código de saída do processo: 0 = sucesso.</returns>
    public static async Task<int> ExecutarAsync(IServiceProvider servicos, string[] args, string? senha, TextWriter saida)
    {
        var matricula = LerOpcao(args, "--matricula");
        var nome = LerOpcao(args, "--nome");
        if (string.IsNullOrEmpty(senha))
        {
            await saida.WriteLineAsync($"Erro: defina a senha na variável de ambiente {VariavelSenha}.");
            return 1;
        }

        using var escopo = servicos.CreateScope();
        if (args.Contains(OpcaoGerarSql))
            return await GerarSqlAsync(escopo.ServiceProvider, args, matricula, nome, senha, saida);
        if (args.Contains(OpcaoAceitarSenhaForaDaPolitica))
        {
            await saida.WriteLineAsync($"Erro: {OpcaoAceitarSenhaForaDaPolitica} só vale com {OpcaoGerarSql}.");
            return 1;
        }

        var casoDeUso = escopo.ServiceProvider.GetRequiredService<CriarPrimeiroAdministrador>();

        try
        {
            var criado = await casoDeUso.ExecutarAsync(matricula, nome, senha, CancellationToken.None);
            await saida.WriteLineAsync(
                $"Administrador '{criado.Matricula}' criado (id {criado.Id}). Troque a senha no primeiro acesso.");
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
        IServiceProvider servicos, string[] args, string? matricula, string? nome, string senha, TextWriter saida)
    {
        var arquivo = LerOpcao(args, "--saida");
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            await saida.WriteLineAsync("Erro: informe o arquivo do script com --saida <arquivo.sql>.");
            return 1;
        }

        var aceitarForaDaPolitica = args.Contains(OpcaoAceitarSenhaForaDaPolitica);
        try
        {
            var script = servicos.GetRequiredService<GerarSqlAdministradorInicial>()
                .Executar(matricula, nome, senha, aceitarForaDaPolitica);
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

        if (aceitarForaDaPolitica)
            await saida.WriteLineAsync("Atenção: a senha provisória foi aceita fora da política; a troca no primeiro acesso é obrigatória.");
        await saida.WriteLineAsync($"Script gerado em {arquivo} (só o hash da senha; o banco não foi acessado).");
        return 0;
    }

    private static string? LerOpcao(string[] args, string nome)
    {
        var indice = Array.IndexOf(args, nome);
        return indice >= 0 && indice + 1 < args.Length ? args[indice + 1] : null;
    }
}
