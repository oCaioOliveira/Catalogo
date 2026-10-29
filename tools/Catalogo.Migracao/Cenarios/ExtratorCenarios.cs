using System.Text.RegularExpressions;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Cenarios;

/// <summary>Um endpoint (ou fila) identificado numa linha da tabela de cenários.</summary>
public sealed record EndpointExtraido(string Nome, string? Metodo, string Endpoint)
{
    /// <summary>Endpoints iguais no mesmo projeto são o mesmo item: nome, método e endpoint, sem diferenciar maiúsculas e acentos.</summary>
    public string Chave => string.Join('\u001f', CalculadoraChaveUnica.NormalizarTexto(Nome),
        CalculadoraChaveUnica.NormalizarTexto(Metodo ?? ""), CalculadoraChaveUnica.NormalizarTexto(Endpoint));
}

/// <summary>Um cenário extraído de uma linha (e de um endpoint) da tabela.</summary>
public sealed record CenarioExtraido(
    int Numero, string? Descricao, EndpointExtraido Endpoint, int Usuarios, decimal TempoMedio, decimal? Percentil95, decimal Vazao,
    decimal Erros, int? Amostras, string Origem);

/// <summary>O que saiu da coluna "Cenários de testes executados" de um relatório.</summary>
public sealed class ResultadoCenarios
{
    public List<CenarioExtraido> Cenarios { get; } = [];

    /// <summary>Os trechos que não puderam ser estruturados (texto, tabelas e linhas não reconhecidas, colunas extras), já limpos.</summary>
    public List<string> Observacoes { get; } = [];

    /// <summary>Problemas para o relatório de anomalias (uma frase cada).</summary>
    public List<string> Anomalias { get; } = [];

    /// <summary>Valores numéricos ambíguos ou ajustados (arredondamento, conversão de unidade, ponto decimal decidido pela coluna).</summary>
    public List<string> NumerosAmbiguos { get; } = [];

    public int TabelasReconhecidas { get; set; }
    public int TabelasNaoReconhecidas { get; set; }
    public int LinhasNaoImportadas { get; set; }

    public string? TextoObservacoes => Observacoes.Count == 0 ? null : string.Join("\n\n", Observacoes);
}

/// <summary>
/// Extrai os cenários da coluna "Cenários de testes executados" (HTML do SharePoint):
///   - cada tabela tem o cabeçalho nas linhas antes da primeira linha de dados (pode ter 2 linhas: nome e unidade; e uma linha de
///     título, com o mesmo texto em todas as células, que é ignorada no cabeçalho);
///   - as colunas são reconhecidas pelo sentido (<see cref="ColunasCenario"/>); a tabela é reconhecida se tem usuários virtuais,
///     vazão, tempo médio e erros, e o endpoint (pela coluna de endpoint, de serviço, ou pelo único endpoint do relatório);
///   - uma tabela sem cabeçalho com o mesmo número de colunas da anterior continua a anterior;
///   - cada linha de dados vira um cenário POR ENDPOINT (uma célula pode ter vários, um por linha); células mescladas já vêm
///     propagadas (<see cref="ConversorHtml.Tabelas"/>);
///   - o que não puder ser estruturado vai para as observações, como texto limpo: texto fora das tabelas, tabelas e linhas não
///     reconhecidas, linhas sem um valor obrigatório e os valores de colunas não reconhecidas.
/// </summary>
public static partial class ExtratorCenarios
{
    private static readonly SentidoColuna[] Obrigatorias = [SentidoColuna.Usuarios, SentidoColuna.Vazao, SentidoColuna.TempoMedio, SentidoColuna.Erros];

