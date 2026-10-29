using System.Text.Json.Nodes;

namespace Catalogo.Domain.Metadados.Administracao;

/// <summary>
/// RN58 a RN77: para cada alteração de metadados, QUAIS verificações contra os dados existentes precisam ser feitas,
/// quais escritas em massa ela exige e quais usos (pela configuração) a impedem. Código puro: quem executa as
/// verificações (em LINQ, numa transação Serializable) é a Infrastructure.
///
/// Regra deste arquivo: toda alteração que pode deixar um registro inválido tem uma verificação aqui, e cada
/// verificação tem um teste em PlanejadorAlteracoesTests (Domain) e um de integração com dados.
/// </summary>
public static class PlanejadorAlteracoes
{
    // Códigos estáveis dos impactos (o front decide por eles). Documentados em docs/api/administracao-de-metadados.md.
    public const string CampoObrigatorioSemValor = "CAMPO_OBRIGATORIO_SEM_VALOR";
    public const string ValorPadraoAplicado = "VALOR_PADRAO_APLICADO";
    public const string TextoAcimaDoTamanho = "TEXTO_ACIMA_DO_TAMANHO";
    public const string NumeroForaDaPrecisao = "NUMERO_FORA_DA_PRECISAO";
    public const string OpcaoEmUso = "OPCAO_EM_USO";
    public const string CampoComValores = "CAMPO_COM_VALORES";
    public const string CampoEmUso = "CAMPO_EM_USO";
    public const string DuplicatasNaRestricao = "DUPLICATAS_NA_RESTRICAO";
    public const string ChavesUnicasGeradas = "CHAVES_UNICAS_GERADAS";
    public const string DependenciaViolada = "DEPENDENCIA_VIOLADA";
    public const string EntidadeComRegistros = "ENTIDADE_COM_REGISTROS";
    public const string EntidadeComHistorico = "ENTIDADE_COM_HISTORICO";
    public const string EntidadeComConfiguracoes = "ENTIDADE_COM_CONFIGURACOES";
    public const string EntidadeReferenciada = "ENTIDADE_REFERENCIADA";
    public const string GrupoComEntidades = "GRUPO_COM_ENTIDADES";
    public const string ValorNaoEUrl = "VALOR_NAO_E_URL";
    public const string ItensOcultos = "ITENS_OCULTOS";

    /// <summary>
    /// RN59 e RN60: campo novo. Opcional: sempre permitido. Obrigatório numa entidade com registros: exige o valor
    /// padrão, que é gravado em todos os registros existentes.
    /// </summary>
    public static PlanoAlteracao NovoCampo(Entidade entidade, Campo novo, JsonNode? valorPadrao)
    {
        if (!novo.Obrigatorio)
            return PlanoAlteracao.Vazio;

        return valorPadrao is null
            ? new PlanoAlteracao(
                [new RegistrosSemValor(novo, CampoObrigatorioSemValor,
                    $"A entidade '{entidade.Chave}' tem registros, que ficariam sem valor no campo obrigatório '{novo.Chave}'. " +
                    "Informe \"valorPadrao\" para preenchê-los, ou crie o campo como opcional.", Bloqueia: true)],
                [], [])
            : ComValorPadrao(entidade, novo, valorPadrao, apenasVazios: false);
    }

