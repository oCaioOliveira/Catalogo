using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Catalogo.Domain.ChavesUnicas;

namespace Catalogo.Migracao.Texto;

/// <summary>Limpeza de texto dos campos do SharePoint: caracteres invisíveis e espaços.</summary>
public static partial class LimpezaTexto
{
    /// <summary>
    /// Remove os caracteres invisíveis (espaço de largura zero U+200B a U+200D, U+2060, BOM U+FEFF, hífen opcional U+00AD) e troca o
    /// espaço não separável (U+00A0, U+202F) por espaço comum. O resto fica como está.
    /// </summary>
    public static string RemoverInvisiveis(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        foreach (var c in texto)
        {
            switch (c)
            {
                case '​' or '‌' or '‍' or '⁠' or '﻿' or '­':
                    break;
                case ' ' or ' ' or ' ':
                    resultado.Append(' ');
                    break;
                default:
                    resultado.Append(c);
                    break;
            }
        }

        return resultado.ToString();
    }

    /// <summary>
    /// Texto de várias linhas: sem invisíveis, quebras de linha "\n", espaços repetidos dentro de cada linha viram um (o recuo fica), sem espaço
    /// no fim das linhas, no máximo uma linha em branco seguida, e sem linhas em branco nas pontas.
    /// </summary>
    public static string NormalizarMultilinha(string texto)
    {
        var limpo = RemoverInvisiveis(texto).Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ');
        // Espaços repetidos viram um, menos o recuo no começo da linha (as listas aninhadas usam recuo de 2 espaços por nível).
        var linhas = limpo.Split('\n').Select(l =>
        {
            var recuo = l.Length - l.TrimStart(' ').Length;
            return (new string(' ', recuo) + EspacosRepetidos().Replace(l[recuo..], " ")).TrimEnd();
        }).ToList();
        var resultado = new List<string>();
        foreach (var linha in linhas)
        {
            if (linha.Trim().Length == 0)
            {
                if (resultado.Count > 0 && resultado[^1].Length > 0)
                    resultado.Add("");
                continue;
            }

            resultado.Add(linha);
        }

        while (resultado.Count > 0 && resultado[^1].Length == 0)
            resultado.RemoveAt(resultado.Count - 1);
        return string.Join("\n", resultado).Trim('\n');
    }

    /// <summary>Texto de uma linha só: sem invisíveis, quebras e tabulações viram espaço, espaços repetidos viram um, trim.</summary>
    public static string NormalizarLinha(string texto) =>
        EspacosRepetidos().Replace(RemoverInvisiveis(texto).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '), " ").Trim();

    /// <summary>
    /// Chave de comparação de nomes: uma linha, sem acentos, em maiúsculas (a mesma normalização das chaves únicas, RN09).
    /// "  Depósitos   e captação" e "DEPÓSITOS E CAPTAÇÃO" têm a mesma chave.
    /// </summary>
    public static string ChaveComparacao(string texto) => CalculadoraChaveUnica.NormalizarTexto(NormalizarLinha(texto));

    /// <summary>Sem acentos e em minúsculas, para reconhecer cabeçalhos ("Vazão Média" → "vazao media").</summary>
    public static string SemAcentosMinusculo(string texto)
    {
        var decomposto = NormalizarLinha(texto).Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                resultado.Append(c);
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex EspacosRepetidos();
}
