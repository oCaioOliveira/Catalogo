using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Migracao.Entrada;
using Catalogo.Migracao.Montagem;
using Catalogo.Migracao.Sql;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Saidas;

/// <summary>Resultado da conferência de integridade: para cada coluna, quantos valores não vazios do CSV e onde eles ficaram.</summary>
public sealed record IntegridadeColuna(string Coluna, string Destino, int Valores, int EmCampo, int SoEmDadosOriginais, int Perdidos);

/// <summary>Os relatórios da migração: relatorio_migracao.csv, anomalias.md, a conferência de integridade e as quantidades.</summary>
public static class RelatoriosMigracao
{
    /// <summary>Coluna do CSV → onde o valor vai (tipo de item e campo). A mesma tabela do docs/migracao-sharepoint.md.</summary>
    public static readonly IReadOnlyDictionary<string, (string Destino, string[] Campos)> Destinos = new Dictionary<string, (string, string[])>
    {
        ["SIGLA"] = ("Sigla.codigo_sigla (pelo Projeto do relatório)", ["projeto"]),
        ["Bateria de testes"] = ("Relatório.bateria_testes e .titulo", ["bateria_testes"]),
        ["Contexto"] = ("Relatório.contexto e .titulo; Projeto.nome", ["contexto"]),
        ["Comunidade"] = ("Relatório.comunidade_na_execucao; Comunidade (da Sigla)", ["comunidade_na_execucao"]),
        ["Tipo de Teste"] = ("Relatório.tipo_teste (Tecnologia)", ["tipo_teste"]),
        ["Ambiente"] = ("Relatório.ambiente → Ambiente.nome", ["ambiente"]),
        ["Data da execução"] = ("Relatório.data_execucao", ["data_execucao"]),
        ["Executado por"] = ("Relatório.executado_por", ["executado_por"]),
        ["Host / Queue Manager"] = ("Relatório.host_gerenciador_filas", ["host_gerenciador_filas"]),
        ["Endpoints / Fila"] = ("Relatório.endpoints_testados", ["endpoints_testados"]),
        ["Objetivo"] = ("Relatório.objetivo", ["objetivo"]),
        ["Apontamentos"] = ("Relatório.apontamentos (HTML → texto)", ["apontamentos"]),
        ["Cenários de testes executados"] = ("Cenário de Teste e Endpoint de Serviço; o resto em Relatório.observacoes_cenarios", ["observacoes_cenarios", "#cenarios"]),
        ["Conclusão"] = ("Relatório.conclusao", ["conclusao"]),
        ["Resultado"] = ("Relatório.status_resultado → Status do Resultado", ["status_resultado"]),
        ["RTC"] = ("RTC (rtc_externo_id e url) pelo Relatório.rtc; o que não é work item vira Link", ["rtc", "#links"]),
        ["Script"] = ("Relatório.script", ["script"]),
        ["Configurações do Ambiente"] = ("Relatório.configuracoes_ambiente", ["configuracoes_ambiente"]),
        ["Infraestrutura"] = ("Relatório.infraestrutura", ["infraestrutura"]),
        ["Link"] = ("Link (texto_exibicao e url) do Relatório", ["#links"]),
    };

    /// <summary>
    /// Para cada relatório e coluna com valor no CSV: o valor está num campo estruturado (o campo de destino preenchido no item) e,
    /// sempre, ÍNTEGRO em dados_originais (o texto "Coluna: valor" exatamente como no CSV, lido de volta do JSON Dados gravado)?
    /// Perdido = não está em dados_originais (ou o relatório não foi migrado).
    /// </summary>
    public static IReadOnlyList<IntegridadeColuna> ConferirIntegridade(ResultadoMigracao resultado)
    {
        var contagem = LinhaSharePoint.Colunas.ToDictionary(c => c, _ => (Valores: 0, EmCampo: 0, SoOriginais: 0, Perdidos: 0));
        foreach (var migrado in resultado.Relatorios)
        {
            var linha = migrado.Linha;
            var dados = migrado.Relatorio is null ? null : JsonNode.Parse(migrado.Relatorio.Registro.Dados) as JsonObject;
            var originais = dados?["dados_originais"]?.GetValue<string>();
            var referencias = migrado.Relatorio?.Registro.Referencias.Select(r => migrado.Relatorio.Entidade.ObterCampo(r.CampoId)!.Chave).ToHashSet() ?? [];
            foreach (var coluna in LinhaSharePoint.Colunas)
            {
                var valor = linha[coluna];
                if (valor.Trim().Length == 0)
                    continue;
                var c = contagem[coluna];
                c.Valores++;
                var integro = originais is not null && ContemColuna(originais, coluna, valor);
                var emCampo = migrado.Relatorio is not null && Destinos[coluna].Campos.Any(campo => campo switch
                {
                    "#cenarios" => migrado.Cenarios.Count > 0,
                    "#links" => migrado.Links.Count > 0,
                    _ => dados!.ContainsKey(campo) || referencias.Contains(campo),
                });
                if (!integro)
                    c.Perdidos++;
                else if (emCampo)
                    c.EmCampo++;
                else
                    c.SoOriginais++;
                contagem[coluna] = c;
            }
        }

        return LinhaSharePoint.Colunas
            .Select(c => new IntegridadeColuna(c, Destinos[c].Destino, contagem[c].Valores, contagem[c].EmCampo, contagem[c].SoOriginais, contagem[c].Perdidos))
            .ToList();
    }