    /// <summary>
    /// RN61 a RN68: alteração de um campo existente (<paramref name="novo"/> já passou por
    /// <see cref="RegrasMetadados.ValidarCampo"/>; o tipo só pode ter mudado como RegrasMetadados.MudancaDeTipoPermitida, RN66).
    /// </summary>
    public static PlanoAlteracao AlteracaoCampo(Entidade entidade, Campo atual, Campo novo, JsonNode? valorPadrao)
    {
        var verificacoes = new List<VerificacaoDados>();
        var escritas = new List<EscritaRegistros>();

        // RN61: tornar obrigatório. RN62: tornar opcional é sempre permitido.
        if (novo.Obrigatorio && !atual.Obrigatorio && novo.Ativo)
        {
            if (valorPadrao is null)
            {
                verificacoes.Add(new RegistrosSemValor(atual, CampoObrigatorioSemValor,
                    $"Registros sem valor no campo '{atual.Chave}', que passaria a ser obrigatório. Preencha-os ou informe \"valorPadrao\" " +
                    "(ele preenche só os vazios).", Bloqueia: true));
            }
            else
            {
                var plano = ComValorPadrao(entidade, atual, valorPadrao, apenasVazios: true);
                verificacoes.AddRange(plano.Verificacoes);
                escritas.AddRange(plano.Escritas);
            }
        }

        // RN66: para URL, só se TODO valor existente for uma URL válida (a mesma regra do validador, ADR 0042). A normalização da
        // chave única muda (o texto ignora maiúsculas e acentos; a URL só no esquema e no host): as chaves são recalculadas.
        if (novo.TipoDado != atual.TipoDado)
        {
            if (novo.TipoDado == TipoDado.Url)
            {
                verificacoes.Add(new ValoresQueNaoSaoUrl(atual, ValorNaoEUrl,
                    $"Valores de '{atual.Chave}' que não são uma URL válida (http:// ou https://, sem espaços, até " +
                    $"{Validacao.Tipos.UrlValidador.MaximoCaracteres} caracteres). Corrija-os antes de mudar o tipo para URL."));
            }

            escritas.AddRange(RecalcularRestricoesDoCampo(entidade, atual));
        }

        // RN63 e RN64: diminuir o tamanho do TEXTO só se nenhum valor exceder; aumentar, sempre.
        if (novo.TipoDado == TipoDado.Texto && novo.TamanhoMaximo < atual.TamanhoMaximo)
        {
            verificacoes.Add(new TextosAcimaDoTamanho(atual, novo.TamanhoMaximo!.Value, TextoAcimaDoTamanho,
                $"Valores de '{atual.Chave}' com mais de {novo.TamanhoMaximo} caracteres (o limite era {atual.TamanhoMaximo})."));
        }

        // RN64 e RN65: DECIMAL. Diminuir a escala OU a parte inteira (precisão − escala) só se todos os valores couberem.
        // Aumentar só a escala diminui a parte inteira: por isso a conta é pela parte inteira, e não só pela precisão.
        if (novo.TipoDado == TipoDado.Decimal)
        {
            var inteirosAtual = atual.Precisao!.Value - atual.Escala!.Value;
            var inteirosNovo = novo.Precisao!.Value - novo.Escala!.Value;
            if (novo.Escala < atual.Escala || inteirosNovo < inteirosAtual)
            {
                verificacoes.Add(new NumerosForaDaPrecisao(atual, novo.Precisao.Value, novo.Escala!.Value, NumeroForaDaPrecisao,
                    $"Valores de '{atual.Chave}' que não cabem em DECIMAL({novo.Precisao},{novo.Escala}): mais de {novo.Escala} " +
                    $"casas decimais ou mais de {inteirosNovo} dígitos na parte inteira."));
            }

            // A escala entra no hash da chave única (RN09: "12.5" vira "12.50"): as chaves das restrições com o campo mudam.
            if (novo.Escala != atual.Escala)
                escritas.AddRange(RecalcularRestricoesDoCampo(entidade, atual));
        }

        // RN67 e RN68: LISTA. Opção nova: sempre. Opção removida: só se nenhum registro a usa.
        if (novo.TipoDado == TipoDado.Lista)
        {
            var removidas = atual.Opcoes.Except(novo.Opcoes, StringComparer.Ordinal).ToList();
            if (removidas.Count > 0)
            {
                verificacoes.Add(new ValoresComOpcoes(atual, removidas, OpcaoEmUso,
                    $"Registros que usam a(s) opção(ões) removida(s) de '{atual.Chave}': {string.Join(", ", removidas)}."));
            }
        }

        // A ordem dos campos é a ordem do hash das restrições compostas (RN09): mudar a ordem muda as chaves.
        if (novo.Ordem != atual.Ordem)
            escritas.AddRange(RecalcularSeAOrdemMudou(entidade, entidade.Campos.Select(c => c.Id == atual.Id ? novo : c).ToList()));

        return new PlanoAlteracao(verificacoes, escritas.Distinct().ToList(), []);
    }

