using System.Globalization;
using System.Text.Json.Nodes;
using Catalogo.Domain.Importacoes;
using Catalogo.Migracao.Cenarios;
using Catalogo.Migracao.Conversao;
using Catalogo.Migracao.Entrada;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Montagem;

/// <summary>Opções da montagem.</summary>
public sealed record OpcoesMigracao(string MatriculaAutor, string NomeEquipe, string BaseSharePoint, TimeZoneInfo Fuso);

/// <summary>Um relatório do CSV e o que saiu dele: o item do relatório, os cenários e os links (ou o motivo de não ter sido migrado).</summary>
public sealed class RelatorioMigrado
{
    public required LinhaSharePoint Linha { get; init; }
    public ItemMigrado? Relatorio { get; set; }
    public List<ItemMigrado> Cenarios { get; } = [];
    public List<ItemMigrado> Links { get; } = [];
    public List<string> Mensagens { get; } = [];
    public string? MotivoNaoMigrado { get; set; }
    public int CenariosExtraidos { get; set; }
    public DateTime DataUtc { get; set; }

    public string Situacao => Relatorio is null ? "Não migrado" : Mensagens.Count == 0 ? "OK" : "OK com observações";
    public IEnumerable<ItemMigrado> Itens => Relatorio is null ? [] : new[] { Relatorio }.Concat(Cenarios).Concat(Links);
}

/// <summary>Tudo o que a migração produz, para os scripts e os relatórios.</summary>
public sealed class ResultadoMigracao
{
    public required Guid EquipeId { get; init; }
    public required DateTime EquipeCriadaEm { get; init; }
    public List<ItemMigrado> Apoio { get; } = [];
    public List<RelatorioMigrado> Relatorios { get; } = [];
    public List<ItemRecusado> Recusados { get; } = [];
    public Anomalias Anomalias { get; } = new();
}

/// <summary>As anomalias, por assunto (para o anomalias.md).</summary>
public sealed class Anomalias
{
    public List<string> SiglasComVariasComunidades { get; } = [];
    public List<string> Comunidades { get; } = [];
    public List<string> RtcForaDoPadrao { get; } = [];
    public List<string> LinksRelativos { get; } = [];
    public List<string> TabelasCenarios { get; } = [];
    public List<string> NumerosAmbiguos { get; } = [];
    public List<string> Projetos { get; } = [];
    public List<string> Imagens { get; } = [];
    public List<string> Outras { get; } = [];
}

/// <summary>
/// A migração dos relatórios do SharePoint (docs/migracao-sharepoint.md): monta, com a <see cref="FabricaRegistros"/>, os cadastros
/// de apoio (comunidades, siglas, ambientes, status), os itens da equipe (RTCs, projetos, endpoints) e cada relatório com os seus
/// cenários e links, aplicando as regras de conversão. Nenhum valor do CSV fica de fora: o que não vai para um campo estruturado vai
/// para as observações, e a linha inteira vai para dados_originais.
/// </summary>
public sealed class MontadorMigracao(FabricaRegistros fabrica, OpcoesMigracao opcoes)
{
    private readonly Dictionary<int, ResultadoCenarios> _cenarios = [];

