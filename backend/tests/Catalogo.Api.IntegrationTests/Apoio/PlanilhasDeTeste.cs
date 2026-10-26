using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ClosedXML.Excel;

namespace Catalogo.Api.IntegrationTests.Apoio;

/// <summary>Monta planilhas (XLSX e CSV) para os testes da importação e chama a API dela.</summary>
public static class PlanilhasDeTeste
{
    /// <summary>XLSX com o cabeçalho na linha 1 e uma linha por item. Os valores são gravados com o tipo deles (texto, número, data).</summary>
    public static byte[] Xlsx(IReadOnlyList<string> cabecalhos, params object?[][] linhas)
    {
        using var pasta = new XLWorkbook();
        var aba = pasta.Worksheets.Add("Dados");
        for (var c = 0; c < cabecalhos.Count; c++)
            aba.Cell(1, c + 1).Value = cabecalhos[c];
        for (var l = 0; l < linhas.Length; l++)
        {
            for (var c = 0; c < linhas[l].Length; c++)
            {
                var celula = aba.Cell(l + 2, c + 1);
                switch (linhas[l][c])
                {
                    case null:
                        break;
                    case DateTime data:
                        celula.Value = data;
                        break;
                    case int inteiro:
                        celula.Value = inteiro;
                        break;
                    case double numero:
                        celula.Value = numero;
                        break;
                    case bool booleano:
                        celula.Value = booleano;
                        break;
                    default:
                        celula.Value = linhas[l][c]!.ToString();
                        break;
                }
            }
        }

        using var memoria = new MemoryStream();
        pasta.SaveAs(memoria);
        return memoria.ToArray();
    }

    /// <summary>CSV em UTF-8 (com BOM, como o Excel salva), com o separador informado.</summary>
    public static byte[] Csv(char separador, IReadOnlyList<string> cabecalhos, params string[][] linhas)
    {
        var texto = new StringBuilder();
        texto.Append(string.Join(separador, cabecalhos)).Append("\r\n");
        foreach (var linha in linhas)
            texto.Append(string.Join(separador, linha)).Append("\r\n");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(texto.ToString())];
    }

    public static string UrlEntidade(string grupo, string entidade) => $"/api/v1/grupos/{grupo}/entidades/{entidade}";

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string urlEntidade, byte[] conteudo, string nomeArquivo)
    {
        var formulario = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formulario.Add(arquivo, "arquivo", nomeArquivo);
        return cliente.PostAsync($"{urlEntidade}/importacoes", formulario);
    }

    /// <summary>Envia o arquivo, faz a prévia com a sugestão de mapeamento e devolve (id, prévia). Falha o teste se algo não for 2xx.</summary>
    public static async Task<(string Id, JsonNode Previa)> EnviarEValidarAsync(
        HttpClient cliente, string urlEntidade, byte[] conteudo, string nomeArquivo = "itens.xlsx", string duplicados = "PULAR",
        string linhasComErro = "SO_VALIDAS", Guid? equipePadrao = null)
    {
        var enviada = await OkAsync(await EnviarAsync(cliente, urlEntidade, conteudo, nomeArquivo), HttpStatusCode.Created);
        var id = enviada["id"]!.GetValue<string>();
        var mapeamento = new JsonArray(enviada["colunas"]!.AsArray()
            .Select(c => (JsonNode)new JsonObject { ["indice"] = c!["indice"]!.GetValue<int>(), ["destino"] = c["destino"]!.DeepClone() })
            .ToArray());
        var previa = await ValidarAsync(cliente, urlEntidade, id, mapeamento, duplicados, linhasComErro, equipePadrao);
        return (id, previa);
    }

    public static async Task<JsonNode> ValidarAsync(
        HttpClient cliente, string urlEntidade, string id, JsonArray mapeamento, string duplicados = "PULAR", string linhasComErro = "SO_VALIDAS",
        Guid? equipePadrao = null)
    {
        var corpo = new JsonObject
        {
            ["mapeamento"] = mapeamento,
            ["opcoes"] = new JsonObject
            {
                ["duplicados"] = duplicados,
                ["linhasComErro"] = linhasComErro,
                ["equipePadraoId"] = equipePadrao?.ToString(),
            },
        };
        return await OkAsync(await cliente.PostAsJsonAsync($"{urlEntidade}/importacoes/{id}/validar?tamanho=100", corpo));
    }

    public static Task<HttpResponseMessage> ExecutarAsync(HttpClient cliente, string urlEntidade, string id) =>
        cliente.PostAsync($"{urlEntidade}/importacoes/{id}/executar", null);

    public static Task<HttpResponseMessage> DesfazerAsync(HttpClient cliente, string urlEntidade, string id) =>
        cliente.PostAsync($"{urlEntidade}/importacoes/{id}/desfazer", null);

    public static async Task<JsonNode> OkAsync(HttpResponseMessage resposta, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == esperado, $"{resposta.RequestMessage?.Method} {resposta.RequestMessage?.RequestUri}: {(int)resposta.StatusCode} {corpo}");
        return JsonNode.Parse(corpo)!;
    }

    /// <summary>A linha da prévia pelo número dela na planilha (o cabeçalho é a 1).</summary>
    public static JsonNode Linha(JsonNode previa, int numero) =>
        previa["linhas"]!["itens"]!.AsArray().Single(l => l!["numero"]!.GetValue<int>() == numero)!;

    public static string Situacao(JsonNode previa, int numero) => Linha(previa, numero)["situacao"]!.GetValue<string>();

    public static IReadOnlyList<string> Mensagens(JsonNode previa, int numero) =>
        Linha(previa, numero)["erros"]!.AsArray().Select(e => e!["mensagem"]!.GetValue<string>()).ToList();

    public static IReadOnlyList<int?> ColunasComErro(JsonNode previa, int numero) =>
        Linha(previa, numero)["erros"]!.AsArray().Select(e => e!["coluna"]?.GetValue<int>()).ToList();
}