    /// <summary>
    /// RN69: desativar campo. Bloqueado se ele é campo de exibição, está numa restrição única ou numa dependência ativa.
    /// Os valores ficam guardados. Reativar um campo obrigatório: os registros criados enquanto ele estava inativo não
    /// têm valor e ficariam inválidos, então é conferido como tornar obrigatório (RN61).
    /// </summary>
    public static PlanoAlteracao AtivacaoCampo(Grupo grupo, Entidade entidade, Campo campo, bool ativar)
    {
        if (ativar)
        {
            return campo.Obrigatorio
                ? new PlanoAlteracao(
                    [new RegistrosSemValor(campo, CampoObrigatorioSemValor,
                        $"Registros sem valor no campo obrigatório '{campo.Chave}' (criados enquanto ele estava inativo). " +
                        "Torne-o opcional antes de reativar, ou preencha-os.", Bloqueia: true)],
                    [], [])
                : PlanoAlteracao.Vazio;
        }

        var usos = RegrasMetadados.UsosDoCampo(grupo, entidade, campo, soDependenciasAtivas: true);
        return usos.Count == 0
            ? PlanoAlteracao.Vazio
            : new PlanoAlteracao([], [],
                [Uso(CampoEmUso, $"O campo '{campo.Chave}' está em uso: tire-o desses lugares antes de desativá-lo.", usos)]);
    }

    /// <summary>
    /// RN70: excluir campo. Só se nenhum registro tem valor e ele não é usado em restrição, dependência (ativa ou não)
    /// ou exibição. Senão, a orientação é desativar.
    /// </summary>
    public static PlanoAlteracao ExcluirCampo(Grupo grupo, Entidade entidade, Campo campo)
    {
        var usos = RegrasMetadados.UsosDoCampo(grupo, entidade, campo, soDependenciasAtivas: false);
        return new PlanoAlteracao(
            [new RegistrosComValor(campo, CampoComValores,
                $"Registros com valor em '{campo.Chave}': o campo não pode ser excluído. Desative-o: os valores ficam guardados.")],
            [],
            usos.Count == 0 ? [] : [Uso(CampoEmUso, $"O campo '{campo.Chave}' está em uso: tire-o desses lugares antes de excluí-lo.", usos)]);
    }

    /// <summary>
    /// RN71: restrição nova. Bloqueada se os dados já têm duplicatas (com o mesmo hash da unicidade, RN09). Se passar,
    /// as chaves de todos os registros existentes são geradas na mesma transação. <paramref name="entidadeComRestricao"/>
    /// já tem a restrição nova.
    /// </summary>
    public static PlanoAlteracao NovaRestricao(Entidade entidadeComRestricao, RestricaoUnica restricao) => new(
        [
            new DuplicatasNaRestricao(entidadeComRestricao, restricao, DuplicatasNaRestricao,
                $"Registros de '{entidadeComRestricao.Chave}' que já repetem os valores de {string.Join(" + ", restricao.Campos.Select(c => c.Chave))}. " +
                "Corrija os duplicados antes de criar a restrição."),
            new RegistrosDaEntidade(entidadeComRestricao.Id, ChavesUnicasGeradas,
                $"Registros que vão ganhar a chave única '{restricao.Nome}'.", Bloqueia: false),
        ],
        [new RecalcularChavesUnicas(entidadeComRestricao.Id, [restricao.Nome])],
        []);