    /// <param name="endpointsDoRelatorio">A coluna "Endpoints / Fila" do relatório: usada quando a tabela não tem o endpoint e o relatório tem UM só.</param>
    public static ResultadoCenarios Extrair(string html, string endpointsDoRelatorio)
    {
        var resultado = new ResultadoCenarios();
        var fora = ConversorHtml.TextoForaDasTabelas(html);
        var tabelas = ConversorHtml.Tabelas(html);
        if (fora.Length > 0)
            resultado.Observacoes.Add(tabelas.Count == 0 ? fora : "Texto da coluna (fora das tabelas):\n" + fora);

        var doRelatorio = EndpointsDoRelatorio(endpointsDoRelatorio);
        Cabecalho? anterior = null;
        var usados = new HashSet<(int, string)>();

        for (var k = 0; k < tabelas.Count; k++)
        {
            var nome = tabelas.Count == 1 ? "Tabela" : $"Tabela {k + 1}";
            var linhas = tabelas[k].Linhas.Where(l => l.Any(c => c.Length > 0)).ToList();
            if (linhas.Count == 0)
                continue;

            var cabecalho = LerCabecalho(linhas, tabelas[k].Colunas, anterior, nome, resultado);
            var faltando = cabecalho is null ? Obrigatorias.ToList() : Obrigatorias.Where(o => !cabecalho.Indices.ContainsKey(o)).ToList();
            var temEndpoint = cabecalho is not null && (cabecalho.Indices.ContainsKey(SentidoColuna.Endpoint) || cabecalho.Indices.ContainsKey(SentidoColuna.Servico) || doRelatorio.Count == 1);
            if (cabecalho is null || faltando.Count > 0 || !temEndpoint)
            {
                resultado.TabelasNaoReconhecidas++;
                resultado.Observacoes.Add($"{nome} (não estruturada):\n" + Renderizar(linhas));
                var motivo = cabecalho is null
                    ? "sem cabeçalho reconhecível"
                    : faltando.Count > 0
                        ? "faltam as colunas " + string.Join(", ", faltando.Select(NomeSentido))
                        : "sem coluna de endpoint ou serviço e o relatório tem mais de um endpoint";
                resultado.Anomalias.Add($"{nome} não reconhecida ({motivo}). Cabeçalho: {Renderizar(linhas.Take(Math.Max(1, cabecalho?.LinhaDados ?? 1)).ToList()).Replace("\n", " / ")}");
                continue;
            }

            resultado.TabelasReconhecidas++;
            anterior = cabecalho;
            var cenariosDaTabela = LerLinhas(linhas, cabecalho, nome, doRelatorio, resultado);

            // Tabelas seguintes que repetem os números de cenário (ex.: "antes" e "depois"): numeradas depois das anteriores.
            if (cenariosDaTabela.Any(c => usados.Contains((c.Numero, c.Endpoint.Chave))))
            {
                var deslocamento = resultado.Cenarios.Count == 0 ? 0 : resultado.Cenarios.Max(c => c.Numero);
                cenariosDaTabela = cenariosDaTabela
                    .Select(c => c with
                    {
                        Numero = c.Numero + deslocamento,
                        Descricao = Limitar($"{nome}, cenário {c.Numero}" + (c.Descricao is null ? "" : $" — {c.Descricao}"), 150),
                    })
                    .ToList();
                resultado.Anomalias.Add($"{nome} repete os números de cenário de outra tabela: os cenários dela foram numerados a partir de {deslocamento + 1} (o número original está na descrição).");
            }

            foreach (var cenario in cenariosDaTabela)
            {
                if (!usados.Add((cenario.Numero, cenario.Endpoint.Chave)))
                {
                    resultado.LinhasNaoImportadas++;
                    resultado.Observacoes.Add($"{cenario.Origem} (não importada: cenário {cenario.Numero} repetido para o mesmo endpoint): " +
                        $"{cenario.Endpoint.Endpoint} — usuários {cenario.Usuarios}, vazão {cenario.Vazao}, tempo médio {cenario.TempoMedio}, " +
                        $"percentil 95 {cenario.Percentil95?.ToString() ?? "-"}, erros {cenario.Erros}%");
                    resultado.Anomalias.Add($"{cenario.Origem}: cenário {cenario.Numero} repetido para o mesmo endpoint; a linha ficou nas observações.");
                    continue;
                }

                resultado.Cenarios.Add(cenario);
            }
        }

        return resultado;
    }

    private sealed record Cabecalho(IReadOnlyDictionary<SentidoColuna, int> Indices, IReadOnlyDictionary<int, ColunaReconhecida> Reconhecidas,
        IReadOnlyList<string> Textos, int LinhaDados, int Colunas);

