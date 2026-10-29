using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Catalogo.Migracao.Texto;

/// <summary>Uma tabela HTML como grade: uma lista de linhas, cada uma com o texto das células (já com rowspan/colspan propagados).</summary>
public sealed record Grade(IReadOnlyList<IReadOnlyList<string>> Linhas)
{
    public int Colunas => Linhas.Count == 0 ? 0 : Linhas.Max(l => l.Count);
}

/// <summary>
/// Converte o HTML do SharePoint (Apontamentos, Cenários...) em texto limpo COM ESTRUTURA:
///   - parágrafos, divisões e quebras viram linhas;
///   - listas viram itens com "- " (não ordenada) ou "1. " (ordenada), com recuo de 2 espaços por nível;
///   - tabelas viram linhas com as células separadas por " | ";
///   - entidades HTML são decodificadas (&amp;#58;, &amp;quot;, &amp;#160;...) e os estilos, scripts e comentários saem;
///   - &lt;img&gt; vira "[imagem: &lt;src&gt;]" ("[imagem: sem endereço no SharePoint]" quando não tem src);
///   - links ficam como "texto (url)" (só a url, se o texto for igual a ela ou vazio).
/// O resultado não tem caracteres invisíveis nem espaços repetidos (<see cref="LimpezaTexto.NormalizarMultilinha"/>).
/// </summary>
public static partial class ConversorHtml
{
    public const string ImagemSemEndereco = "sem endereço no SharePoint";

    private static readonly HtmlParser Parser = new();

    private static readonly HashSet<string> Ignorados = ["SCRIPT", "STYLE", "HEAD", "TITLE", "META", "LINK", "NOSCRIPT", "TEMPLATE"];

    private static readonly HashSet<string> Blocos =
    [
        "P", "DIV", "H1", "H2", "H3", "H4", "H5", "H6", "BLOCKQUOTE", "SECTION", "ARTICLE", "HEADER", "FOOTER", "PRE", "ADDRESS",
        "FIGURE", "FIGCAPTION", "DL", "DT", "DD", "HR", "CENTER", "FORM", "FIELDSET", "NAV", "MAIN", "ASIDE", "CAPTION",
    ];

    /// <summary>O texto parece HTML (tem alguma marcação conhecida)? Texto puro passa só pela limpeza.</summary>
    public static bool PareceHtml(string texto) => MarcacaoHtml().IsMatch(texto);

    /// <summary>HTML → texto limpo com estrutura (ver a classe). Texto que não é HTML só passa pela limpeza.</summary>
    public static string ParaTexto(string html) => ParaTexto(html, incluirTabelas: true);

    /// <summary>O texto do HTML SEM as tabelas (o que vem antes, entre e depois delas).</summary>
    public static string TextoForaDasTabelas(string html) => ParaTexto(html, incluirTabelas: false);

    private static string ParaTexto(string html, bool incluirTabelas)
    {
        if (!PareceHtml(html))
            return LimpezaTexto.NormalizarMultilinha(System.Net.WebUtility.HtmlDecode(html));

        var documento = Parser.ParseDocument("<!DOCTYPE html><html><body>" + html + "</body></html>");
        var escritor = new Escritor(incluirTabelas);
        escritor.Filhos(documento.Body!, 0);
        return LimpezaTexto.NormalizarMultilinha(escritor.Texto);
    }

    /// <summary>
    /// As tabelas do HTML (só as de primeiro nível), cada uma como grade com rowspan e colspan PROPAGADOS: uma célula mesclada
    /// aparece em todas as linhas e colunas que ela abrange. O texto de cada célula é o texto limpo dela, com as quebras de linha
    /// (um endpoint por linha, por exemplo) preservadas.
    /// </summary>
    public static IReadOnlyList<Grade> Tabelas(string html)
    {
        if (!PareceHtml(html))
            return [];

        var documento = Parser.ParseDocument("<!DOCTYPE html><html><body>" + html + "</body></html>");
        return documento.QuerySelectorAll("table").OfType<IHtmlTableElement>()
            .Where(t => t.ParentElement?.Closest("table") is null)
            .Select(ParaGrade)
            .ToList();
    }