    /// <summary>RN73: dependência nova ou alterada. Bloqueada se algum registro existente já viola a regra (RN08).</summary>
    public static PlanoAlteracao NovaDependencia(CampoDependencia dependencia) => new(
        [new ViolacoesDaDependencia(dependencia, DependenciaViolada,
            $"Registros em que '{dependencia.Campo.Chave}' aponta para um registro cujo '{dependencia.CampoAlvo.Chave}' é diferente de " +
            $"'{dependencia.CampoOrigem.Chave}'" + (dependencia.CampoVia is null ? "." : $" (lido em '{dependencia.CampoVia.Chave}')."))],
        [], []);

    /// <summary>
    /// RN74: mudar o escopo (global ↔ por equipe) só numa entidade sem registros e sem configuração de equipe
    /// (aprovação ou modelo de avaliação). A regra de referências globais é de configuração (RN54).
    /// </summary>
    public static PlanoAlteracao MudarEscopo(Entidade entidade) => new(
        [
            new RegistrosDaEntidade(entidade.Id, EntidadeComRegistros,
                $"'{entidade.Chave}' tem registros: o escopo só muda numa entidade vazia (os registros teriam de mudar de equipe)."),
            new ConfiguracoesDaEntidade(entidade.Id, EntidadeComConfiguracoes,
                $"'{entidade.Chave}' tem aprovação ou modelo de avaliação configurados em alguma equipe."),
        ],
        [], []);

    /// <summary>
    /// RN76: excluir entidade só se nunca teve registros (o histórico deles aponta para ela e não é apagado, RN32), não
    /// tem configuração de equipe e não é referenciada por campos de outras entidades. Senão, desative-a (RN75).
    /// </summary>
    public static PlanoAlteracao ExcluirEntidade(Grupo grupo, Entidade entidade)
    {
        var referencias = RegrasMetadados.ReferenciasParaEntidade(grupo, entidade);
        return new PlanoAlteracao(
            [
                new RegistrosDaEntidade(entidade.Id, EntidadeComRegistros, $"'{entidade.Chave}' tem registros. Desative-a."),
                new HistoricoDaEntidade(entidade.Id, EntidadeComHistorico,
                    $"'{entidade.Chave}' já teve registros: o histórico deles é permanente e aponta para ela. Desative-a."),
                new ConfiguracoesDaEntidade(entidade.Id, EntidadeComConfiguracoes,
                    $"'{entidade.Chave}' tem aprovação ou modelo de avaliação configurados em alguma equipe."),
            ],
            [],
            referencias.Count == 0 ? [] : [Uso(EntidadeReferenciada, $"'{entidade.Chave}' é referenciada por outros campos.", referencias)]);
    }

    /// <summary>
    /// RN92: mudar as equipes habilitadas na listagem. Habilitar não tem efeito nos dados. Tirar uma equipe é PERMITIDO, com aviso:
    /// para cada equipe removida, quantos itens dela ficam ocultos para os membros (só o ADMINISTRADOR os vê). Nada é apagado, e
    /// tudo volta ao habilitá-la de novo.
    /// </summary>
    /// <param name="nomes">Nome de cada equipe (para a descrição do impacto).</param>
    public static PlanoAlteracao EquipesDaListagem(Grupo grupo, IReadOnlySet<Guid> novas, IReadOnlyDictionary<Guid, string> nomes) => new(
        grupo.EquipesHabilitadas
            .Where(e => !novas.Contains(e))
            .OrderBy(e => nomes.GetValueOrDefault(e), StringComparer.CurrentCulture)
            .Select(e => (VerificacaoDados)new RegistrosDaEquipeNoGrupo(grupo.Id, e, ItensOcultos,
                $"Os itens da equipe '{nomes.GetValueOrDefault(e) ?? e.ToString()}' ficam ocultos para os membros dela (só o " +
                "administrador os vê). Nada é apagado: eles voltam ao habilitar a equipe de novo."))
            .ToList(),
        [], []);

