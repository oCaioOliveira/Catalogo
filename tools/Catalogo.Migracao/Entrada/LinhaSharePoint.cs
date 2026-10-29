namespace Catalogo.Migracao.Entrada;

/// <summary>
/// Um relatório da lista "Relatórios de Testes de Desempenho" do SharePoint: as 20 colunas da exportação, pelo nome, com os
/// valores ORIGINAIS (sem nenhuma limpeza). <see cref="Numero"/> é a posição do relatório no CSV (1 = o primeiro depois do
/// cabeçalho) e <see cref="LinhaArquivo"/> a linha física do arquivo em que ele começa.
/// </summary>
public sealed class LinhaSharePoint
{
    public static readonly IReadOnlyList<string> Colunas =
    [
        "SIGLA", "Bateria de testes", "Contexto", "Comunidade", "Tipo de Teste", "Ambiente", "Data da execução", "Executado por",
        "Host / Queue Manager", "Endpoints / Fila", "Objetivo", "Apontamentos", "Cenários de testes executados", "Conclusão",
        "Resultado", "RTC", "Script", "Configurações do Ambiente", "Infraestrutura", "Link",
    ];

    private readonly IReadOnlyDictionary<string, string> _valores;

    public LinhaSharePoint(int numero, int linhaArquivo, IReadOnlyDictionary<string, string> valores)
    {
        Numero = numero;
        LinhaArquivo = linhaArquivo;
        _valores = valores;
    }

    public int Numero { get; }
    public int LinhaArquivo { get; }

    /// <summary>O valor original de uma coluna (vazio se a coluna estiver vazia).</summary>
    public string this[string coluna] => _valores.TryGetValue(coluna, out var valor) ? valor : throw new KeyNotFoundException(coluna);

    public string Sigla => this["SIGLA"];
    public string Bateria => this["Bateria de testes"];
    public string Contexto => this["Contexto"];
    public string Comunidade => this["Comunidade"];
    public string TipoTeste => this["Tipo de Teste"];
    public string Ambiente => this["Ambiente"];
    public string DataExecucao => this["Data da execução"];
    public string ExecutadoPor => this["Executado por"];
    public string HostQueueManager => this["Host / Queue Manager"];
    public string EndpointsFila => this["Endpoints / Fila"];
    public string Objetivo => this["Objetivo"];
    public string Apontamentos => this["Apontamentos"];
    public string Cenarios => this["Cenários de testes executados"];
    public string Conclusao => this["Conclusão"];
    public string Resultado => this["Resultado"];
    public string Rtc => this["RTC"];
    public string Script => this["Script"];
    public string ConfiguracoesAmbiente => this["Configurações do Ambiente"];
    public string Infraestrutura => this["Infraestrutura"];
    public string Link => this["Link"];

    /// <summary>
    /// A linha original inteira como texto, SEM NENHUMA LIMPEZA: cada coluna como "Nome: valor", separadas por uma linha em branco
    /// (o valor pode ter quebras de linha). Vai para o campo dados_originais.
    /// </summary>
    public string TextoOriginal() =>
        string.Join("\n\n", Colunas.Select(c => $"{c}: {this[c]}"));

    /// <summary>
    /// Converte os registros do CSV em relatórios. Para (FormatException) se o cabeçalho não tiver exatamente as 20 colunas esperadas
    /// ou se algum registro tiver outro número de valores: nada é migrado pela metade.
    /// </summary>
    public static IReadOnlyList<LinhaSharePoint> DeRegistros(IReadOnlyList<RegistroCsv> registros)
    {
        if (registros.Count == 0)
            throw new FormatException("CSV vazio.");
        var cabecalho = registros[0].Valores;
        if (!cabecalho.SequenceEqual(Colunas))
            throw new FormatException("Cabeçalho do CSV diferente do esperado: " + string.Join(" | ", cabecalho));

        var linhas = new List<LinhaSharePoint>();
        for (var i = 1; i < registros.Count; i++)
        {
            var registro = registros[i];
            if (registro.Valores.Count == 1 && registro.Valores[0].Length == 0)
                continue; // linha em branco no fim do arquivo
            if (registro.Valores.Count != Colunas.Count)
                throw new FormatException($"O registro da linha {registro.LinhaInicial} tem {registro.Valores.Count} colunas (esperadas {Colunas.Count}).");
            var valores = Colunas.Select((c, j) => (c, registro.Valores[j])).ToDictionary(p => p.c, p => p.Item2);
            linhas.Add(new LinhaSharePoint(linhas.Count + 1, registro.LinhaInicial, valores));
        }

        return linhas;
    }
}
