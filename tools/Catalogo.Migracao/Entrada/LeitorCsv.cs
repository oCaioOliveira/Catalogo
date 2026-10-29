using System.Text;

namespace Catalogo.Migracao.Entrada;

/// <summary>Um registro do CSV: os valores, na ordem das colunas, e a linha física do arquivo em que ele começa (1 = cabeçalho).</summary>
public sealed record RegistroCsv(int LinhaInicial, IReadOnlyList<string> Valores);

/// <summary>
/// Leitor de CSV no formato da exportação do SharePoint (RFC 4180): separador vírgula, campos entre aspas com aspas dobradas
/// dentro e QUEBRAS DE LINHA dentro das aspas. O texto de cada campo é devolvido exatamente como está no arquivo (sem trim e sem
/// trocar quebras de linha), porque a linha original vai inteira para o campo dados_originais.
/// </summary>
public static class LeitorCsv
{
    /// <summary>Lê o arquivo (UTF-8, com ou sem BOM). O primeiro registro é o cabeçalho.</summary>
    public static IReadOnlyList<RegistroCsv> LerArquivo(string caminho) => Ler(File.ReadAllText(caminho, new UTF8Encoding(false)));

    public static IReadOnlyList<RegistroCsv> Ler(string texto)
    {
        if (texto.Length > 0 && texto[0] == '﻿')
            texto = texto[1..];

        var registros = new List<RegistroCsv>();
        var valores = new List<string>();
        var campo = new StringBuilder();
        var linha = 1;
        var inicioRegistro = 1;
        var entreAspas = false;
        var campoComecou = false;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreAspas)
            {
                if (c == '"')
                {
                    if (i + 1 < texto.Length && texto[i + 1] == '"')
                    {
                        campo.Append('"');
                        i++;
                    }
                    else
                    {
                        entreAspas = false;
                    }
                }
                else
                {
                    if (c == '\n')
                        linha++;
                    campo.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"' when campo.Length == 0 && !campoComecou:
                    entreAspas = true;
                    campoComecou = true;
                    break;
                case ',':
                    valores.Add(campo.ToString());
                    campo.Clear();
                    campoComecou = false;
                    break;
                case '\r' when i + 1 < texto.Length && texto[i + 1] == '\n':
                    break;
                case '\n' or '\r':
                    valores.Add(campo.ToString());
                    campo.Clear();
                    campoComecou = false;
                    registros.Add(new RegistroCsv(inicioRegistro, valores));
                    valores = [];
                    linha++;
                    inicioRegistro = linha;
                    break;
                default:
                    campo.Append(c);
                    campoComecou = true;
                    break;
            }
        }

        if (entreAspas)
            throw new FormatException($"CSV inválido: aspas abertas no registro que começa na linha {inicioRegistro}.");
        if (campo.Length > 0 || valores.Count > 0)
        {
            valores.Add(campo.ToString());
            registros.Add(new RegistroCsv(inicioRegistro, valores));
        }

        return registros;
    }
}