    /// <summary>Excluir grupo: só sem entidades (RN77; um grupo com entidades é desativado).</summary>
    public static PlanoAlteracao ExcluirGrupo(Grupo grupo) =>
        grupo.Entidades.Count == 0
            ? PlanoAlteracao.Vazio
            : new PlanoAlteracao([], [],
                [Uso(GrupoComEntidades, $"O grupo '{grupo.Chave}' tem entidades: desative-o.", grupo.Entidades.Select(e => $"entidade '{e.Chave}'").ToList())]);

    /// <summary>
    /// Nova ordem dos campos: as restrições COMPOSTAS cuja ordem relativa dos campos mudou têm as chaves recalculadas,
    /// porque a ordem do hash é Campo.Ordem (RN09). Restrição de um campo só não muda.
    /// </summary>
    public static PlanoAlteracao ReordenarCampos(Entidade entidade, IReadOnlyList<Campo> camposNaNovaOrdem)
    {
        var comNovaOrdem = camposNaNovaOrdem.Select((c, i) => Copiar(c, ordem: i + 1)).ToList();
        return new PlanoAlteracao([], RecalcularSeAOrdemMudou(entidade, comNovaOrdem).ToList(), []);
    }

    private static PlanoAlteracao ComValorPadrao(Entidade entidade, Campo campo, JsonNode valorPadrao, bool apenasVazios) => new(
        [new RegistrosSemValor(campo, ValorPadraoAplicado,
            $"Registros que vão receber o valor padrão {valorPadrao.ToJsonString()} em '{campo.Chave}'.", Bloqueia: false)],
        [new PreencherValorPadrao(entidade.Id, campo.Chave, valorPadrao, apenasVazios)],
        []);

    private static IEnumerable<EscritaRegistros> RecalcularRestricoesDoCampo(Entidade entidade, Campo campo)
    {
        var restricoes = entidade.RestricoesUnicas.Where(r => r.Campos.Any(c => c.Id == campo.Id)).Select(r => r.Nome).ToList();
        return restricoes.Count == 0 ? [] : [new RecalcularChavesUnicas(entidade.Id, restricoes)];
    }

    private static IEnumerable<EscritaRegistros> RecalcularSeAOrdemMudou(Entidade entidade, IReadOnlyList<Campo> camposNovos)
    {
        var ordemNova = camposNovos.ToDictionary(c => c.Id);
        var mudaram = entidade.RestricoesUnicas
            .Where(r => r.Campos.Count > 1)
            .Where(r =>
            {
                var antes = r.Campos.Select(c => c.Id);
                var depois = r.Campos.Select(c => ordemNova[c.Id]).OrderBy(c => c.Ordem).ThenBy(c => c.Chave, StringComparer.Ordinal).Select(c => c.Id);
                return !antes.SequenceEqual(depois);
            })
            .Select(r => r.Nome)
            .ToList();
        return mudaram.Count == 0 ? [] : [new RecalcularChavesUnicas(entidade.Id, mudaram)];
    }

    private static ImpactoDados Uso(string tipo, string descricao, IReadOnlyList<string> usos) =>
        new(tipo, descricao, Bloqueia: true, Quantidade: 0, Exemplos: [], Usos: usos);

    private static Campo Copiar(Campo c, int ordem) => new()
    {
        Id = c.Id, EntidadeId = c.EntidadeId, Chave = c.Chave, Nome = c.Nome, TipoDado = c.TipoDado, Obrigatorio = c.Obrigatorio,
        TamanhoMaximo = c.TamanhoMaximo, Precisao = c.Precisao, Escala = c.Escala, Opcoes = c.Opcoes,
        EntidadeReferenciadaId = c.EntidadeReferenciadaId, AcaoExclusao = c.AcaoExclusao, Ordem = ordem, Ativo = c.Ativo,
    };
}