    public ResultadoMigracao Montar(IReadOnlyList<LinhaSharePoint> linhas)
    {
        var datas = linhas.ToDictionary(l => l.Numero, DataUtc);
        var equipeId = FabricaRegistros.GuidDe("equipe:" + opcoes.NomeEquipe);
        var resultado = new ResultadoMigracao { EquipeId = equipeId, EquipeCriadaEm = datas.Values.Min() };

        foreach (var linha in linhas)
            _cenarios[linha.Numero] = ExtratorCenarios.Extrair(linha.Cenarios, linha.EndpointsFila);

        DateTime Primeira(IEnumerable<LinhaSharePoint> referencias) => referencias.Min(l => datas[l.Numero]);

        // ---- Comunidades (globais): trim, espaços colapsados; iguais sem diferenciar maiúsculas, acentos e espaços viram uma.
        var comunidades = new Dictionary<string, Guid>();
        foreach (var grupo in linhas.GroupBy(l => LimpezaTexto.ChaveComparacao(l.Comunidade)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var grafias = grupo.GroupBy(l => RegrasConversao.NomeComunidade(l.Comunidade)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            var nome = grafias[0].Key;
            if (grupo.Select(l => l.Comunidade).Distinct().Count() > 1)
                resultado.Anomalias.Comunidades.Add($"\"{nome}\": unificadas as grafias {string.Join(", ", grupo.Select(l => $"\"{Visivel(l.Comunidade)}\"").Distinct())}");
            var id = FabricaRegistros.GuidDe("comunidade:" + grupo.Key);
            if (Tentar(resultado, "comunidade", nome, () => fabrica.Criar("comunidade", id, new JsonObject { ["nome"] = nome }, null, Primeira(grupo), $"Comunidade {nome}")))
                comunidades[grupo.Key] = id;
        }

        // ---- Siglas (globais): código em maiúsculas, ligado à comunidade do relatório MAIS RECENTE da sigla.
        var siglas = new Dictionary<string, Guid>();
        foreach (var grupo in linhas.GroupBy(l => RegrasConversao.CodigoSigla(l.Sigla)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var maisRecente = grupo.OrderByDescending(l => datas[l.Numero]).ThenByDescending(l => l.Numero).First();
            var chaveComunidade = LimpezaTexto.ChaveComparacao(maisRecente.Comunidade);
            var distintas = grupo.Select(l => RegrasConversao.NomeComunidade(l.Comunidade)).GroupBy(LimpezaTexto.ChaveComparacao).ToList();
            if (distintas.Count > 1)
            {
                resultado.Anomalias.SiglasComVariasComunidades.Add(
                    $"{grupo.Key}: {string.Join("; ", distintas.Select(d => $"\"{d.First()}\" ({d.Count()} relatório(s), último em {grupo.Where(l => LimpezaTexto.ChaveComparacao(l.Comunidade) == d.Key).Max(l => datas[l.Numero]):dd/MM/yyyy})"))} → escolhida \"{RegrasConversao.NomeComunidade(maisRecente.Comunidade)}\"");
            }

            if (!comunidades.TryGetValue(chaveComunidade, out var comunidadeId))
            {
                resultado.Recusados.Add(new ItemRecusado("sigla", grupo.Key, "a comunidade dela não foi criada"));
                continue;
            }