    private static Cabecalho? LerCabecalho(List<IReadOnlyList<string>> linhas, int colunas, Cabecalho? anterior, string nome, ResultadoCenarios resultado)
    {
        var primeiraDeDados = linhas.FindIndex(EhLinhaDeDados);
        if (primeiraDeDados < 0)
            return null;
        if (primeiraDeDados == 0)
        {
            if (anterior is null || anterior.Colunas != colunas)
                return null;
            resultado.Anomalias.Add($"{nome} não tem cabeçalho: usado o da tabela anterior (mesmo número de colunas).");
            return anterior with { LinhaDados = 0 };
        }

        var linhasCabecalho = linhas.Take(primeiraDeDados).ToList();
        // Linha de título: o mesmo texto em todas as células (uma célula com colspan na tabela toda).
        var titulos = linhasCabecalho.Where(l => l.Count > 1 && l.All(c => c == l[0]) && l[0].Length > 0).ToList();
        if (titulos.Count < linhasCabecalho.Count)
        {
            foreach (var titulo in titulos)
                resultado.Observacoes.Add($"{nome}: título \"{titulo[0]}\"");
            linhasCabecalho = linhasCabecalho.Except(titulos).ToList();
        }

        var textos = Enumerable.Range(0, colunas)
            .Select(j => string.Join(" ", linhasCabecalho
                .Select(l => j < l.Count ? LimpezaTexto.NormalizarLinha(l[j]) : "")
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var indices = new Dictionary<SentidoColuna, int>();
        var reconhecidas = new Dictionary<int, ColunaReconhecida>();
        for (var j = 0; j < textos.Count; j++)
        {
            if (ColunasCenario.Reconhecer(textos[j]) is { } coluna && !indices.ContainsKey(coluna.Sentido))
            {
                indices[coluna.Sentido] = j;
                reconhecidas[j] = coluna;
            }
        }

        return new Cabecalho(indices, reconhecidas, textos, primeiraDeDados, colunas);
    }

    /// <summary>Linha de dados: pelo menos 2 células que são números.</summary>
    private static bool EhLinhaDeDados(IReadOnlyList<string> linha) =>
        linha.Count(c => NumeroBrasileiro.Ler(c, EstiloPonto.Milhar).Valida) >= 2;

    private static readonly SentidoColuna[] Identificadoras = [SentidoColuna.Cenario, SentidoColuna.Servico, SentidoColuna.Metodo, SentidoColuna.Endpoint];

    private static readonly SentidoColuna[] Medidas =
        [SentidoColuna.Usuarios, SentidoColuna.Vazao, SentidoColuna.TempoMedio, SentidoColuna.Percentil95, SentidoColuna.Erros, SentidoColuna.Amostras];

    private static List<CenarioExtraido> LerLinhas(
        List<IReadOnlyList<string>> linhas, Cabecalho cabecalho, string nome, IReadOnlyList<EndpointExtraido> doRelatorio, ResultadoCenarios resultado)
    {
        var cenarios = new List<CenarioExtraido>();
        var unicoEndpoint = doRelatorio.Count == 1 ? doRelatorio[0] : null;
        string Celula(IReadOnlyList<string> linha, SentidoColuna sentido) =>
            cabecalho.Indices.TryGetValue(sentido, out var j) && j < linha.Count ? linha[j] : "";
        bool Identificada(IReadOnlyList<string> linha) => Identificadoras.Any(s => Celula(linha, s).Trim().Length > 0);

        // As linhas de dados, sem o cabeçalho repetido e sem as linhas "espaçadoras" de células mescladas: sem nada nas colunas que
        // identificam o cenário e com as mesmas medidas da linha anterior (a mesclagem só as repetiu). Uma linha sem identificação com
        // medidas DIFERENTES continua o cenário da anterior (mesclagem visual, sem rowspan): herda as colunas de identificação.
        var dados = new List<(IReadOnlyList<string> Linha, int Numero)>();
        IReadOnlyList<string>? anterior = null;
        for (var i = cabecalho.LinhaDados; i < linhas.Count; i++)
        {
            var linha = linhas[i];
            var origem = $"{nome}, linha {i + 1}";
            if (linha.Select(LimpezaTexto.NormalizarLinha).SequenceEqual(cabecalho.Textos.Take(linha.Count)))
                continue;
            if (!EhLinhaDeDados(linha))
            {
                resultado.Observacoes.Add($"{origem} (não é uma linha de dados): {RenderizarLinha(linha)}");
                continue;
            }

            if (!Identificada(linha) && anterior is not null)
            {
                if (Medidas.All(m => Celula(linha, m) == Celula(anterior, m)))
                    continue;
                var herdada = linha.ToList();
                foreach (var sentido in Identificadoras.Where(s => cabecalho.Indices.ContainsKey(s)))
                {
                    var j = cabecalho.Indices[sentido];
                    if (j < herdada.Count && j < anterior.Count)
                        herdada[j] = anterior[j];
                }

                linha = herdada;
            }

            dados.Add((linha, i + 1));
            anterior = linha;
        }

        var estilos = cabecalho.Reconhecidas.Keys.ToDictionary(j => j, j => NumeroBrasileiro.EstiloDaColuna(dados.SelectMany(d => j < d.Linha.Count ? Linhas(d.Linha[j]) : [])));
        var sequencia = 0;

        for (var i = 0; i < dados.Count; i++)
        {
            var (linha, numeroLinha) = dados[i];
            var origem = $"{nome}, linha {numeroLinha}";
            sequencia++;
            var problemas = new List<string>();
            var observacoesLinha = new List<string>();

            var (numero, descricao) = NumeroCenario(Celula(linha, SentidoColuna.Cenario), sequencia, cabecalho.Indices.ContainsKey(SentidoColuna.Cenario));
            var semColunaDeEndpoint = !cabecalho.Indices.ContainsKey(SentidoColuna.Endpoint) && !cabecalho.Indices.ContainsKey(SentidoColuna.Servico);
            var (endpoints, notaEndpoint) = DividirEndpoints(Celula(linha, SentidoColuna.Servico), Celula(linha, SentidoColuna.Metodo),
                Celula(linha, SentidoColuna.Endpoint), semColunaDeEndpoint ? unicoEndpoint : null, doRelatorio);
            if (endpoints.Count == 0)
                problemas.Add("sem endpoint");
            if (notaEndpoint is not null)
                observacoesLinha.Add(notaEndpoint);

            // Os valores de uma medida: um só para a linha, ou um por endpoint (células com um valor por linha).
            IReadOnlyList<decimal?>? Medida(SentidoColuna sentido, bool obrigatorio, int escala)
            {
                if (!cabecalho.Indices.TryGetValue(sentido, out var j))
                    return null;
                var celula = j < linha.Count ? linha[j] : "";
                var valores = Linhas(celula);
                IReadOnlyList<string> porEndpoint;
                if (valores.Count <= 1)
                {
                    porEndpoint = [celula];
                }
                else if (valores.Count == endpoints.Count)
                {
                    porEndpoint = valores;
                }
                else if (PosicaoNaMesclagem(dados, i, j, valores.Count) is int posicao)
                {
                    porEndpoint = [valores[posicao]];
                    observacoesLinha.Add($"{cabecalho.Textos[j]}: célula mesclada com {valores.Count} valores ({string.Join(" / ", valores)}); usado o {posicao + 1}º, o deste cenário");
                }
                else
                {
                    if (obrigatorio)
                        problemas.Add($"{NomeSentido(sentido)}: {valores.Count} valores na célula para {endpoints.Count} endpoint(s) ({string.Join(" / ", valores)})");
                    else
                        observacoesLinha.Add($"{cabecalho.Textos[j]}: {string.Join(" / ", valores)} (vários valores na célula)");
                    resultado.NumerosAmbiguos.Add($"{origem}, {cabecalho.Textos[j]}: '{string.Join(" / ", valores)}' — vários valores sem correspondência com os endpoints");
                    return null;
                }

                var lidos = new List<decimal?>();
                foreach (var texto in porEndpoint)
                    lidos.Add(Ler(texto, j, sentido, obrigatorio, escala));
                return lidos;
            }

            decimal? Ler(string celula, int j, SentidoColuna sentido, bool obrigatorio, int escala)
            {
                var leitura = NumeroBrasileiro.Ler(celula, estilos[j]);
                if (leitura.Vazia)
                {
                    if (obrigatorio)
                        problemas.Add($"{NomeSentido(sentido)} vazio");
                    return null;
                }

                if (!leitura.Valida)
                {
                    if (obrigatorio)
                        problemas.Add($"{NomeSentido(sentido)}: {leitura.Problema}");
                    else
                        observacoesLinha.Add($"{cabecalho.Textos[j]}: {LimpezaTexto.NormalizarLinha(celula)} (não é um número inequívoco)");
                    resultado.NumerosAmbiguos.Add($"{origem}, {cabecalho.Textos[j]}: '{LimpezaTexto.NormalizarLinha(celula)}' — {leitura.Problema}");
                    return null;
                }

                var fator = cabecalho.Reconhecidas[j].Fator;
                var conversao = cabecalho.Reconhecidas[j].Conversao;
                if (leitura.Unidade is "s" or "seg" or "segs" or "segundo" or "segundos" && sentido is SentidoColuna.TempoMedio or SentidoColuna.Percentil95 && fator == 1)
                {
                    // Segundos na célula de uma coluna em ms só valem quando inequívocos: "1191 seg" (≈ 20 min, com média de 274 ms) e
                    // "14 seg ms" (duas unidades) são erros de digitação de sentido incerto. Tratados como valor não inequívoco.
                    var outraUnidade = leitura.Observacao is { } nota && UnidadeDeTempo().IsMatch(nota);
                    if (leitura.Valor >= SegundosPlausiveis || outraUnidade)
                    {
                        var motivo = outraUnidade ? "duas unidades de tempo na célula" : $"{leitura.Valor} segundos não é um tempo de resposta plausível";
                        if (obrigatorio)
                            problemas.Add($"{NomeSentido(sentido)}: {LimpezaTexto.NormalizarLinha(celula)} ({motivo})");
                        else
                            observacoesLinha.Add($"{cabecalho.Textos[j]}: {LimpezaTexto.NormalizarLinha(celula)} (não é um número inequívoco: {motivo})");
                        resultado.NumerosAmbiguos.Add($"{origem}, {cabecalho.Textos[j]}: '{LimpezaTexto.NormalizarLinha(celula)}' — {motivo}; {(obrigatorio ? "linha não importada como cenário" : "campo deixado vazio")}");
                        return null;
                    }

                    fator = 1000;
                    conversao = "segundos → ms (×1000), pela unidade da célula";
                }
                else if (leitura.Unidade is "ms" && fator == 1000)
                {
                    fator = 1;
                    conversao = null;
                }
                else if (leitura.Unidade is { } u && (u.EndsWith("min", StringComparison.Ordinal) || u == "tpm") && sentido == SentidoColuna.Vazao && fator == 1)
                {
                    fator = 1m / 60m;
                    conversao = "por minuto → por segundo (÷60), pela unidade da célula";
                }

                var valor = leitura.Valor!.Value * fator;
                if (leitura.Observacao is not null)
                {
                    observacoesLinha.Add($"{cabecalho.Textos[j]}: {LimpezaTexto.NormalizarLinha(celula)} ({leitura.Observacao})");
                    if (!leitura.Observacao.StartsWith("nota", StringComparison.Ordinal))
                        resultado.NumerosAmbiguos.Add($"{origem}, {cabecalho.Textos[j]}: {leitura.Observacao}");
                }

                if (conversao is not null)
                {
                    observacoesLinha.Add($"{cabecalho.Textos[j]}: {LimpezaTexto.NormalizarLinha(celula)} convertido ({conversao})");
                    resultado.NumerosAmbiguos.Add($"{origem}, {cabecalho.Textos[j]}: '{LimpezaTexto.NormalizarLinha(celula)}' convertido ({conversao})");
                }

                var arredondado = Math.Round(valor, escala, MidpointRounding.AwayFromZero);
                if (arredondado != valor)
                {
                    observacoesLinha.Add($"{cabecalho.Textos[j]}: {LimpezaTexto.NormalizarLinha(celula)} arredondado para {arredondado} ({escala} casas)");
                    resultado.NumerosAmbiguos.Add($"{origem}, {cabecalho.Textos[j]}: '{LimpezaTexto.NormalizarLinha(celula)}' arredondado para {arredondado}");
                }

                if (sentido is SentidoColuna.Usuarios or SentidoColuna.Amostras && (arredondado < 0 || arredondado > int.MaxValue))
                {
                    problemas.Add($"{NomeSentido(sentido)} fora do intervalo ({arredondado})");
                    return null;
                }

                return arredondado;
            }

            var usuarios = Medida(SentidoColuna.Usuarios, true, 0);
            var vazao = Medida(SentidoColuna.Vazao, true, 2);
            var tempo = Medida(SentidoColuna.TempoMedio, true, 2);
            var p95 = Medida(SentidoColuna.Percentil95, false, 2);
            var erros = Medida(SentidoColuna.Erros, true, 2);
            var amostras = Medida(SentidoColuna.Amostras, false, 0);

            // Colunas não reconhecidas: os valores vão para as observações.
            for (var j = 0; j < linha.Count; j++)
            {
                if (cabecalho.Reconhecidas.ContainsKey(j) || linha[j].Length == 0)
                    continue;
                var titulo = j < cabecalho.Textos.Count && cabecalho.Textos[j].Length > 0 ? cabecalho.Textos[j] : $"coluna {j + 1}";
                observacoesLinha.Add($"{titulo}: {LimpezaTexto.NormalizarLinha(linha[j].Replace("\n", " / "))}");
            }

            if (problemas.Count > 0)
            {
                resultado.LinhasNaoImportadas++;
                resultado.Observacoes.Add($"{origem} (não importada: {string.Join("; ", problemas)}):\n{RenderizarCabecalhoELinha(cabecalho, linha)}");
                resultado.Anomalias.Add($"{origem}: linha não importada como cenário ({string.Join("; ", problemas)}).");
                continue;
            }

            if (observacoesLinha.Count > 0)
                resultado.Observacoes.Add($"Cenário {numero} ({origem}): {string.Join("; ", observacoesLinha.Distinct())}");

            static T? Valor<T>(IReadOnlyList<T?>? valores, int k) where T : struct =>
                valores is null || valores.Count == 0 ? null : valores.Count == 1 ? valores[0] : valores[k];

            for (var k = 0; k < endpoints.Count; k++)
            {
                cenarios.Add(new CenarioExtraido(numero, descricao, endpoints[k], (int)Valor(usuarios, k)!.Value, Valor(tempo, k)!.Value, Valor(p95, k),
                    Valor(vazao, k)!.Value, Valor(erros, k)!.Value, Valor(amostras, k) is decimal a ? (int)a : null, origem));
            }
        }

        return cenarios;
    }

    /// <summary>
    /// Célula mesclada (rowspan) com um valor por cenário: o mesmo texto, com <paramref name="quantidade"/> valores, se repete em
    /// exatamente <paramref name="quantidade"/> linhas de dados seguidas. Devolve a posição desta linha no bloco, ou nulo.
    /// </summary>
    private static int? PosicaoNaMesclagem(List<(IReadOnlyList<string> Linha, int Numero)> dados, int indice, int coluna, int quantidade)
    {
        string Texto(int i) => coluna < dados[i].Linha.Count ? dados[i].Linha[coluna] : "";
        var inicio = indice;
        while (inicio > 0 && Texto(inicio - 1) == Texto(indice))
            inicio--;
        var fim = indice;
        while (fim < dados.Count - 1 && Texto(fim + 1) == Texto(indice))
            fim++;
        return fim - inicio + 1 == quantidade ? indice - inicio : null;
    }

    /// <summary>
    /// O número do cenário: "1", "1.", "1º", "Cenário 1" → 1, sem descrição. Texto com um número ("2 - Pico") → o número e o
    /// texto como descrição. Texto sem número ("A *") ou sem coluna de cenário → a posição da linha na tabela (e o texto, se houver,
    /// como descrição).
    /// </summary>
    public static (int Numero, string? Descricao) NumeroCenario(string celula, int sequencia, bool temColuna)
    {
        var texto = LimpezaTexto.NormalizarLinha(celula.Replace('\n', ' '));
        if (!temColuna || texto.Length == 0)
            return (sequencia, null);

        var somenteNumero = SomenteNumeroCenario().Match(texto);
        if (somenteNumero.Success && int.TryParse(somenteNumero.Groups[1].Value, out var n))
            return (n, null);

        var algum = PrimeiroNumero().Match(texto);
        return algum.Success && int.TryParse(algum.Value, out var m) && m > 0
            ? (m, Limitar(texto, 150))
            : (sequencia, Limitar(texto, 150));
    }

    /// <summary>
    /// Os endpoints de uma linha. A célula de endpoint pode ter vários, um por linha, e linhas de método ("GET", "POST - Autorizar
    /// Lote"): cada linha com um caminho ("/...", "http...") ou um nome de fila é um endpoint; os métodos e nomes são associados na
    /// ordem quando a quantidade bate, ou valem para todos quando há um só. Sem nome do serviço, o nome é o próprio endpoint.
    /// Linhas de método SEM caminho ("POST - Envia Notificações") são procuradas na coluna "Endpoints / Fila" do relatório (pelo
    /// método e pelo nome); sem uma correspondência única, o endpoint fica com o texto da célula, como está. A nota diz o que foi feito.
    /// </summary>
    public static (IReadOnlyList<EndpointExtraido> Endpoints, string? Nota) DividirEndpoints(
        string servico, string metodo, string endpoint, EndpointExtraido? padrao, IReadOnlyList<EndpointExtraido> doRelatorio)
    {
        var metodos = Linhas(metodo).Select(m => m.ToUpperInvariant()).ToList();
        var nomes = Linhas(servico);
        var caminhos = new List<string>();
        var semCaminho = new List<(string Linha, string Metodo, string Nome)>();
        // Numa célula com caminhos, as linhas sem caminho não são endpoints: "ETAPA 1:" é o rótulo do caminho seguinte,
        // "&classe=01" é o resto do caminho anterior (quebrado em linha) e "Sem controle de vazão" é uma nota.
        var linhasEndpoint = Linhas(endpoint);
        var celulaComCaminhos = linhasEndpoint.Any(l => CaminhoNaLinha().IsMatch(l));
        var rotulosPendentes = new List<string>();
        var rotulos = new Dictionary<int, string>();
        var notasCelula = new List<string>();

        void AdicionarCaminho(string caminho)
        {
            if (rotulosPendentes.Count > 0)
            {
                rotulos[caminhos.Count] = string.Join(" – ", rotulosPendentes);
                rotulosPendentes.Clear();
            }

            caminhos.Add(caminho);
        }

        foreach (var linha in linhasEndpoint)
        {
            var casamento = LinhaDeMetodo().Match(linha);
            if (!casamento.Success)
            {
                if (celulaComCaminhos && !CaminhoNaLinha().IsMatch(linha))
                {
                    if (linha.EndsWith(':'))
                    {
                        rotulosPendentes.Add(linha.TrimEnd(':').Trim());
                        continue;
                    }

                    if (caminhos.Count > 0 && ContinuacaoDeCaminho().IsMatch(linha))
                    {
                        caminhos[^1] += linha;
                        notasCelula.Add($"\"{linha}\" é a continuação do caminho anterior");
                        continue;
                    }

                    if (linha.Contains(' ', StringComparison.Ordinal))
                    {
                        notasCelula.Add($"\"{linha}\" é uma nota, não um endpoint");
                        continue;
                    }
                }

                AdicionarCaminho(linha);
                continue;
            }

            var metodoLinha = casamento.Groups["metodo"].Value.ToUpperInvariant();
            var resto = casamento.Groups["nome"].Value.Trim();
            var caminho = CaminhoNaLinha().Match(resto);
            if (caminho.Success)
            {
                // "GET /api/x" ou "POST Nome: /api/x": método e caminho na mesma linha.
                metodos.Add(metodoLinha);
                var nomeAntes = resto[..caminho.Index].Trim().TrimEnd(':', '-', '–').Trim();
                if (nomeAntes.Length > 0)
                    nomes.Add(nomeAntes);
                AdicionarCaminho(caminho.Value);
            }
            else if (resto.Length > 0)
            {
                semCaminho.Add((linha, metodoLinha, resto));
            }
            else
            {
                metodos.Add(metodoLinha);
            }
        }

        notasCelula.AddRange(rotulosPendentes.Select(r => $"\"{r}:\" é um rótulo sem caminho depois dele"));
        string? nota = notasCelula.Count == 0 ? null : "endpoint: " + string.Join("; ", notasCelula);
        if (caminhos.Count == 0 && semCaminho.Count > 0)
        {
            var achados = new List<EndpointExtraido>();
            var notas = new List<string>();
            foreach (var (linha, metodoLinha, nomeLinha) in semCaminho)
            {
                var candidato = Correspondente(metodoLinha, nomeLinha, doRelatorio);
                if (candidato is not null)
                {
                    achados.Add(new EndpointExtraido(nomeLinha, metodoLinha, candidato.Endpoint));
                    notas.Add($"\"{linha}\" → {candidato.Endpoint} (da coluna \"Endpoints / Fila\")");
                }
                else
                {
                    achados.Add(new EndpointExtraido(nomeLinha, metodoLinha, linha));
                    notas.Add($"\"{linha}\": sem caminho na tabela nem correspondência única em \"Endpoints / Fila\"; o endpoint ficou com o texto da célula");
                }
            }

            return (achados, "endpoint: " + string.Join("; ", notas));
        }

        if (caminhos.Count == 0)
        {
            if (padrao is null)
            {
                // Só o nome do serviço (sem endpoint na tabela): procura na lista do relatório. As linhas do nome são um nome só.
                var nomeServico = string.Join(" ", nomes);
                if (nomeServico.Length > 0 && Correspondente(metodos.Count == 1 ? metodos[0] : null, nomeServico, doRelatorio) is { } achado)
                    return ([new EndpointExtraido(nomeServico, metodos.Count == 1 ? metodos[0] : achado.Metodo, achado.Endpoint)], $"endpoint: \"{nomeServico}\" → {achado.Endpoint} (da coluna \"Endpoints / Fila\")");
                return ([], null);
            }

            nota = $"endpoint: o único da coluna \"Endpoints / Fila\" do relatório ({padrao.Endpoint})";
            return ([padrao with { Nome = nomes.Count == 0 ? padrao.Nome : string.Join(" / ", nomes), Metodo = metodos.Count == 1 ? metodos[0] : padrao.Metodo }], nota);
        }

        string? Associar(List<string> valores, int i) =>
            valores.Count == caminhos.Count ? valores[i] : valores.Count == 0 ? null : string.Join(" / ", valores.Distinct());

        string Nome(int i, string caminho) => (Associar(nomes, i), rotulos.GetValueOrDefault(i)) switch
        {
            ({ } nome, { } rotulo) => $"{rotulo} – {nome}",
            ({ } nome, null) => nome,
            (null, { } rotulo) => rotulo,
            _ => caminho,
        };

        return (caminhos.Select((caminho, i) => new EndpointExtraido(Nome(i, caminho), Associar(metodos, i), caminho)).ToList(), nota);
    }

    /// <summary>
    /// Os endpoints listados na coluna "Endpoints / Fila" do relatório. Cada linha pode ser "/caminho", "GET /caminho",
    /// "POST Nome do serviço:/caminho" ou um nome de fila.
    /// </summary>
    public static IReadOnlyList<EndpointExtraido> EndpointsDoRelatorio(string coluna)
    {
        var lista = new List<EndpointExtraido>();
        foreach (var linha in Linhas(ConversorHtml.ParaTexto(coluna)))
        {
            var casamento = LinhaDeMetodo().Match(linha);
            var metodoLinha = casamento.Success ? casamento.Groups["metodo"].Value.ToUpperInvariant() : null;
            var resto = casamento.Success ? casamento.Groups["nome"].Value.Trim() : linha;
            var caminho = CaminhoNaLinha().Match(resto);
            if (caminho.Success)
            {
                var nomeAntes = resto[..caminho.Index].Trim().TrimEnd(':', '-', '–').Trim();
                lista.Add(new EndpointExtraido(nomeAntes.Length > 0 ? nomeAntes : caminho.Value, metodoLinha, caminho.Value));
            }
            else if (resto.Length > 0 && metodoLinha is null)
            {
                lista.Add(new EndpointExtraido(resto, null, resto));
            }
        }

        return lista.DistinctBy(e => e.Chave).ToList();
    }

    /// <summary>
    /// O endpoint da lista do relatório com o mesmo método (se informado) e o nome mais parecido: palavras comparadas pelos 4 primeiros
    /// caracteres, sem acentos ("Cosulta Notifcação" ≈ "Consulta Notificação"). Vale o que cobre mais palavras do nome (pelo menos metade)
    /// e, no empate, o que tem menos palavras sobrando; se ainda empatar, nenhum (a correspondência precisa ser única).
    /// </summary>
    private static readonly char[] SeparadoresPalavras = [' ', '-', '_', '/', ':', '.', '(', ')'];

    private static EndpointExtraido? Correspondente(string? metodo, string nome, IReadOnlyList<EndpointExtraido> doRelatorio)
    {
        static HashSet<string> Palavras(string texto) =>
            LimpezaTexto.SemAcentosMinusculo(texto).Split(SeparadoresPalavras, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length >= 3).Select(p => p.Length > 4 ? p[..4] : p).ToHashSet();

        var alvo = Palavras(nome);
        if (alvo.Count == 0)
            return null;
        // Cobertura: quanto do nome da tabela aparece no endpoint (nome ou caminho); desempate pela precisão (quanto do endpoint é o nome).
        var pontuados = doRelatorio
            .Where(e => metodo is null || e.Metodo is null || e.Metodo == metodo)
            .Select(e =>
            {
                var palavras = Palavras(e.Nome + " " + e.Endpoint);
                var comuns = palavras.Intersect(alvo).Count();
                return (Endpoint: e, Cobertura: comuns / (double)alvo.Count, Precisao: palavras.Count == 0 ? 0 : comuns / (double)palavras.Count);
            })
            .Where(p => p.Cobertura >= 0.5)
            .OrderByDescending(p => p.Cobertura).ThenByDescending(p => p.Precisao)
            .ToList();
        return pontuados.Count == 1 || (pontuados.Count > 1 && (pontuados[0].Cobertura, pontuados[0].Precisao) != (pontuados[1].Cobertura, pontuados[1].Precisao))
            ? pontuados[0].Endpoint
            : null;
    }

    private static List<string> Linhas(string celula) =>
        LimpezaTexto.NormalizarMultilinha(celula).Split('\n').Select(l => l.Trim().TrimStart('-', '•', '*', ' ').Trim()).Where(l => l.Length > 0).ToList();

    private static string NomeSentido(SentidoColuna sentido) => sentido switch
    {
        SentidoColuna.Usuarios => "usuários virtuais",
        SentidoColuna.Vazao => "vazão",
        SentidoColuna.TempoMedio => "tempo médio",
        SentidoColuna.Percentil95 => "percentil 95",
        SentidoColuna.Erros => "erros (%)",
        SentidoColuna.Amostras => "amostras",
        SentidoColuna.Cenario => "cenário",
        SentidoColuna.Servico => "nome do serviço",
        SentidoColuna.Metodo => "método",
        SentidoColuna.Endpoint => "endpoint",
        _ => sentido.ToString(),
    };

    private static string Limitar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..(tamanho - 1)] + "…";

    private static string RenderizarLinha(IReadOnlyList<string> linha) =>
        string.Join(" | ", linha.Select(c => LimpezaTexto.NormalizarLinha(c.Replace("\n", " / "))));

    private static string Renderizar(IReadOnlyList<IReadOnlyList<string>> linhas)
    {
        // Linhas iguais seguidas (rowspan propagado em todas as colunas) aparecem uma vez só.
        var saida = new List<string>();
        foreach (var linha in linhas)
        {
            var texto = RenderizarLinha(linha);
            if (saida.Count == 0 || saida[^1] != texto)
                saida.Add(texto);
        }

        return string.Join("\n", saida);
    }

    private static string RenderizarCabecalhoELinha(Cabecalho cabecalho, IReadOnlyList<string> linha) =>
        string.Join("; ", linha.Select((c, j) => $"{(j < cabecalho.Textos.Count && cabecalho.Textos[j].Length > 0 ? cabecalho.Textos[j] : $"coluna {j + 1}")}: {LimpezaTexto.NormalizarLinha(c.Replace("\n", " / "))}"));

    [GeneratedRegex(@"^(?:cen[aá]rio\s*)?(\d+)\s*[.ºª°)]?$", RegexOptions.IgnoreCase)]
    private static partial Regex SomenteNumeroCenario();

    [GeneratedRegex(@"\d+")]
    private static partial Regex PrimeiroNumero();

    /// <summary>Um caminho ("/api/x") ou endereço ("https://...") dentro de uma linha.</summary>
    [GeneratedRegex(@"(https?://|/)\S+")]
    private static partial Regex CaminhoNaLinha();

    [GeneratedRegex(@"^(?<metodo>GET|POST|PUT|PATCH|DELETE|DEL|HEAD|OPTIONS)\b\s*(?:[-–:]\s*)?(?<nome>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaDeMetodo();

    /// <summary>Acima disto, um tempo de resposta "em segundos" numa coluna em ms é tratado como erro de digitação (10 minutos).</summary>
    private const decimal SegundosPlausiveis = 600m;

    /// <summary>O resto de um caminho quebrado em linha: sem espaço, com parâmetros ("&amp;classe=01", "restricao?documento={d}") ou "${...}".</summary>
    [GeneratedRegex(@"^(?:\$\{\S*|[^\s/]*[?&=][^\s]*)$")]
    private static partial Regex ContinuacaoDeCaminho();

    /// <summary>Uma segunda unidade de tempo depois do número ("14 seg ms").</summary>
    [GeneratedRegex(@"nota ""(ms|s|seg|segs|segundos?|milissegundos?|min)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnidadeDeTempo();
}