    private static Grade ParaGrade(IHtmlTableElement tabela)
    {
        var linhas = new List<List<string>>();
        // Células "pendentes" de rowspan: (linha, coluna) → texto.
        var pendentes = new Dictionary<(int Linha, int Coluna), string>();

        var indice = 0;
        foreach (var tr in tabela.Rows)
        {
            var linha = new List<string>();
            var coluna = 0;
            foreach (var celula in tr.Cells)
            {
                while (pendentes.Remove((indice, coluna), out var mesclado))
                {
                    linha.Add(mesclado);
                    coluna++;
                }

                var texto = TextoCelula(celula);
                var colspan = Math.Clamp(celula.ColumnSpan, 1, 100);
                var rowspan = Math.Clamp(celula.RowSpan, 1, 1000);
                for (var c = 0; c < colspan; c++)
                {
                    linha.Add(texto);
                    for (var r = 1; r < rowspan; r++)
                        pendentes[(indice + r, coluna)] = texto;
                    coluna++;
                }
            }

            while (pendentes.Remove((indice, coluna), out var restante))
            {
                linha.Add(restante);
                coluna++;
            }

            linhas.Add(linha);
            indice++;
        }

        // Rowspan que passa da última linha: as linhas abrangidas não existem, nada a propagar.
        return new Grade(linhas);
    }

    /// <summary>O texto de uma célula: o mesmo texto limpo, com as linhas internas preservadas.</summary>
    public static string TextoCelula(IElement celula)
    {
        var escritor = new Escritor(incluirTabelas: true);
        escritor.Filhos(celula, 0);
        return LimpezaTexto.NormalizarMultilinha(escritor.Texto);
    }

    /// <summary>Escreve o texto de uma árvore de nós, com as regras de bloco, lista, tabela, imagem e link.</summary>
    private sealed class Escritor(bool incluirTabelas)
    {
        private readonly StringBuilder _texto = new();

        public string Texto => _texto.ToString();

        // Logo depois do marcador de um item ("- ", "1. ") também é começo de linha: o <div> ou <p> dentro do <li> não quebra a linha.
        private int _fimDoMarcador = -1;

        private bool NoComecoDaLinha => _texto.Length == 0 || _texto[^1] == '\n' || _texto.Length == _fimDoMarcador;

        public void Filhos(INode no, int nivelLista)
        {
            foreach (var filho in no.ChildNodes)
                Escrever(filho, nivelLista);
        }

        private void Escrever(INode no, int nivelLista)
        {
            switch (no)
            {
                case IText texto:
                    EscreverTexto(texto.Data);
                    return;
                case IElement elemento:
                    EscreverElemento(elemento, nivelLista);
                    return;
            }
        }

        private void EscreverTexto(string texto)
        {
            // Espaços do HTML: qualquer sequência vira um espaço; no começo da linha, nenhum.
            var colapsado = EspacosHtml().Replace(texto, " ");
            if (NoComecoDaLinha)
                colapsado = colapsado.TrimStart();
            if (colapsado.Length > 0 && colapsado[0] == ' ' && _texto.Length > 0 && _texto[^1] == ' ')
                colapsado = colapsado[1..];
            _texto.Append(colapsado);
        }

        private void QuebrarLinha()
        {
            while (_texto.Length > 0 && _texto[^1] == ' ')
                _texto.Length--;
            _texto.Append('\n');
        }

        private void Bloco()
        {
            if (!NoComecoDaLinha)
                QuebrarLinha();
        }