            var id = FabricaRegistros.GuidDe("sigla:" + grupo.Key);
            if (Tentar(resultado, "sigla", grupo.Key, () => fabrica.Criar("sigla", id, new JsonObject { ["comunidade"] = comunidadeId.ToString("D"), ["codigo_sigla"] = grupo.Key }, null, Primeira(grupo), $"Sigla {grupo.Key}")))
                siglas[grupo.Key] = id;
        }

        // ---- Ambientes e status (globais).
        var ambientes = CriarSimples(resultado, linhas, "ambiente", "nome", l => RegrasConversao.NomeAmbiente(l.Ambiente), Primeira);
        var status = CriarSimples(resultado, linhas, "status_resultado", "descricao", l => LimpezaTexto.NormalizarLinha(l.Resultado), Primeira);
        if (linhas.Any(l => LimpezaTexto.ChaveComparacao(l.Ambiente) == "CONTIGENCIA PRD"))
            resultado.Anomalias.Outras.Add($"Ambiente \"Contigência PRD\" corrigido para \"Contingência PRD\" ({linhas.Count(l => LimpezaTexto.ChaveComparacao(l.Ambiente) == "CONTIGENCIA PRD")} relatório(s)).");

        // ---- RTCs (da equipe): pelo número do work item; vários relatórios com o mesmo RTC apontam para o mesmo item.
        var rtcs = new Dictionary<string, Guid>();
        var comRtc = linhas.Select(l => (Linha: l, Rtc: RegrasConversao.Rtc(l.Rtc))).Where(x => x.Rtc is not null).ToList();
        foreach (var x in comRtc.Where(x => !x.Rtc!.EhWorkItem))
            resultado.Anomalias.RtcForaDoPadrao.Add($"Relatório {x.Linha.Numero} ({x.Linha.Sigla} — {LimpezaTexto.NormalizarLinha(x.Linha.Contexto)}, {x.Linha.Bateria}): \"{x.Rtc!.Url}\" não é work item: vira Link do relatório, sem RTC.");
        foreach (var x in comRtc.Where(x => x.Rtc!.EhWorkItem && !x.Rtc.Url.Contains("id=", StringComparison.OrdinalIgnoreCase)))
            resultado.Anomalias.RtcForaDoPadrao.Add($"Relatório {x.Linha.Numero} ({x.Linha.Sigla}, {x.Linha.Bateria}): \"{x.Rtc!.Url}\" no formato itemName: número {x.Rtc.NumeroWorkItem} extraído de /WorkItem/.");
        foreach (var grupo in comRtc.Where(x => x.Rtc!.EhWorkItem).GroupBy(x => x.Rtc!.NumeroWorkItem!).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var primeiro = grupo.OrderBy(x => datas[x.Linha.Numero]).ThenBy(x => x.Linha.Numero).First();
            var urls = grupo.Select(x => x.Rtc!.Url).Distinct().ToList();
            if (urls.Count > 1)
                resultado.Anomalias.RtcForaDoPadrao.Add($"RTC {grupo.Key}: {urls.Count} endereços diferentes; gravado o do relatório mais antigo ({primeiro.Rtc!.Url}).");
            var id = FabricaRegistros.GuidDe("rtc:" + grupo.Key);
            if (Tentar(resultado, "rtc", grupo.Key, () => fabrica.Criar("rtc", id, new JsonObject { ["rtc_externo_id"] = grupo.Key, ["url"] = primeiro.Rtc!.Url }, equipeId, Primeira(grupo.Select(x => x.Linha)), $"RTC {grupo.Key}")))
                rtcs[grupo.Key] = id;
        }

        // ---- Projetos (da equipe): um por sigla + contexto (o nome é o Contexto; iguais sem diferenciar maiúsculas e acentos, como a unicidade).
        var projetos = new Dictionary<(string Sigla, string Contexto), Guid>();
        foreach (var grupo in linhas.GroupBy(l => (Sigla: RegrasConversao.CodigoSigla(l.Sigla), Contexto: LimpezaTexto.ChaveComparacao(l.Contexto))).OrderBy(g => g.Key.Sigla, StringComparer.Ordinal).ThenBy(g => g.Key.Contexto, StringComparer.Ordinal))
        {
            var nomes = grupo.GroupBy(l => LimpezaTexto.NormalizarLinha(l.Contexto)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            var nome = nomes[0].Key;
            if (nomes.Count > 1)
                resultado.Anomalias.Projetos.Add($"{grupo.Key.Sigla}: os contextos {string.Join(", ", nomes.Select(n => $"\"{n.Key}\""))} só diferem por maiúsculas ou acentos: um projeto só, \"{nome}\".");
            if (!siglas.TryGetValue(grupo.Key.Sigla, out var siglaId))
                continue;
            var id = FabricaRegistros.GuidDe($"projeto:{grupo.Key.Sigla}|{grupo.Key.Contexto}");
            if (Tentar(resultado, "projeto", $"{grupo.Key.Sigla} — {nome}", () => fabrica.Criar("projeto", id, new JsonObject { ["sigla"] = siglaId.ToString("D"), ["nome"] = nome }, equipeId, Primeira(grupo), $"Projeto {grupo.Key.Sigla} — {nome}")))
                projetos[grupo.Key] = id;
        }

        // ---- Endpoints (da equipe): os das tabelas de cenários, por projeto; iguais no mesmo projeto são reaproveitados.
        var endpoints = new Dictionary<(Guid Projeto, string Chave), Guid>();
        var porEndpoint = linhas
            .SelectMany(l => _cenarios[l.Numero].Cenarios.Select(c => (Linha: l, c.Endpoint)))
            .Where(x => projetos.ContainsKey(ChaveProjeto(x.Linha)))
            .GroupBy(x => (Projeto: projetos[ChaveProjeto(x.Linha)], x.Endpoint.Chave));
        foreach (var grupo in porEndpoint.OrderBy(g => g.Key.Projeto).ThenBy(g => g.Key.Chave, StringComparer.Ordinal))
        {
            var endpoint = grupo.OrderBy(x => datas[x.Linha.Numero]).First().Endpoint;
            var dados = new JsonObject
            {
                ["projeto"] = grupo.Key.Projeto.ToString("D"),
                ["nome_servico"] = endpoint.Nome,
                ["endpoint_url_fila"] = endpoint.Endpoint,
                ["ativo"] = true,
            };
            if (endpoint.Metodo is not null)
                dados["metodo"] = endpoint.Metodo;
            var id = FabricaRegistros.GuidDe($"endpoint:{grupo.Key.Projeto:D}|{grupo.Key.Chave}");
            if (Tentar(resultado, "endpoint_servico", endpoint.Endpoint, () => fabrica.Criar("endpoint_servico", id, dados, equipeId, Primeira(grupo.Select(x => x.Linha)), $"Endpoint {endpoint.Endpoint}")))
                endpoints[grupo.Key] = id;
        }

        // ---- Relatórios, com os cenários e os links.
        var grafiasInfra = RegrasConversao.GrafiasInfraestrutura(linhas.SelectMany(l => RegrasConversao.ItensJson(l.Infraestrutura)));
        foreach (var linha in linhas.OrderBy(l => datas[l.Numero]).ThenBy(l => l.Numero))
        {
            var migrado = new RelatorioMigrado { Linha = linha, DataUtc = datas[linha.Numero] };
            resultado.Relatorios.Add(migrado);
            MontarRelatorio(resultado, migrado, ambientes, status, rtcs, projetos, endpoints, grafiasInfra);
        }

        return resultado;

        (string, string) ChaveProjeto(LinhaSharePoint l) => (RegrasConversao.CodigoSigla(l.Sigla), LimpezaTexto.ChaveComparacao(l.Contexto));
    }

    private void MontarRelatorio(
        ResultadoMigracao resultado, RelatorioMigrado migrado, IReadOnlyDictionary<string, Guid> ambientes, IReadOnlyDictionary<string, Guid> status,
        IReadOnlyDictionary<string, Guid> rtcs, IReadOnlyDictionary<(string, string), Guid> projetos,
        IReadOnlyDictionary<(Guid, string), Guid> endpoints, IReadOnlyDictionary<string, string> grafiasInfra)
    {
        var linha = migrado.Linha;
        var sigla = RegrasConversao.CodigoSigla(linha.Sigla);
        var rotulo = $"{sigla} — {LimpezaTexto.NormalizarLinha(linha.Contexto)} ({linha.Bateria})";
        var cenarios = _cenarios[linha.Numero];
        migrado.CenariosExtraidos = cenarios.Cenarios.Count;

        if (!projetos.TryGetValue((sigla, LimpezaTexto.ChaveComparacao(linha.Contexto)), out var projetoId))
        {
            migrado.MotivoNaoMigrado = "o projeto (sigla + contexto) não foi criado";
            return;
        }

        var bateria = RegrasConversao.NumeroBateria(linha.Bateria);
        var tecnologias = RegrasConversao.ItensJson(linha.TipoTeste);
        var tipoTeste = ConversorCelulas.Converter(fabrica.Entidade("relatorio_teste").ObterCampo("tipo_teste")!,
            CelulaImportada.DeTexto(tecnologias.FirstOrDefault() ?? ""), opcoes.Fuso);
        if (tecnologias.Count > 1)
            migrado.Mensagens.Add($"Tipo de Teste com {tecnologias.Count} tecnologias; gravada a primeira ({tecnologias[0]}).");

        var dados = new JsonObject
        {
            ["titulo"] = RegrasConversao.Titulo(linha.Contexto, bateria ?? 0),
            ["projeto"] = projetoId.ToString("D"),
            ["ambiente"] = Id(ambientes, LimpezaTexto.ChaveComparacao(RegrasConversao.NomeAmbiente(linha.Ambiente))),
            ["status_resultado"] = Id(status, LimpezaTexto.ChaveComparacao(linha.Resultado)),
            ["data_execucao"] = DataExecucao(linha),
            ["executado_por"] = RegrasConversao.ExecutadoPor(linha.ExecutadoPor),
        };
        if (bateria is int b)
            dados["bateria_testes"] = b;
        if (tipoTeste.Valido && tipoTeste.Valor is not null)
            dados["tipo_teste"] = tipoTeste.Valor;
        else
            migrado.Mensagens.Add($"Tipo de Teste \"{linha.TipoTeste}\": {tipoTeste.Erro}");

        Texto(dados, "comunidade_na_execucao", RegrasConversao.NomeComunidade(linha.Comunidade));
        Texto(dados, "host_gerenciador_filas", ConversorHtml.ParaTexto(linha.HostQueueManager));
        Texto(dados, "endpoints_testados", ConversorHtml.ParaTexto(linha.EndpointsFila));
        Texto(dados, "contexto", ConversorHtml.ParaTexto(linha.Contexto));
        Texto(dados, "objetivo", ConversorHtml.ParaTexto(linha.Objetivo));
        Texto(dados, "apontamentos", ConversorHtml.ParaTexto(linha.Apontamentos));
        Texto(dados, "conclusao", ConversorHtml.ParaTexto(linha.Conclusao));
        Texto(dados, "configuracoes_ambiente", ConversorHtml.ParaTexto(linha.ConfiguracoesAmbiente));
        Texto(dados, "infraestrutura", RegrasConversao.Infraestrutura(linha.Infraestrutura, grafiasInfra));
        Texto(dados, "observacoes_cenarios", cenarios.TextoObservacoes);

        if (LimpezaTexto.NormalizarLinha(linha.Script).Length > 0)
        {
            var (url, completado) = RegrasConversao.LinkAbsoluto(linha.Script, opcoes.BaseSharePoint);
            dados["script"] = url;
            if (completado)
            {
                resultado.Anomalias.LinksRelativos.Add($"Relatório {linha.Numero} ({rotulo}), Script: \"{LimpezaTexto.NormalizarLinha(linha.Script)}\" → {url}");
                migrado.Mensagens.Add("Script com endereço relativo, completado com a base do SharePoint.");
            }
        }

        var rtc = RegrasConversao.Rtc(linha.Rtc);
        if (rtc is { EhWorkItem: true } && rtcs.TryGetValue(rtc.NumeroWorkItem!, out var rtcId))
            dados["rtc"] = rtcId.ToString("D");

        foreach (var imagem in new[] { linha.Apontamentos, linha.Cenarios, linha.Objetivo, linha.Conclusao }.Where(h => h.Contains("<img", StringComparison.OrdinalIgnoreCase)))
            resultado.Anomalias.Imagens.Add($"Relatório {linha.Numero} ({rotulo}): imagem sem endereço na exportação do SharePoint (fica como \"[imagem: {ConversorHtml.ImagemSemEndereco}]\").");

        var relatorioId = FabricaRegistros.GuidDe($"relatorio:{linha.Numero}|{sigla}|{linha.Bateria}|{linha.DataExecucao}|{LimpezaTexto.ChaveComparacao(linha.Contexto)}");
        var preservados = new Dictionary<string, string> { ["dados_originais"] = linha.TextoOriginal() };
        try
        {
            migrado.Relatorio = fabrica.Criar("relatorio_teste", relatorioId, dados, resultado.EquipeId, migrado.DataUtc, $"Relatório {linha.Numero}: {rotulo}", preservados);
        }
        catch (FalhaItem falha)
        {
            migrado.MotivoNaoMigrado = falha.Message;
            resultado.Recusados.Add(new ItemRecusado("relatorio_teste", $"Relatório {linha.Numero}: {rotulo}", falha.Message));
            return;
        }

        // Anomalias da coluna de cenários.
        foreach (var anomalia in cenarios.Anomalias)
        {
            resultado.Anomalias.TabelasCenarios.Add($"Relatório {linha.Numero} ({rotulo}): {anomalia}");
            migrado.Mensagens.Add(anomalia);
        }

        foreach (var numero in cenarios.NumerosAmbiguos)
            resultado.Anomalias.NumerosAmbiguos.Add($"Relatório {linha.Numero} ({rotulo}): {numero}");

        // Cenários.
        foreach (var cenario in cenarios.Cenarios)
        {
            if (!endpoints.TryGetValue((projetoId, cenario.Endpoint.Chave), out var endpointId))
            {
                migrado.Mensagens.Add($"{cenario.Origem}: o endpoint {cenario.Endpoint.Endpoint} não foi criado; cenário {cenario.Numero} não migrado.");
                continue;
            }

            var dadosCenario = new JsonObject
            {
                ["relatorio_teste"] = relatorioId.ToString("D"),
                ["endpoint_servico"] = endpointId.ToString("D"),
                ["numero_cenario"] = cenario.Numero,
                ["usuarios_virtuais"] = cenario.Usuarios,
                ["tempo_medio_resposta_ms"] = cenario.TempoMedio,
                ["vazao_media_tps"] = cenario.Vazao,
                ["taxa_erro_percentual"] = cenario.Erros,
            };
            if (cenario.Descricao is not null)
                dadosCenario["descricao_cenario"] = cenario.Descricao;
            if (cenario.Percentil95 is decimal p95)
                dadosCenario["percentil_95_ms"] = p95;
            if (cenario.Amostras is int amostras)
                dadosCenario["amostras"] = amostras;

            var cenarioId = FabricaRegistros.GuidDe($"cenario:{relatorioId:D}|{cenario.Numero}|{endpointId:D}");
            try
            {
                migrado.Cenarios.Add(fabrica.Criar("cenario_teste", cenarioId, dadosCenario, resultado.EquipeId, migrado.DataUtc, $"Cenário {cenario.Numero} de {rotulo}"));
            }
            catch (FalhaItem falha)
            {
                migrado.Mensagens.Add($"{cenario.Origem}: cenário {cenario.Numero} recusado ({falha.Message}).");
                resultado.Recusados.Add(new ItemRecusado("cenario_teste", $"Cenário {cenario.Numero} ({cenario.Origem}) de {rotulo}", falha.Message));
            }
        }

        // Links: a coluna Link e os valores da coluna RTC que não são work item.
        var links = new List<(string Valor, string Origem)>();
        if (LimpezaTexto.NormalizarLinha(linha.Link).Length > 0)
            links.Add((linha.Link, "Link"));
        if (rtc is { EhWorkItem: false })
            links.Add((linha.Rtc, "RTC"));
        foreach (var (valor, origem) in links)
        {
            var (url, completado) = RegrasConversao.LinkAbsoluto(valor, opcoes.BaseSharePoint);
            if (completado)
                resultado.Anomalias.LinksRelativos.Add($"Relatório {linha.Numero} ({rotulo}), {origem}: \"{LimpezaTexto.NormalizarLinha(valor)}\" → {url}");
            var linkId = FabricaRegistros.GuidDe($"link:{relatorioId:D}|{url}");
            if (migrado.Links.Any(l => l.Registro.Id == linkId))
                continue;
            var dadosLink = new JsonObject
            {
                ["relatorio_teste"] = relatorioId.ToString("D"),
                ["texto_exibicao"] = RegrasConversao.TextoDoLink(url),
                ["url"] = url,
            };
            try
            {
                migrado.Links.Add(fabrica.Criar("link", linkId, dadosLink, resultado.EquipeId, migrado.DataUtc, $"Link de {rotulo}"));
                if (origem == "RTC")
                    migrado.Mensagens.Add("O valor da coluna RTC não é um work item: virou Link do relatório.");
            }
            catch (FalhaItem falha)
            {
                migrado.Mensagens.Add($"Link \"{url}\" recusado ({falha.Message}); o valor fica em dados_originais.");
                resultado.Recusados.Add(new ItemRecusado("link", $"Link de {rotulo}: {url}", falha.Message));
            }
        }

        if (cenarios.TabelasNaoReconhecidas > 0 || cenarios.LinhasNaoImportadas > 0 || (cenarios.Cenarios.Count == 0 && cenarios.Observacoes.Count > 0))
            migrado.Mensagens.Add($"{cenarios.Cenarios.Count} cenário(s) estruturado(s); {cenarios.TabelasNaoReconhecidas} tabela(s) e {cenarios.LinhasNaoImportadas} linha(s) não estruturadas foram para as observações dos cenários.");
    }

    private Dictionary<string, Guid> CriarSimples(
        ResultadoMigracao resultado, IReadOnlyList<LinhaSharePoint> linhas, string entidade, string campo, Func<LinhaSharePoint, string> nome,
        Func<IEnumerable<LinhaSharePoint>, DateTime> primeira)
    {
        var ids = new Dictionary<string, Guid>();
        foreach (var grupo in linhas.GroupBy(l => LimpezaTexto.ChaveComparacao(nome(l))).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var valor = grupo.GroupBy(nome).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
            var id = FabricaRegistros.GuidDe($"{entidade}:{grupo.Key}");
            if (Tentar(resultado, entidade, valor, () => fabrica.Criar(entidade, id, new JsonObject { [campo] = valor }, null, primeira(grupo), $"{entidade} {valor}")))
                ids[grupo.Key] = id;
        }

        return ids;
    }

    private static bool Tentar(ResultadoMigracao resultado, string entidade, string descricao, Func<ItemMigrado> criar)
    {
        try
        {
            resultado.Apoio.Add(criar());
            return true;
        }
        catch (FalhaItem falha)
        {
            resultado.Recusados.Add(new ItemRecusado(entidade, descricao, falha.Message));
            return false;
        }
    }

    private static string? Id(IReadOnlyDictionary<string, Guid> ids, string chave) => ids.TryGetValue(chave, out var id) ? id.ToString("D") : null;

    private static void Texto(JsonObject dados, string campo, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
            dados[campo] = valor;
    }

    /// <summary>A data de execução como o sistema grava: "dd/MM/aaaa HH:mm" na hora local do negócio (a mesma conversão da importação).</summary>
    private string DataExecucao(LinhaSharePoint linha)
    {
        var conversao = ConversorCelulas.Converter(fabrica.Entidade("relatorio_teste").ObterCampo("data_execucao")!,
            CelulaImportada.DeTexto(LimpezaTexto.NormalizarLinha(linha.DataExecucao)), opcoes.Fuso);
        return conversao.Valido && conversao.Valor is not null
            ? conversao.Valor.GetValue<string>()
            : throw new FormatException($"Relatório {linha.Numero}: data de execução inválida \"{linha.DataExecucao}\" ({conversao.Erro}).");
    }

    private DateTime DataUtc(LinhaSharePoint linha) =>
        DateTimeOffset.Parse(DataExecucao(linha), CultureInfo.InvariantCulture).UtcDateTime;

    private static string Visivel(string texto) => texto.Replace("\t", "\\t");
}