    /// <summary>O trecho "Coluna: valor" do texto original, exatamente (o valor pode ter quebras de linha e conter "\n\n").</summary>
    public static bool ContemColuna(string originais, string coluna, string valor)
    {
        var indice = LinhaSharePoint.Colunas.ToList().IndexOf(coluna);
        var inicio = $"{coluna}: ";
        var posicao = indice == 0 ? (originais.StartsWith(inicio, StringComparison.Ordinal) ? 0 : -1) : originais.IndexOf("\n\n" + inicio, StringComparison.Ordinal);
        if (posicao < 0)
            return false;
        var comeco = posicao + (indice == 0 ? 0 : 2) + inicio.Length;
        if (indice == LinhaSharePoint.Colunas.Count - 1)
            return originais[comeco..] == valor;
        var proxima = "\n\n" + LinhaSharePoint.Colunas[indice + 1] + ": ";
        // O valor pode conter o texto da próxima coluna: confere pelo comprimento exato.
        return originais.Length >= comeco + valor.Length && originais.Substring(comeco, valor.Length) == valor
            && originais[(comeco + valor.Length)..].StartsWith(proxima, StringComparison.Ordinal);
    }

    public static void EscreverCsv(string caminho, ResultadoMigracao resultado)
    {
        var s = new StringBuilder();
        s.AppendLine("linha_csv;linha_arquivo;sigla;contexto;bateria;data_execucao;guid_relatorio;cenarios_extraidos;cenarios_migrados;links;situacao;mensagens");
        foreach (var r in resultado.Relatorios.OrderBy(r => r.Linha.Numero))
        {
            var mensagens = r.MotivoNaoMigrado is not null ? new[] { "Não migrado: " + r.MotivoNaoMigrado }.Concat(r.Mensagens) : r.Mensagens;
            s.AppendLine(string.Join(';',
                r.Linha.Numero, r.Linha.LinhaArquivo, Csv(r.Linha.Sigla), Csv(LimpezaTexto.NormalizarLinha(r.Linha.Contexto)), Csv(r.Linha.Bateria),
                Csv(r.Linha.DataExecucao), r.Relatorio?.Registro.Id.ToString("D") ?? "", r.CenariosExtraidos, r.Cenarios.Count, r.Links.Count,
                Csv(r.Situacao), Csv(string.Join(" | ", mensagens))));
        }

        File.WriteAllText(caminho, s.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string valor) => "\"" + valor.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";

    public static void EscreverAnomalias(string caminho, ResultadoMigracao resultado, string baseSharePoint)
    {
        var a = resultado.Anomalias;
        var s = new StringBuilder();
        var naoMigrados = resultado.Relatorios.Where(r => r.Relatorio is null).ToList();
        s.AppendLine("# Migração SharePoint → Catálogo: anomalias");
        s.AppendLine();
        s.AppendLine($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm} por `backend/tools/Catalogo.Migracao`. Dados de produção: fora do git.");
        s.AppendLine();
        s.AppendLine("## Precisam da sua decisão");
        s.AppendLine();
        s.AppendLine($"1. **Base dos links relativos do SharePoint**: usada `{baseSharePoint}` em {a.LinksRelativos.Count} endereço(s) (lista abaixo). Confirme se é a base certa; se não for, gere de novo com `--base-sharepoint`.");
        s.AppendLine($"2. **Comunidade das {a.SiglasComVariasComunidades.Count} siglas que mudaram de comunidade**: escolhida a do relatório mais recente (lista abaixo). O nome da época fica em `comunidade_na_execucao` de cada relatório.");
        s.AppendLine($"3. **Itens recusados pelos validadores do sistema**: {resultado.Recusados.Count} (lista abaixo){(naoMigrados.Count > 0 ? $"; {naoMigrados.Count} relatório(s) não migrado(s)" : "")}.");
        s.AppendLine($"4. **Tabelas de cenários não estruturadas**: {a.TabelasCenarios.Count} ocorrência(s) (lista abaixo). O conteúdo foi para `observacoes_cenarios` como texto limpo; nada se perdeu, mas esses cenários não entram nos indicadores do painel.");
        s.AppendLine();

        Secao(s, $"Siglas com comunidades diferentes ao longo do tempo ({a.SiglasComVariasComunidades.Count})", a.SiglasComVariasComunidades);
        Secao(s, $"Comunidades com grafias unificadas ({a.Comunidades.Count})", a.Comunidades);
        Secao(s, $"Valores de RTC fora do padrão ({a.RtcForaDoPadrao.Count})", a.RtcForaDoPadrao);
        Secao(s, $"Links relativos completados com {baseSharePoint} ({a.LinksRelativos.Count})", a.LinksRelativos);
        Secao(s, $"Tabelas de cenários não reconhecidas, total ou parcialmente ({a.TabelasCenarios.Count})", a.TabelasCenarios);
        Secao(s, $"Valores numéricos ambíguos, convertidos ou arredondados ({a.NumerosAmbiguos.Count})", a.NumerosAmbiguos);
        Secao(s, $"Itens recusados pelos validadores ({resultado.Recusados.Count})", resultado.Recusados.Select(r => $"{r.Entidade} — {r.Descricao}: {r.Motivo}").ToList());
        Secao(s, $"Projetos com contextos que só diferem por maiúsculas ou acentos ({a.Projetos.Count})", a.Projetos);
        Secao(s, $"Imagens sem endereço na exportação ({a.Imagens.Count})", a.Imagens);
        Secao(s, $"Outras ({a.Outras.Count})", a.Outras);
        File.WriteAllText(caminho, s.ToString(), new UTF8Encoding(false));
    }

    private static void Secao(StringBuilder s, string titulo, IReadOnlyList<string> itens)
    {
        s.AppendLine($"## {titulo}");
        s.AppendLine();
        if (itens.Count == 0)
            s.AppendLine("Nenhuma.");
        foreach (var item in itens)
            s.AppendLine("- " + item.Replace("\n", " "));
        s.AppendLine();
    }

    /// <summary>As quantidades para o docs/migracao-sharepoint.md e o resumo do console.</summary>
    public static string Quantidades(ResultadoMigracao resultado, IReadOnlyList<ArquivoGerado> arquivos, IReadOnlyList<IntegridadeColuna> integridade)
    {
        var itens = resultado.Apoio.Concat(resultado.Relatorios.SelectMany(r => r.Itens)).GroupBy(i => i.Entidade.NomePlural ?? i.Entidade.Nome)
            .Select(g => $"| {g.Key} | {g.Count()} |");
        var s = new StringBuilder();
        s.AppendLine("| Tipo de item | Quantidade |");
        s.AppendLine("|---|---|");
        foreach (var l in itens)
            s.AppendLine(l);
        s.AppendLine($"| Equipe | 1 ({resultado.EquipeId:D}) |");
        s.AppendLine();
        s.AppendLine("| Situação dos relatórios | Quantidade |");
        s.AppendLine("|---|---|");
        foreach (var g in resultado.Relatorios.GroupBy(r => r.Situacao).OrderBy(g => g.Key))
            s.AppendLine($"| {g.Key} | {g.Count()} |");
        s.AppendLine();
        s.AppendLine("| Arquivo | Tamanho | Relatórios | Itens |");
        s.AppendLine("|---|---|---|---|");
        foreach (var a in arquivos)
            s.AppendLine($"| {a.Nome} | {a.Bytes / 1024.0 / 1024.0:F2} MB | {a.Relatorios} | {a.Itens} |");
        s.AppendLine();
        s.AppendLine("| Coluna do CSV | Valores | Em campo | Só em dados_originais | Perdidos |");
        s.AppendLine("|---|---|---|---|---|");
        foreach (var c in integridade)
            s.AppendLine($"| {c.Coluna} | {c.Valores} | {c.EmCampo} | {c.SoEmDadosOriginais} | {c.Perdidos} |");
        s.AppendLine($"| **Total** | {integridade.Sum(c => c.Valores)} | {integridade.Sum(c => c.EmCampo)} | {integridade.Sum(c => c.SoEmDadosOriginais)} | **{integridade.Sum(c => c.Perdidos)}** |");
        return s.ToString();
    }
}