        private void EscreverElemento(IElement elemento, int nivelLista)
        {
            var tag = elemento.TagName;
            if (Ignorados.Contains(tag))
                return;

            switch (tag)
            {
                case "BR":
                    QuebrarLinha();
                    return;
                case "IMG":
                    var src = elemento.GetAttribute("src");
                    var alt = elemento.GetAttribute("alt");
                    EscreverTexto(" ");
                    _texto.Append("[imagem: ").Append(string.IsNullOrWhiteSpace(src) ? ImagemSemEndereco : src.Trim());
                    if (!string.IsNullOrWhiteSpace(alt))
                        _texto.Append(" — ").Append(LimpezaTexto.NormalizarLinha(alt));
                    _texto.Append(']');
                    return;
                case "A":
                    EscreverLink(elemento, nivelLista);
                    return;
                case "UL" or "OL":
                    EscreverLista(elemento, nivelLista, ordenada: tag == "OL");
                    return;
                case "TABLE":
                    if (incluirTabelas)
                        EscreverTabela((IHtmlTableElement)elemento);
                    else
                        Bloco();
                    return;
                case "LI":
                    // LI fora de lista: um item simples.
                    Bloco();
                    Marcador(nivelLista, "- ");
                    Filhos(elemento, nivelLista + 1);
                    FimDoItem();
                    return;
                case "TR":
                    Bloco();
                    _texto.Append(string.Join(" | ", elemento.Children.Select(c => LimpezaTexto.NormalizarLinha(TextoCelula(c).Replace("\n", " / ")))));
                    Bloco();
                    return;
            }

            if (Blocos.Contains(tag))
            {
                Bloco();
                Filhos(elemento, nivelLista);
                Bloco();
                return;
            }

            Filhos(elemento, nivelLista);
        }

        private void EscreverLink(IElement link, int nivelLista)
        {
            var interno = new Escritor(incluirTabelas);
            interno.Filhos(link, nivelLista);
            var texto = LimpezaTexto.NormalizarLinha(interno.Texto);
            var href = link.GetAttribute("href")?.Trim();

            if (string.IsNullOrEmpty(href) || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                EscreverTexto(texto);
                return;
            }

            EscreverTexto(texto.Length == 0 || string.Equals(texto, href, StringComparison.OrdinalIgnoreCase)
                ? href
                : $"{texto} ({href})");
        }

        private void EscreverLista(IElement lista, int nivelLista, bool ordenada)
        {
            if (_texto.Length == _fimDoMarcador)
                QuebrarLinha(); // lista logo no começo de um item: o marcador do item fica na linha dele
            Bloco();
            var numero = int.TryParse(lista.GetAttribute("start"), out var inicio) ? inicio : 1;
            foreach (var item in lista.Children)
            {
                if (item.TagName != "LI")
                {
                    Escrever(item, nivelLista + 1);
                    continue;
                }

                Bloco();
                Marcador(nivelLista, ordenada ? $"{numero++}. " : "- ");
                Filhos(item, nivelLista + 1);
                FimDoItem();
            }

            Bloco();
        }

        private void Marcador(int nivelLista, string marcador)
        {
            _texto.Append(new string(' ', 2 * nivelLista)).Append(marcador);
            _fimDoMarcador = _texto.Length;
        }

        private void FimDoItem()
        {
            if (_texto.Length == _fimDoMarcador)
                QuebrarLinha(); // item vazio: o marcador fica sozinho na linha, como no original
            else
                Bloco();
        }

        private void EscreverTabela(IHtmlTableElement tabela)
        {
            Bloco();
            foreach (var linha in tabela.Rows)
            {
                var celulas = linha.Cells.Select(c => LimpezaTexto.NormalizarLinha(TextoCelula(c).Replace("\n", " / "))).ToList();
                if (celulas.All(c => c.Length == 0))
                    continue;
                _texto.Append(string.Join(" | ", celulas));
                QuebrarLinha();
            }

            Bloco();
        }
    }

    [GeneratedRegex(@"<\s*/?\s*(p|div|span|br|table|tr|td|th|ul|ol|li|a|img|b|i|u|strong|em|font|h[1-6]|tbody|thead)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MarcacaoHtml();

    [GeneratedRegex(@"[ \t\r\n\f]+")]
    private static partial Regex EspacosHtml();
}
