using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Mapeamentos;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Registros;
using Catalogo.Domain.Validacao;

namespace Catalogo.Application.Comum;

/// <summary>
/// Passa cada linha da planilha pelas MESMAS regras do cadastro manual (RN97 a RN102), sem atalho nenhum: monta o corpo que a tela
/// enviaria (células convertidas, RN100; referências resolvidas pelo nome, RN99; equipe pelo nome) e o entrega ao
/// <see cref="GravadorRegistro"/>, o mesmo do POST e do PUT. Só o que é LIDO do banco vem em lote (<see cref="LotePreparacao"/>):
/// os itens referenciados (uma consulta por tipo de item) e as chaves únicas que já existem (RN102).
///
/// Na prévia, tudo o que o gravador marcou para gravar é descartado no fim (<see cref="IUnidadeDeTrabalho.DescartarAlteracoes"/>);
/// na execução, fica marcado, e o caso de uso grava tudo num único SaveChanges (uma transação).
/// </summary>
public sealed class SimuladorImportacao(
    IConsultasImportacao consultas,
    IRegistroRepository registros,
    IEquipeRepository equipes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    GravadorRegistro gravador,
    ValidadorRegistro validador,
    CalculadoraChaveUnica calculadora,
    OpcoesExportacao opcoesExportacao)
{
    /// <param name="manterParaGravar">
    /// Falso (prévia): descarta tudo no fim. Verdadeiro (execução): os itens ficam marcados para o SaveChanges de quem chamou.
    /// </param>
    public async Task<ResumoImportacao> SimularAsync(
        Grupo grupo, Entidade entidade, UsuarioAtual usuario, PlanilhaImportada planilha, MapeamentoImportacao mapeamento,
        OpcoesImportacao opcoes, OrigemGravacao origem, bool manterParaGravar, CancellationToken cancellationToken)
    {
        var colunas = new ColunasDoMapeamento(entidade, mapeamento);
        var linhas = planilha.Linhas.Select(l => new LinhaEmPreparo(l)).ToList();

        // 1. Equipe (pelo nome) e campos simples (RN100). As referências vêm depois, em lote.
        var equipesPorNome = await EquipesPorNomeAsync(usuario, entidade, cancellationToken);
        foreach (var linha in linhas)
        {
            DefinirEquipe(linha, colunas, entidade, usuario, opcoes, equipesPorNome);
            ConverterCampos(linha, colunas);
        }

        // 2. Referências pelo nome ou pelo ID (RN99), uma consulta por tipo de item referenciado.
        var carregados = await ResolverReferenciasAsync(grupo, entidade, usuario, colunas, linhas, cancellationToken);

        // 3. Chaves únicas de cada linha, para os duplicados no arquivo e no banco (RN102), também em lote.
        foreach (var linha in linhas.Where(l => l.EquipeConhecida))
        {
            var validado = validador.Validar(entidade, linha.Dados);
            if (validado.Valido && linha.Erros.Count == 0)
                linha.Chaves = calculadora.Calcular(entidade, validado.Registro!, entidade.EhGlobal ? null : linha.Equipe);
            else if (linha.Erros.Count == 0)
                linha.Chaves = ChavesDosCamposDaRestricao(entidade, linha);
            else
                AcrescentarErrosDosOutrosCampos(linha, colunas, validado.Erros);
        }

        var donos = await consultas.ObterDonosDasChavesAsync(
            entidade.Id, linhas.SelectMany(l => l.Chaves).Select(c => (c.Restricao.Id, c.ValorHash)).ToList(), cancellationToken);
        var lote = new LotePreparacao(carregados, donos);
        Classificar(linhas, colunas, entidade, opcoes, lote);

        // 4. Cada linha pelo MESMO caminho do cadastro manual.
        var paraAlterar = (await consultas.ObterParaAlteracaoAsync(
                linhas.Where(l => l.Situacao == SituacaoLinha.Atualizacao).Select(l => l.RegistroId!.Value).ToList(), cancellationToken))
            .ToDictionary(r => r.Id);
        lote = await AmpliarComReferenciasAtuaisAsync(lote, carregados, donos, paraAlterar.Values, cancellationToken);
        foreach (var linha in linhas.Where(l => l.Situacao is SituacaoLinha.Nova or SituacaoLinha.Atualizacao))
            await GravarAsync(linha, colunas, grupo, entidade, usuario, origem, lote, paraAlterar, cancellationToken);

        if (!manterParaGravar)
            unidadeDeTrabalho.DescartarAlteracoes();

        return ResumoImportacao.De(linhas.Select(l => l.Resultado()).ToList(), opcoes);
    }

    /// <summary>
    /// Na atualização, as referências sem coluna na planilha ficam como estão (RN102): os itens que elas apontam também precisam
    /// estar no lote, para o <see cref="PreparadorRegistro"/> conferi-los (numa consulta só, para todos os itens a atualizar).
    /// </summary>
    private async Task<LotePreparacao> AmpliarComReferenciasAtuaisAsync(
        LotePreparacao lote, IReadOnlyDictionary<Guid, Registro> carregados, IReadOnlyDictionary<(Guid RestricaoId, string Hash), Guid> donos,
        IEnumerable<Registro> paraAlterar, CancellationToken cancellationToken)
    {
        var faltando = paraAlterar.SelectMany(r => r.Referencias).Select(r => r.RegistroDestinoId)
            .Where(id => !carregados.ContainsKey(id)).Distinct().ToList();
        if (faltando.Count == 0)
            return lote;

        var todos = carregados.ToDictionary(p => p.Key, p => p.Value);
        foreach (var registro in await registros.ObterPorIdsAsync(faltando, cancellationToken))
            todos[registro.Id] = registro;
        return new LotePreparacao(todos, donos);
    }

    /// <summary>
    /// As equipes que a coluna Equipe aceita, pelo nome sem diferenciar maiúsculas e acentos: as do usuário nesta área de gestão
    /// (RN91; para o ADMINISTRADOR, as habilitadas). Qual delas ele pode usar continua sendo decidido pelo gravador.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, Guid>> EquipesPorNomeAsync(
        UsuarioAtual usuario, Entidade entidade, CancellationToken cancellationToken)
    {
        if (entidade.EhGlobal || usuario.Equipes.Count == 0)
            return new Dictionary<string, Guid>();

        var lista = await equipes.ObterPorIdsAsync(usuario.Equipes.ToList(), cancellationToken);
        return lista
            .GroupBy(e => CalculadoraChaveUnica.NormalizarTexto(e.Nome))
            .ToDictionary(g => g.Key, g => g.First().Id);
    }

    private static void DefinirEquipe(
        LinhaEmPreparo linha, ColunasDoMapeamento colunas, Entidade entidade, UsuarioAtual usuario, OpcoesImportacao opcoes,
        IReadOnlyDictionary<string, Guid> equipesPorNome)
    {
        if (entidade.EhGlobal)
        {
            linha.EquipeConhecida = true;
            return;
        }

        Guid? equipe = null;
        var celula = colunas.Equipe is int indice ? linha.Original.Celula(indice) : CelulaImportada.Vazia;
        if (!celula.EhVazia)
        {
            var nome = celula.ParaTexto();
            if (equipesPorNome.TryGetValue(CalculadoraChaveUnica.NormalizarTexto(nome), out var encontrada))
                equipe = encontrada;
            else
                linha.Erros.Add(new ErroLinha(colunas.Equipe, ChavesReservadas.EquipeId,
                    $"Equipe '{nome}' não encontrada entre as suas equipes nesta área de gestão."));
        }
        else
        {
            equipe = opcoes.EquipePadraoId;
        }

        if (equipe is Guid escolhida)
            linha.Dados[ChavesReservadas.EquipeId] = escolhida.ToString("D");

        // Quem tem uma equipe só nesta área de gestão não precisa informar (RN27): o gravador usa a dele.
        linha.Equipe = equipe ?? (!usuario.EhAdministrador && usuario.Equipes.Count == 1 ? usuario.Equipes.Single() : null);
        linha.EquipeConhecida = linha.Equipe is not null;
    }

    private void ConverterCampos(LinhaEmPreparo linha, ColunasDoMapeamento colunas)
    {
        foreach (var (campo, indice) in colunas.Simples)
        {
            var conversao = ConversorCelulas.Converter(campo, linha.Original.Celula(indice), opcoesExportacao.Fuso);
            if (!conversao.Valido)
                linha.Erros.Add(new ErroLinha(indice, campo.Chave, conversao.Erro!));
            else if (conversao.Valor is not null)
                linha.Dados[campo.Chave] = conversao.Valor;
        }
    }

    /// <summary>
    /// RN99. Para cada campo de referência mapeado, UMA consulta com todos os nomes do arquivo (no escopo de visibilidade do usuário,
    /// o mesmo da listagem) e UMA carga dos itens encontrados. Os campos com dependência (RN08) são resolvidos depois dos campos de que
    /// dependem, para escolher, entre nomes repetidos, o item que atende à dependência.
    /// </summary>
    /// <returns>Os itens carregados (que também alimentam as regras do <see cref="PreparadorRegistro"/>, sem consultar de novo).</returns>
    private async Task<IReadOnlyDictionary<Guid, Registro>> ResolverReferenciasAsync(
        Grupo grupo, Entidade entidade, UsuarioAtual usuario, ColunasDoMapeamento colunas, List<LinhaEmPreparo> linhas,
        CancellationToken cancellationToken)
    {
        var candidatosPorCampo = new Dictionary<Guid, ILookup<string, CandidatoReferencia>>();
        var idsInformados = new HashSet<Guid>();

        foreach (var campo in colunas.Referencias)
        {
            var entidadeReferenciada = grupo.ObterEntidade(campo.EntidadeReferenciadaId!.Value);
            var exibicao = entidadeReferenciada?.CampoExibicaoId is Guid exibicaoId ? entidadeReferenciada.ObterCampo(exibicaoId) : null;
            if (colunas.Nome.TryGetValue(campo.Chave, out var colunaNome) && entidadeReferenciada is not null
                && ResolvedorReferencias.AceitaNome(exibicao))
            {
                var nomes = linhas.Select(l => l.Original.Celula(colunaNome)).Where(c => !c.EhVazia).Select(c => c.ParaTexto()).Distinct().ToList();
                var escopo = PreparadorConsultaRegistros.Escopo(usuario, entidadeReferenciada, null);
                var itens = await consultas.BuscarPorNomesAsync(entidadeReferenciada.Id, exibicao!, nomes, escopo, cancellationToken);
                candidatosPorCampo[campo.Id] = itens
                    .Select(i => new CandidatoReferencia(i.Id, i.Nome, i.EquipeId, entidadeReferenciada.EhGlobal, i.Situacao, i.CriadoPorId))
                    .ToLookup(c => ResolvedorReferencias.ChaveDoNome(exibicao!, c.Nome) ?? "");
            }

            if (colunas.Identificador.TryGetValue(campo.Chave, out var colunaId))
            {
                foreach (var celula in linhas.Select(l => l.Original.Celula(colunaId)).Where(c => !c.EhVazia))
                {
                    if (Guid.TryParse(celula.ParaTexto(), out var id))
                        idsInformados.Add(id);
                }
            }
        }

        var todosIds = candidatosPorCampo.Values.SelectMany(l => l.SelectMany(g => g)).Select(c => c.Id).Concat(idsInformados).Distinct().ToList();
        var carregados = todosIds.Count == 0
            ? new Dictionary<Guid, Registro>()
            : (await registros.ObterPorIdsAsync(todosIds, cancellationToken)).ToDictionary(r => r.Id);
        var rotulos = idsInformados.Count == 0
            ? new Dictionary<Guid, string?>()
            : await registros.ObterRotulosAsync(idsInformados.ToList(), cancellationToken);

        foreach (var linha in linhas)
        {
            // Sem saber a equipe da linha, não dá para saber o que ela pode referenciar (RN29): o erro de equipe já está na linha.
            if (!linha.EquipeConhecida)
                continue;

            foreach (var campo in colunas.Referencias)
                ResolverNaLinha(linha, campo, grupo, entidade, usuario, colunas, candidatosPorCampo, carregados, rotulos);
        }

        return carregados;
    }

    private static void ResolverNaLinha(
        LinhaEmPreparo linha, Campo campo, Grupo grupo, Entidade entidade, UsuarioAtual usuario, ColunasDoMapeamento colunas,
        IReadOnlyDictionary<Guid, ILookup<string, CandidatoReferencia>> candidatosPorCampo, IReadOnlyDictionary<Guid, Registro> carregados,
        IReadOnlyDictionary<Guid, string?> rotulos)
    {
        var entidadeReferenciada = grupo.ObterEntidade(campo.EntidadeReferenciadaId!.Value);
        var nomeEntidade = entidadeReferenciada?.Nome ?? campo.Nome;

        // A coluna "<campo> (ID)", se preenchida, vale mais que o nome (é ela que desfaz nomes repetidos).
        if (colunas.Identificador.TryGetValue(campo.Chave, out var colunaId) && linha.Original.Celula(colunaId) is { EhVazia: false } celulaId)
        {
            if (!Guid.TryParse(celulaId.ParaTexto(), out var id))
            {
                linha.Erros.Add(new ErroLinha(colunaId, campo.Chave, "Identificador inválido: copie o id do item (36 caracteres, com hífens)."));
                return;
            }

            linha.Dados[campo.Chave] = id.ToString("D");
            linha.ColunaDoCampo[campo.Chave] = colunaId;
            // O nome só aparece na prévia se o usuário vê o item (RN28); se não vê, o gravador diz "não encontrado".
            if (carregados.TryGetValue(id, out var registro) && entidadeReferenciada is not null
                && PoliticaPermissao.PodeVer(usuario, entidadeReferenciada.EhGlobal, registro))
                linha.Referencias.Add(new ReferenciaResolvida(colunaId, campo.Chave, id, rotulos.GetValueOrDefault(id)));
            return;
        }

        if (!colunas.Nome.TryGetValue(campo.Chave, out var colunaNome) || linha.Original.Celula(colunaNome) is not { EhVazia: false } celula)
            return;

        var nome = celula.ParaTexto();
        if (!candidatosPorCampo.TryGetValue(campo.Id, out var porNome))
        {
            linha.Erros.Add(new ErroLinha(colunaNome, campo.Chave,
                $"'{nomeEntidade}' não tem um nome de texto ou número para identificar os itens: use a coluna '{campo.Nome} {SugestaoMapeamento.SufixoId}'."));
            return;
        }

        var exibicao = entidadeReferenciada!.ObterCampo(entidadeReferenciada.CampoExibicaoId!.Value)!;
        var candidatos = porNome[ResolvedorReferencias.ChaveDoNome(exibicao, nome) ?? ""];
        var resolucao = ResolvedorReferencias.Resolver(
            nome, candidatos, entidade.EhGlobal ? null : linha.Equipe, usuario.Id, campo, nomeEntidade,
            AtendeDependencia(entidade, campo, linha, carregados));

        if (resolucao.Item is { } item)
        {
            linha.Dados[campo.Chave] = item.Id.ToString("D");
            linha.ColunaDoCampo[campo.Chave] = colunaNome;
            linha.Referencias.Add(new ReferenciaResolvida(colunaNome, campo.Chave, item.Id, item.Nome));
        }
        else
        {
            linha.Erros.Add(new ErroLinha(colunaNome, campo.Chave, resolucao.Erro!));
        }
    }

    /// <summary>
    /// RN08 aplicada à escolha entre nomes repetidos: o valor de origem é lido como o <see cref="ValidadorDependencias"/> lê (no
    /// registro escolhido no campo via, já resolvido nesta linha, ou na própria linha). Nulo se o campo não tem dependência.
    /// </summary>
    private static Func<CandidatoReferencia, bool>? AtendeDependencia(
        Entidade entidade, Campo campo, LinhaEmPreparo linha, IReadOnlyDictionary<Guid, Registro> carregados)
    {
        var dependencia = entidade.Dependencias.FirstOrDefault(d => d.Campo.Id == campo.Id);
        if (dependencia is null)
            return null;

        string? origem;
        if (dependencia.CampoVia is { } via)
        {
            origem = linha.Dados[via.Chave] is JsonValue valor && valor.TryGetValue<string>(out var texto)
                     && Guid.TryParse(texto, out var viaId) && carregados.TryGetValue(viaId, out var registroVia)
                ? ValidadorDependencias.LerValor(registroVia, dependencia.CampoOrigem)
                : null;
        }
        else
        {
            origem = ValidadorDependencias.ValorComparavel(dependencia.CampoOrigem, linha.Dados[dependencia.CampoOrigem.Chave]);
        }

        return candidato => carregados.TryGetValue(candidato.Id, out var registro) && ValidadorDependencias.Atende(dependencia, origem, registro);
    }

    /// <summary>
    /// A linha não passa na validação completa (ex.: "atualizar" com só as colunas da chave e as que mudam): a chave de cada
    /// restrição sai só dos campos dela, já convertidos. Assim a linha ainda é reconhecida como um item que já existe; o resto da
    /// validação continua com o gravador (RN102).
    /// </summary>
    private IReadOnlyList<ChaveUnicaCalculada> ChavesDosCamposDaRestricao(Entidade entidade, LinhaEmPreparo linha)
    {
        var chaves = new List<ChaveUnicaCalculada>();
        foreach (var restricao in entidade.RestricoesUnicas)
        {
            try
            {
                var hash = calculadora.CalcularHash(restricao, campo => linha.Dados[campo.Chave], entidade.EhGlobal ? null : linha.Equipe);
                if (hash is not null)
                    chaves.Add(new ChaveUnicaCalculada(restricao, hash));
            }
            catch (Exception erro) when (erro is FormatException or InvalidOperationException or ArgumentException)
            {
                // Valor fora do tipo: o gravador mostra o erro do campo.
            }
        }

        return chaves;
    }

    /// <summary>
    /// RN05 também na planilha: a linha que já tem um erro de conversão ou de referência mostra junto os erros dos OUTROS campos
    /// (ex.: um obrigatório vazio). Os campos que já têm erro ficam com o primeiro, sem um "obrigatório" a mais.
    /// </summary>
    private static void AcrescentarErrosDosOutrosCampos(
        LinhaEmPreparo linha, ColunasDoMapeamento colunas, IReadOnlyDictionary<string, string[]> erros)
    {
        var comErro = linha.Erros.Select(e => e.Campo).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var (chave, mensagens) in erros.Where(e => !comErro.Contains(e.Key)))
        {
            foreach (var mensagem in mensagens)
                linha.Erros.Add(new ErroLinha(colunas.ColunaDoErro(chave, linha), chave, mensagem));
        }
    }

    /// <summary>
    /// RN102: duplicados no arquivo (erro nas duas linhas) e no banco (pular ou atualizar). O que sobra é item novo.
    /// </summary>
    private static void Classificar(
        List<LinhaEmPreparo> linhas, ColunasDoMapeamento colunas, Entidade entidade, OpcoesImportacao opcoes, LotePreparacao lote)
    {
        var repetidas = DuplicadosNoArquivo.Encontrar(linhas.Where(l => l.Chaves.Count > 0).Select(l => (l.Original.Numero, l.Chaves)));

        foreach (var linha in linhas)
        {
            if (linha.Erros.Count > 0)
            {
                linha.Situacao = SituacaoLinha.Erro;
                continue;
            }

            if (repetidas.TryGetValue(linha.Original.Numero, out var repetida))
            {
                var restricao = entidade.RestricoesUnicas.First(r => r.Nome == repetida.Restricao);
                linha.Erros.Add(new ErroLinha(colunas.ColunaDe(restricao.Campos[0]), restricao.Campos[0].Chave,
                    $"Repetida no arquivo: a(s) linha(s) {string.Join(", ", repetida.Outras)} têm o mesmo valor em " +
                    $"{string.Join(", ", restricao.Campos.Select(c => c.Nome))}."));
                linha.Situacao = SituacaoLinha.Erro;
                continue;
            }

            var existentes = linha.Chaves.Select(lote.DonoDaChave).OfType<Guid>().Distinct().ToList();
            if (existentes.Count == 0)
            {
                linha.Situacao = SituacaoLinha.Nova;
            }
            else if (opcoes.Duplicados == AcaoDuplicados.Pular)
            {
                linha.Situacao = SituacaoLinha.Ignorada;
                linha.RegistroId = existentes[0];
            }
            else if (existentes.Count > 1)
            {
                linha.Erros.Add(new ErroLinha(null, null,
                    "Os valores desta linha correspondem a mais de um item que já existe (restrições únicas diferentes): não dá para saber qual atualizar."));
                linha.Situacao = SituacaoLinha.Erro;
            }
            else
            {
                linha.Situacao = SituacaoLinha.Atualizacao;
                linha.RegistroId = existentes[0];
            }
        }

        // Duas linhas não podem atualizar o mesmo item.
        foreach (var mesmoItem in linhas.Where(l => l.Situacao == SituacaoLinha.Atualizacao).GroupBy(l => l.RegistroId).Where(g => g.Count() > 1))
        {
            foreach (var linha in mesmoItem)
            {
                linha.Erros.Add(new ErroLinha(null, null,
                    $"As linhas {string.Join(", ", mesmoItem.Select(l => l.Original.Numero))} atualizariam o mesmo item."));
                linha.Situacao = SituacaoLinha.Erro;
            }
        }
    }

    private async Task GravarAsync(
        LinhaEmPreparo linha, ColunasDoMapeamento colunas, Grupo grupo, Entidade entidade, UsuarioAtual usuario, OrigemGravacao origem,
        LotePreparacao lote, IReadOnlyDictionary<Guid, Registro> paraAlterar, CancellationToken cancellationToken)
    {
        try
        {
            if (linha.Situacao == SituacaoLinha.Nova)
            {
                var registro = await gravador.CriarAsync(grupo, entidade, usuario, linha.Dados, origem, cancellationToken, lote);
                linha.RegistroId = registro.Id;
                return;
            }

            if (!paraAlterar.TryGetValue(linha.RegistroId!.Value, out var existente))
                throw new RecursoNaoEncontradoException("Item não encontrado.", CodigosErro.RegistroNaoEncontrado);
            await gravador.AlterarAsync(grupo, entidade, usuario, existente, DadosDaAtualizacao(existente, entidade, linha, colunas), origem,
                cancellationToken, lote);
        }
        catch (ValidacaoRegistroException erro)
        {
            foreach (var (chave, mensagens) in erro.Erros)
            {
                foreach (var mensagem in mensagens)
                    linha.Erros.Add(new ErroLinha(colunas.ColunaDoErro(chave, linha), chave, mensagem));
            }

            linha.Situacao = SituacaoLinha.Erro;
        }
        catch (ConflitoUnicidadeException erro)
        {
            var primeiro = erro.Campos.Count > 0 ? erro.Campos[0] : null;
            linha.Erros.Add(new ErroLinha(primeiro is null ? null : colunas.ColunaDoErro(primeiro, linha), primeiro, erro.Message));
            linha.Situacao = SituacaoLinha.Erro;
        }
        catch (AcessoNegadoException)
        {
            linha.Erros.Add(new ErroLinha(colunas.Equipe, null, linha.Situacao == SituacaoLinha.Nova
                ? "Você não pode criar itens nesta equipe."
                : "Já existe um item com estes valores, e você não pode alterá-lo."));
            linha.Situacao = SituacaoLinha.Erro;
        }
        catch (RecursoNaoEncontradoException)
        {
            // Item que o usuário não vê (RN28) ou equipe inexistente: a mesma mensagem, sem revelar mais nada.
            linha.Erros.Add(new ErroLinha(linha.Situacao == SituacaoLinha.Nova ? colunas.Equipe : null, null, linha.Situacao == SituacaoLinha.Nova
                ? "Equipe não encontrada."
                : "Já existe um item com estes valores, e você não pode alterá-lo."));
            linha.Situacao = SituacaoLinha.Erro;
        }

        if (linha.Situacao == SituacaoLinha.Erro && linha.RegistroId is not null && linha.Erros.Count > 0)
            linha.RegistroId = null;
    }

    /// <summary>
    /// "Atualizar o item existente": a planilha manda nas colunas MAPEADAS (célula vazia = valor apagado, como no PUT, RN15); os campos
    /// sem coluna ficam como estão. A equipe não muda (RN27) e os campos inativos são preservados pelo gravador.
    /// </summary>
    private static JsonObject DadosDaAtualizacao(Registro existente, Entidade entidade, LinhaEmPreparo linha, ColunasDoMapeamento colunas)
    {
        var dados = existente.ParaJsonPlano(entidade);
        foreach (var inativo in entidade.Campos.Where(c => !c.Ativo))
            dados.Remove(inativo.Chave);
        foreach (var chave in dados.Select(p => p.Key).Where(k => entidade.ObterCampo(k) is null).ToList())
            dados.Remove(chave);

        foreach (var chave in colunas.CamposMapeados)
        {
            if (linha.Dados.TryGetPropertyValue(chave, out var valor) && valor is not null)
                dados[chave] = valor.DeepClone();
            else
                dados.Remove(chave);
        }

        return dados;
    }

    /// <summary>Uma linha durante a simulação.</summary>
    private sealed class LinhaEmPreparo(LinhaImportada original)
    {
        public LinhaImportada Original { get; } = original;

        /// <summary>O corpo que o cadastro manual enviaria (campos e "equipeId").</summary>
        public JsonObject Dados { get; } = [];

        public List<ErroLinha> Erros { get; } = [];
        public List<ReferenciaResolvida> Referencias { get; } = [];

        /// <summary>A coluna de onde veio cada referência (nome ou ID), para destacar a célula certa num erro.</summary>
        public Dictionary<string, int> ColunaDoCampo { get; } = new(StringComparer.Ordinal);

        public Guid? Equipe { get; set; }
        public bool EquipeConhecida { get; set; }
        public IReadOnlyList<ChaveUnicaCalculada> Chaves { get; set; } = [];
        public SituacaoLinha Situacao { get; set; }
        public Guid? RegistroId { get; set; }

        public ResultadoLinha Resultado() => new(Original.Numero, Situacao, Erros, Referencias, RegistroId);
    }

    /// <summary>O mapeamento visto pelos campos: em que coluna está cada campo, o ID de cada referência e a equipe.</summary>
    private sealed class ColunasDoMapeamento
    {
        public ColunasDoMapeamento(Entidade entidade, MapeamentoImportacao mapeamento)
        {
            foreach (var coluna in mapeamento.Colunas)
            {
                var campo = coluna.Destino.CampoChave is string chave ? entidade.ObterCampo(chave) : null;
                switch (coluna.Destino.Tipo)
                {
                    case TipoDestinoColuna.Campo when campo is not null:
                        Nome[campo.Chave] = coluna.Indice;
                        break;
                    case TipoDestinoColuna.CampoId when campo is not null:
                        Identificador[campo.Chave] = coluna.Indice;
                        break;
                    case TipoDestinoColuna.Equipe:
                        Equipe = coluna.Indice;
                        break;
                }
            }

            Simples = entidade.Campos.Where(c => !c.EhReferencia && Nome.ContainsKey(c.Chave)).Select(c => (c, Nome[c.Chave])).ToList();
            Referencias = OrdenarPorDependencia(entidade,
                entidade.Campos.Where(c => c.EhReferencia && (Nome.ContainsKey(c.Chave) || Identificador.ContainsKey(c.Chave))).ToList());
            CamposMapeados = Nome.Keys.Concat(Identificador.Keys).Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>Coluna do valor de cada campo (nas referências, a do nome).</summary>
        public Dictionary<string, int> Nome { get; } = new(StringComparer.Ordinal);

        /// <summary>Coluna "&lt;campo&gt; (ID)" de cada referência.</summary>
        public Dictionary<string, int> Identificador { get; } = new(StringComparer.Ordinal);

        public int? Equipe { get; private set; }
        public IReadOnlyList<(Campo Campo, int Indice)> Simples { get; }
        public IReadOnlyList<Campo> Referencias { get; }
        public IReadOnlyList<string> CamposMapeados { get; }

        public int? ColunaDe(Campo campo) =>
            Nome.TryGetValue(campo.Chave, out var nome) ? nome : Identificador.TryGetValue(campo.Chave, out var id) ? id : null;

        /// <summary>A célula de um erro do gravador: a coluna do campo (na referência, a que foi usada) ou a da equipe.</summary>
        public int? ColunaDoErro(string chave, LinhaEmPreparo linha) =>
            chave == ChavesReservadas.EquipeId ? Equipe
            : linha.ColunaDoCampo.TryGetValue(chave, out var usada) ? usada
            : Nome.TryGetValue(chave, out var nome) ? nome
            : Identificador.TryGetValue(chave, out var id) ? id
            : null;

        /// <summary>Cada referência depois das que a dependência dela usa (o campo via, ou a origem na própria linha).</summary>
        private static IReadOnlyList<Campo> OrdenarPorDependencia(Entidade entidade, List<Campo> referencias)
        {
            var ordenadas = new List<Campo>();
            var restantes = new List<Campo>(referencias);
            while (restantes.Count > 0)
            {
                var prontas = restantes.Where(c => Prerequisitos(entidade, c).All(p => ordenadas.Contains(p) || !restantes.Contains(p))).ToList();
                if (prontas.Count == 0)
                    prontas = [restantes[0]]; // ciclo (não deveria existir, RN55): segue na ordem dos campos
                ordenadas.AddRange(prontas);
                restantes.RemoveAll(prontas.Contains);
            }

            return ordenadas;
        }

        private static IEnumerable<Campo> Prerequisitos(Entidade entidade, Campo campo) =>
            entidade.Dependencias
                .Where(d => d.Campo.Id == campo.Id)
                .Select(d => d.CampoVia ?? d.CampoOrigem)
                .Where(c => c.EhReferencia && c.EntidadeId == entidade.Id);
    }
}
