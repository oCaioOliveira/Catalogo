using System.Text.RegularExpressions;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Metadados.Administracao;

/// <summary>
/// Regras de CONFIGURAÇÃO dos metadados (RN51, RN53 a RN55), sem olhar os dados: as mesmas dos CHECKs do banco
/// (V001, V002) e as de coerência entre metadados. Código puro. Todos os erros vão juntos, por campo do corpo
/// (RN05): "chave", "tipoDado", "opcoes[1]", "campos[0]"... Cada método devolve nulo se encontrou erros.
/// As regras que dependem dos dados ficam no <see cref="PlanejadorAlteracoes"/>.
/// </summary>
public static partial class RegrasMetadados
{
    public const int TamanhoMaximoChave = 100;
    public const int TamanhoMaximoNome = 150;
    public const int TamanhoMaximoDescricao = 500;
    public const int TamanhoMaximoTexto = 4000;
    public const int MaximoCamposRestricao = 5;

    /// <summary>"ordem" é rota (PUT .../campos/ordem reordena os campos): um campo com essa chave não seria alcançável.</summary>
    public const string ChaveDeRota = "ordem";

    // ---- Grupo ----

    /// <summary>RN53: grupo novo. A chave é única entre os grupos.</summary>
    public static DefinicaoGrupo? ValidarNovoGrupo(DefinicaoGrupo definicao, IEnumerable<string> chavesExistentes, ErrosPorCampo erros)
    {
        var antes = Contar(erros);
        var chave = ValidarChaveNova(definicao.Chave, chavesExistentes, "chave", "grupo", erros);
        var nome = ValidarNome(definicao.Nome, erros);
        var descricao = ValidarDescricao(definicao.Descricao, erros);
        return Contar(erros) > antes ? null : new DefinicaoGrupo(chave, nome, descricao);
    }

    /// <summary>RN51 e RN53: nome e descrição mudam; a chave, se vier, tem de ser a mesma.</summary>
    public static DefinicaoGrupo? ValidarAlteracaoGrupo(Grupo atual, DefinicaoGrupo definicao, ErrosPorCampo erros)
    {
        var antes = Contar(erros);
        ConferirImutavel(definicao.Chave, atual.Chave, "chave", erros);
        var nome = ValidarNome(definicao.Nome, erros);
        var descricao = ValidarDescricao(definicao.Descricao, erros);
        return Contar(erros) > antes ? null : new DefinicaoGrupo(atual.Chave, nome, descricao);
    }

    // ---- Entidade ----

    /// <summary>
    /// RN51, RN53 e RN54: entidade nova (<paramref name="atual"/> nulo) ou alterada. Chave única no grupo e imutável;
    /// campo de exibição (só na alteração: a entidade nova ainda não tem campos); e, ao mudar o escopo, a regra de
    /// referências globais nos dois sentidos (os dados são conferidos pelo planejador, RN74).
    /// </summary>
    public static EntidadeDefinida? ValidarEntidade(Grupo grupo, Entidade? atual, DefinicaoEntidade definicao, ErrosPorCampo erros)
    {
        var antes = Contar(erros);

        var chave = atual is null
            ? ValidarChaveNova(definicao.Chave, grupo.Entidades.Select(e => e.Chave), "chave", "entidade", erros)
            : ConferirImutavel(definicao.Chave, atual.Chave, "chave", erros);
        var nome = ValidarNome(definicao.Nome, erros);
        var descricao = ValidarDescricao(definicao.Descricao, erros);
        var nomePlural = ValidarNomePlural(definicao.NomePlural, erros);
        var icone = ValidarIcone(definicao.Icone, erros);
        var ordem = definicao.Ordem ?? atual?.Ordem ?? (grupo.Entidades.Count == 0 ? 1 : grupo.Entidades.Max(e => e.Ordem) + 1);
        var escopoEquipe = definicao.EscopoEquipe ?? atual?.EscopoEquipe ?? true;

        if (atual is not null && escopoEquipe != atual.EscopoEquipe)
            ValidarMudancaDeEscopo(grupo, atual, escopoEquipe, erros);

        Guid? campoExibicao = null;
        if (atual is null && !string.IsNullOrWhiteSpace(definicao.CampoExibicao))
        {
            erros.Adicionar("campoExibicao", "A entidade nova ainda não tem campos: defina o campo de exibição depois de criar os campos.");
        }
        else if (atual is not null && !string.IsNullOrWhiteSpace(definicao.CampoExibicao))
        {
            var campo = atual.ObterCampo(definicao.CampoExibicao.Trim());
            if (ValidarCampoExibicao(atual, campo, definicao.CampoExibicao.Trim(), erros))
                campoExibicao = campo!.Id;
        }

        return Contar(erros) > antes ? null : new EntidadeDefinida(chave, nome!, descricao, ordem, escopoEquipe, campoExibicao, nomePlural, icone);
    }

    /// <summary>Tamanho da coluna Entidade.Icone (V010).</summary>
    public const int TamanhoMaximoIcone = 50;

    /// <summary>
    /// Ícone (RN95): opcional, o nome de um ícone do PrimeIcons, como "pi-chart-line": "pi-", depois letras minúsculas, números e
    /// hífens, até 50 caracteres. Vazio = sem ícone (as telas usam um genérico). Se o nome existe no PrimeIcons, quem confere é a
    /// tela (o seletor só oferece os que existem): um nome desconhecido só deixa de aparecer, sem quebrar nada.
    /// </summary>
    private static string? ValidarIcone(string? texto, ErrosPorCampo erros)
    {
        var icone = texto?.Trim();
        if (string.IsNullOrEmpty(icone))
            return null;
        if (icone.Length > TamanhoMaximoIcone || !PadraoIcone().IsMatch(icone))
            erros.Adicionar("icone", $"Use o nome de um ícone do PrimeIcons, como \"pi-chart-line\" (até {TamanhoMaximoIcone} caracteres).");
        return icone;
    }

    /// <summary>Nome no plural: opcional, sem espaços nas pontas, até 150 caracteres (como o nome). Vazio = sem plural.</summary>
    private static string? ValidarNomePlural(string? texto, ErrosPorCampo erros)
    {
        var plural = texto?.Trim();
        if (string.IsNullOrEmpty(plural))
            return null;
        if (plural.Length > TamanhoMaximoNome)
            erros.Adicionar("nomePlural", $"Deve ter no máximo {TamanhoMaximoNome} caracteres.");
        return plural;
    }

    /// <summary>
    /// RN54: um registro global não aponta para registros de equipe (RN29). Nos dois sentidos: a entidade que vira
    /// GLOBAL só pode ter REFERENCIAs para globais; a que vira POR EQUIPE não pode ser referenciada por globais.
    /// </summary>
    private static void ValidarMudancaDeEscopo(Grupo grupo, Entidade entidade, bool escopoEquipe, ErrosPorCampo erros)
    {
        if (!escopoEquipe)
        {
            foreach (var campo in entidade.Campos.Where(c => c.EhReferencia))
            {
                if (grupo.ObterEntidade(campo.EntidadeReferenciadaId!.Value) is { EscopoEquipe: true } destino)
                {
                    erros.Adicionar("escopoEquipe",
                        $"O campo '{campo.Chave}' aponta para '{destino.Chave}', que é por equipe: uma entidade global só referencia entidades globais.");
                }
            }
        }
        else
        {
            foreach (var origem in grupo.Entidades.Where(e => e.EhGlobal && e.Id != entidade.Id))
            {
                foreach (var campo in origem.Campos.Where(c => c.EntidadeReferenciadaId == entidade.Id))
                {
                    erros.Adicionar("escopoEquipe",
                        $"A entidade global '{origem.Chave}' aponta para esta pelo campo '{campo.Chave}': esta precisa continuar global.");
                }
            }
        }
    }

    /// <summary>RN54: campo de exibição é um campo ATIVO da própria entidade, que não é REFERENCIA nem TEXTO_LONGO.</summary>
    public static bool ValidarCampoExibicao(Entidade entidade, Campo? campo, string chave, ErrosPorCampo erros)
    {
        string? erro = campo switch
        {
            null => $"Campo '{chave}' não existe em '{entidade.Chave}'.",
            { Ativo: false } => $"O campo '{chave}' está inativo.",
            { TipoDado: TipoDado.Referencia or TipoDado.TextoLongo } =>
                $"O campo '{chave}' é {TipoDadoConversor.ParaTexto(campo.TipoDado)}: o campo de exibição não pode ser REFERENCIA nem TEXTO_LONGO.",
            _ => null,
        };

        if (erro is not null)
            erros.Adicionar("campoExibicao", erro);
        return erro is null;
    }

    // ---- Campo ----

    /// <summary>
    /// RN51, RN53, RN54 e RN66: campo novo (<paramref name="atual"/> nulo) ou alterado, com os atributos de cada tipo
    /// iguais aos CHECKs do banco. Na alteração: chave, tipo (salvo as mudanças de <see cref="MudancaDeTipoPermitida"/>) e
    /// entidade referenciada não
    /// mudam; ausentes, valem os atuais (também a ação de exclusão e a ordem). Devolve o campo como ficaria.
    /// </summary>
    public static Campo? ValidarCampo(Grupo grupo, Entidade entidade, Campo? atual, DefinicaoCampo definicao, ErrosPorCampo erros)
    {
        var antes = Contar(erros);

        var chave = atual is null
            ? ValidarChaveNova(definicao.Chave, entidade.Campos.Select(c => c.Chave), "chave", "campo", erros, ehCampo: true)
            : ConferirImutavel(definicao.Chave, atual.Chave, "chave", erros);
        var nome = ValidarNome(definicao.Nome, erros);
        var tipo = ValidarTipo(atual, definicao.TipoDado, erros);

        var tamanho = ValidarTamanho(tipo, definicao.TamanhoMaximo, erros);
        var (precisao, escala) = ValidarDecimal(tipo, definicao.Precisao, definicao.Escala, erros);
        var opcoes = ValidarOpcoes(tipo, definicao.Opcoes, erros);
        var (entidadeReferenciada, acao) = ValidarReferencia(grupo, entidade, atual, tipo, definicao, erros);

        // TEXTO → TEXTO_LONGO e URL → TEXTO_LONGO: TEXTO_LONGO não pode ser campo de exibição nem estar numa restrição única
        // (RN54, RN55). É uma pré-condição de CONFIGURAÇÃO: quanto aos dados, ir para TEXTO_LONGO é sempre possível.
        if (atual is { TipoDado: TipoDado.Texto or TipoDado.Url } && tipo == TipoDado.TextoLongo)
        {
            if (entidade.CampoExibicaoId == atual.Id)
                erros.Adicionar("tipoDado", "O campo é o campo de exibição, que não pode ser TEXTO_LONGO. Troque o campo de exibição antes.");
            foreach (var restricao in entidade.RestricoesUnicas.Where(r => r.Campos.Any(c => c.Id == atual.Id)))
                erros.Adicionar("tipoDado", $"O campo está na restrição única '{restricao.Nome}', que não aceita TEXTO_LONGO. Remova a restrição antes.");
        }

        if (Contar(erros) > antes)
            return null;

        return new Campo
        {
            Id = atual?.Id ?? Guid.Empty,
            EntidadeId = entidade.Id,
            Chave = chave,
            Nome = nome!,
            TipoDado = tipo,
            Obrigatorio = definicao.Obrigatorio ?? false,
            TamanhoMaximo = tamanho,
            Precisao = precisao,
            Escala = escala,
            Opcoes = opcoes,
            EntidadeReferenciadaId = entidadeReferenciada,
            AcaoExclusao = acao,
            Ordem = definicao.Ordem ?? atual?.Ordem ?? (entidade.Campos.Count == 0 ? 1 : entidade.Campos.Max(c => c.Ordem) + 1),
            Ativo = atual?.Ativo ?? true,
        };
    }

    private static TipoDado ValidarTipo(Campo? atual, string? texto, ErrosPorCampo erros)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            if (atual is null)
                erros.Adicionar("tipoDado", $"Obrigatório: {TiposAceitos()}.");
            return atual?.TipoDado ?? TipoDado.Texto;
        }

        if (!Enum.GetValues<TipoDado>().Any(t => TipoDadoConversor.ParaTexto(t) == texto.Trim().ToUpperInvariant()))
        {
            erros.Adicionar("tipoDado", $"Tipo desconhecido. Use {TiposAceitos()}.");
            return atual?.TipoDado ?? TipoDado.Texto;
        }

        var tipo = TipoDadoConversor.DeTexto(texto.Trim().ToUpperInvariant());

        // RN66: o tipo não muda, salvo as mudanças de MudancaDeTipoPermitida.
        if (atual is not null && tipo != atual.TipoDado && !MudancaDeTipoPermitida(atual.TipoDado, tipo))
        {
            erros.Adicionar("tipoDado",
                $"O tipo de um campo não muda ({TipoDadoConversor.ParaTexto(atual.TipoDado)} → {TipoDadoConversor.ParaTexto(tipo)}): " +
                "crie um campo novo com o tipo certo e desative este. As mudanças aceitas são TEXTO → TEXTO_LONGO, " +
                "TEXTO ou TEXTO_LONGO → URL e URL → TEXTO_LONGO.");
            return atual.TipoDado;
        }

        return tipo;
    }

    /// <summary>
    /// RN66: as únicas mudanças de tipo aceitas. TEXTO → TEXTO_LONGO e URL → TEXTO_LONGO: o TEXTO_LONGO aceita tudo o que o
    /// tipo anterior aceitava. TEXTO ou TEXTO_LONGO → URL: só se todo valor existente for uma URL válida (verificado contra os
    /// dados pelo <see cref="PlanejadorAlteracoes"/>, ADR 0042).
    /// </summary>
    public static bool MudancaDeTipoPermitida(TipoDado de, TipoDado para) => (de, para) switch
    {
        (TipoDado.Texto, TipoDado.TextoLongo) => true,
        (TipoDado.Texto or TipoDado.TextoLongo, TipoDado.Url) => true,
        (TipoDado.Url, TipoDado.TextoLongo) => true,
        _ => false,
    };

    /// <summary>CK_Campo_Texto: TEXTO tem TamanhoMaximo de 1 a 4000; os outros tipos, nenhum.</summary>
    private static int? ValidarTamanho(TipoDado tipo, int? tamanho, ErrosPorCampo erros)
    {
        if (tipo != TipoDado.Texto)
        {
            if (tamanho is not null)
                erros.Adicionar("tamanhoMaximo", "Só campos TEXTO têm tamanhoMaximo.");
            return null;
        }

        if (tamanho is null)
            erros.Adicionar("tamanhoMaximo", $"Obrigatório para TEXTO: de 1 a {TamanhoMaximoTexto}.");
        else if (tamanho is < 1 or > TamanhoMaximoTexto)
            erros.Adicionar("tamanhoMaximo", $"Deve estar entre 1 e {TamanhoMaximoTexto}.");
        return tamanho;
    }

    /// <summary>
    /// CK_Campo_Decimal (V002, RN22): DECIMAL tem precisão de 1 a 38, escala até a precisão e até 10, e no máximo 28 dígitos
    /// na parte inteira, para caber em RegistroValor.ValorNumero decimal(38,10). Os outros tipos não têm.
    /// </summary>
    private static (byte? Precisao, byte? Escala) ValidarDecimal(TipoDado tipo, int? precisao, int? escala, ErrosPorCampo erros)
    {
        if (tipo != TipoDado.Decimal)
        {
            if (precisao is not null)
                erros.Adicionar("precisao", "Só campos DECIMAL têm precisão.");
            if (escala is not null)
                erros.Adicionar("escala", "Só campos DECIMAL têm escala.");
            return (null, null);
        }

        var valido = true;
        if (precisao is null)
        {
            erros.Adicionar("precisao", "Obrigatória para DECIMAL: de 1 a 38.");
            valido = false;
        }
        else if (precisao is < 1 or > 38)
        {
            erros.Adicionar("precisao", "Deve estar entre 1 e 38.");
            valido = false;
        }

        if (escala is null)
        {
            erros.Adicionar("escala", "Obrigatória para DECIMAL: de 0 a 10.");
            valido = false;
        }
        else if (escala is < 0 or > 10)
        {
            erros.Adicionar("escala", "Deve estar entre 0 e 10.");
            valido = false;
        }

        if (valido && escala > precisao)
        {
            erros.Adicionar("escala", "Não pode ser maior que a precisão.");
            valido = false;
        }
        else if (valido && precisao - escala > 28)
        {
            erros.Adicionar("precisao", "A parte inteira (precisão − escala) pode ter no máximo 28 dígitos.");
            valido = false;
        }

        return valido ? ((byte)precisao!.Value, (byte)escala!.Value) : (null, null);
    }

    /// <summary>CK_Campo_Lista e RN53: LISTA tem pelo menos uma opção, sem vazias nem repetidas; os outros tipos, nenhuma.</summary>
    private static IReadOnlyList<string> ValidarOpcoes(TipoDado tipo, IReadOnlyList<string?>? opcoes, ErrosPorCampo erros)
    {
        if (tipo != TipoDado.Lista)
        {
            if (opcoes is { Count: > 0 })
                erros.Adicionar("opcoes", "Só campos LISTA têm opções.");
            return [];
        }

        if (opcoes is null || opcoes.Count == 0)
        {
            erros.Adicionar("opcoes", "Informe pelo menos uma opção.");
            return [];
        }

        var normalizadas = new List<string>();
        for (var i = 0; i < opcoes.Count; i++)
        {
            var opcao = opcoes[i]?.Trim();
            if (string.IsNullOrEmpty(opcao))
                erros.Adicionar($"opcoes[{i}]", "Opção vazia.");
            else if (normalizadas.Contains(opcao, StringComparer.Ordinal))
                erros.Adicionar($"opcoes[{i}]", $"A opção '{opcao}' está repetida.");
            else
                normalizadas.Add(opcao);
        }

        return normalizadas;
    }

    /// <summary>
    /// CK_Campo_Referencia e RN54: REFERENCIA aponta para uma entidade ATIVA do mesmo grupo e tem ação de exclusão.
    /// Uma entidade global só referencia globais. A entidade referenciada não muda (RN51).
    /// </summary>
    private static (Guid? EntidadeReferenciada, AcaoExclusao? Acao) ValidarReferencia(
        Grupo grupo, Entidade entidade, Campo? atual, TipoDado tipo, DefinicaoCampo definicao, ErrosPorCampo erros)
    {
        if (tipo != TipoDado.Referencia)
        {
            if (!string.IsNullOrWhiteSpace(definicao.EntidadeReferenciada))
                erros.Adicionar("entidadeReferenciada", "Só campos REFERENCIA apontam para outra entidade.");
            if (!string.IsNullOrWhiteSpace(definicao.AcaoExclusao))
                erros.Adicionar("acaoExclusao", "Só campos REFERENCIA têm ação de exclusão.");
            return (null, null);
        }

        Guid? referenciada = null;
        if (atual is not null)
        {
            referenciada = atual.EntidadeReferenciadaId;
            var chaveAtual = grupo.ObterEntidade(referenciada!.Value)?.Chave;
            if (!string.IsNullOrWhiteSpace(definicao.EntidadeReferenciada)
                && !string.Equals(definicao.EntidadeReferenciada.Trim(), chaveAtual, StringComparison.OrdinalIgnoreCase))
            {
                erros.Adicionar("entidadeReferenciada",
                    $"A entidade referenciada não muda ('{chaveAtual}'): crie um campo novo e desative este.");
            }
        }
        else if (string.IsNullOrWhiteSpace(definicao.EntidadeReferenciada))
        {
            erros.Adicionar("entidadeReferenciada", "Obrigatória para REFERENCIA: a chave de uma entidade do mesmo grupo.");
        }
        else if (grupo.ObterEntidade(definicao.EntidadeReferenciada.Trim()) is not { } destino)
        {
            erros.Adicionar("entidadeReferenciada", $"A entidade '{definicao.EntidadeReferenciada.Trim()}' não existe no grupo '{grupo.Chave}'.");
        }
        else if (!destino.Ativo)
        {
            erros.Adicionar("entidadeReferenciada", $"A entidade '{destino.Chave}' está desativada.");
        }
        else if (entidade.EhGlobal && !destino.EhGlobal)
        {
            erros.Adicionar("entidadeReferenciada",
                $"'{entidade.Chave}' é global e '{destino.Chave}' é por equipe: um registro global não pode apontar para registros de equipe.");
        }
        else
        {
            referenciada = destino.Id;
        }

        AcaoExclusao? acao = atual?.AcaoExclusao;
        if (!string.IsNullOrWhiteSpace(definicao.AcaoExclusao))
        {
            acao = definicao.AcaoExclusao.Trim().ToUpperInvariant() switch
            {
                "RESTRINGIR" => AcaoExclusao.Restringir,
                "CASCATA" => AcaoExclusao.Cascata,
                _ => null,
            };
            if (acao is null)
                erros.Adicionar("acaoExclusao", "Use RESTRINGIR ou CASCATA.");
        }
        else if (acao is null)
        {
            erros.Adicionar("acaoExclusao", "Obrigatória para REFERENCIA: RESTRINGIR ou CASCATA.");
        }

        return (referenciada, acao);
    }

    /// <summary>
    /// RN60 e RN61: o valor padrão só existe para preencher registros num campo que passa a ser obrigatório (campo novo
    /// obrigatório, ou campo que vira obrigatório). Não vale para REFERENCIA (cada equipe aponta para os seus registros,
    /// RN29) nem para campo de restrição única (todos os registros ficariam com o mesmo valor). O TIPO do valor é
    /// conferido pelo caso de uso, com o mesmo validador dos registros (RN04).
    /// </summary>
    public static void ValidarValorPadrao(Entidade entidade, Campo? atual, Campo novo, bool temValorPadrao, ErrosPorCampo erros)
    {
        if (!temValorPadrao)
            return;

        var passaASerObrigatorio = novo.Obrigatorio && (atual is null || !atual.Obrigatorio);
        if (!passaASerObrigatorio)
            erros.Adicionar("valorPadrao", "Só vale ao criar um campo obrigatório ou ao tornar um campo obrigatório.");
        else if (novo.EhReferencia)
            erros.Adicionar("valorPadrao", "REFERENCIA não aceita valor padrão: crie o campo opcional, preencha os registros e depois torne-o obrigatório.");
        else if (atual is not null && entidade.RestricoesUnicas.Any(r => r.Campos.Any(c => c.Id == atual.Id)))
            erros.Adicionar("valorPadrao", "O campo está numa restrição única: um mesmo valor em vários registros os deixaria duplicados.");
    }

    // ---- Ordem dos campos ----

    /// <summary>RN53: a nova ordem lista TODOS os campos da entidade (ativos e inativos), cada um uma vez.</summary>
    public static IReadOnlyList<Campo>? ValidarOrdem(Entidade entidade, IReadOnlyList<string?>? chaves, ErrosPorCampo erros)
    {
        var antes = Contar(erros);
        var lista = chaves ?? [];
        var ordenados = new List<Campo>();

        for (var i = 0; i < lista.Count; i++)
        {
            var campo = entidade.ObterCampo(lista[i]?.Trim() ?? "");
            if (campo is null)
                erros.Adicionar($"campos[{i}]", $"Campo '{lista[i]}' não existe em '{entidade.Chave}'.");
            else if (ordenados.Contains(campo))
                erros.Adicionar($"campos[{i}]", $"O campo '{campo.Chave}' aparece mais de uma vez.");
            else
                ordenados.Add(campo);
        }

        var faltando = entidade.Campos.Where(c => !ordenados.Contains(c)).Select(c => c.Chave).ToList();
        if (faltando.Count > 0)
            erros.Adicionar("campos", $"Informe todos os campos, inclusive os inativos. Faltam: {string.Join(", ", faltando)}.");

        return Contar(erros) > antes ? null : ordenados;
    }

    // ---- Restrição única ----

    /// <summary>
    /// RN55: restrição com nome no padrão das chaves, único na entidade, e de 1 a 5 campos ATIVOS da entidade, sem
    /// TEXTO_LONGO nem repetidos. Os campos saem na ordem do hash (Campo.Ordem, depois Chave), como no MetadadosProvider.
    /// </summary>
    public static RestricaoDefinida? ValidarRestricao(Entidade entidade, DefinicaoRestricao definicao, ErrosPorCampo erros)
    {
        var antes = Contar(erros);
        var nome = ValidarChaveNova(definicao.Nome, entidade.RestricoesUnicas.Select(r => r.Nome), "nome", "restrição", erros);

        var chaves = definicao.Campos ?? [];
        if (chaves.Count is < 1 or > MaximoCamposRestricao)
            erros.Adicionar("campos", $"Informe de 1 a {MaximoCamposRestricao} campos.");

        var campos = new List<Campo>();
        for (var i = 0; i < chaves.Count; i++)
        {
            var campo = entidade.ObterCampo(chaves[i]?.Trim() ?? "");
            string? erro = campo switch
            {
                null => $"Campo '{chaves[i]}' não existe em '{entidade.Chave}'.",
                { Ativo: false } => $"O campo '{campo.Chave}' está inativo.",
                { TipoDado: TipoDado.TextoLongo } => $"O campo '{campo.Chave}' é TEXTO_LONGO, que não entra em restrição única.",
                _ when campos.Contains(campo) => $"O campo '{campo.Chave}' aparece mais de uma vez.",
                _ => null,
            };

            if (erro is null)
                campos.Add(campo!);
            else
                erros.Adicionar($"campos[{i}]", erro);
        }

        return Contar(erros) > antes
            ? null
            : new RestricaoDefinida(nome, campos.OrderBy(c => c.Ordem).ThenBy(c => c.Chave, StringComparer.Ordinal).ToList());
    }

    // ---- Dependência ----

    /// <summary>
    /// RN55, com a semântica da RN08 ("em Campo só vale escolher registros cujo CampoAlvo seja igual ao CampoOrigem"):
    ///   - o campo dependente é REFERENCIA;
    ///   - o via (opcional) é REFERENCIA da mesma entidade;
    ///   - a origem é da entidade apontada pelo via, ou da própria entidade sem via;
    ///   - o alvo é da entidade referenciada pelo campo dependente;
    ///   - origem e alvo são REFERENCIA para a MESMA entidade;
    ///   - todos ativos, e sem ciclos entre as dependências do grupo.
    /// </summary>
    public static CampoDependencia? ValidarDependencia(Grupo grupo, Entidade entidade, Campo campo, DefinicaoDependencia definicao, ErrosPorCampo erros)
    {
        var antes = Contar(erros);

        if (!campo.EhReferencia)
            erros.Adicionar("campo", $"'{campo.Chave}' é {TipoDadoConversor.ParaTexto(campo.TipoDado)}: só campos REFERENCIA têm dependência.");
        if (!campo.Ativo)
            erros.Adicionar("campo", $"O campo '{campo.Chave}' está inativo.");

        Campo? via = null;
        if (!string.IsNullOrWhiteSpace(definicao.CampoVia))
        {
            via = entidade.ObterCampo(definicao.CampoVia.Trim());
            if (via is null)
                erros.Adicionar("campoVia", $"Campo '{definicao.CampoVia.Trim()}' não existe em '{entidade.Chave}'.");
            else if (!via.EhReferencia)
                erros.Adicionar("campoVia", $"O via '{via.Chave}' precisa ser REFERENCIA.");
            else if (via.Id == campo.Id)
                erros.Adicionar("campoVia", "O via não pode ser o próprio campo dependente.");
            else if (!via.Ativo)
                erros.Adicionar("campoVia", $"O campo '{via.Chave}' está inativo.");
        }

        // A origem é lida no registro apontado pelo via ou, sem via, no próprio registro.
        var entidadeOrigem = via is { EhReferencia: true } ? grupo.ObterEntidade(via.EntidadeReferenciadaId!.Value) : entidade;
        var origem = ResolverCampo(entidadeOrigem, definicao.CampoOrigem, "campoOrigem", erros);
        if (origem is not null && origem.Id == campo.Id)
            erros.Adicionar("campoOrigem", "A origem não pode ser o próprio campo dependente.");

        // O alvo é lido no registro escolhido no campo dependente.
        var entidadeAlvo = campo.EhReferencia ? grupo.ObterEntidade(campo.EntidadeReferenciadaId!.Value) : null;
        var alvo = entidadeAlvo is null ? null : ResolverCampo(entidadeAlvo, definicao.CampoAlvo, "campoAlvo", erros);

        if (origem is not null && alvo is not null)
        {
            if (!origem.EhReferencia || !alvo.EhReferencia)
                erros.Adicionar("campoAlvo", "Origem e alvo precisam ser REFERENCIA (comparam o registro apontado por cada um).");
            else if (origem.EntidadeReferenciadaId != alvo.EntidadeReferenciadaId)
                erros.Adicionar("campoAlvo", $"Origem ('{origem.Chave}') e alvo ('{alvo.Chave}') precisam apontar para a mesma entidade.");
        }

        if (Contar(erros) > antes)
            return null;

        var dependencia = new CampoDependencia { Campo = campo, CampoVia = via, CampoOrigem = origem!, CampoAlvo = alvo! };
        if (TemCiclo(grupo, dependencia))
        {
            erros.Adicionar("campoOrigem",
                $"A dependência criaria um ciclo: o valor de '{campo.Chave}' passaria a depender, por outras dependências, dele mesmo.");
            return null;
        }

        return dependencia;
    }

    private static Campo? ResolverCampo(Entidade? entidade, string? chave, string chaveErro, ErrosPorCampo erros)
    {
        if (entidade is null)
            return null;

        if (string.IsNullOrWhiteSpace(chave))
        {
            erros.Adicionar(chaveErro, $"Obrigatório: a chave de um campo de '{entidade.Chave}'.");
            return null;
        }

        var campo = entidade.ObterCampo(chave.Trim());
        if (campo is null)
            erros.Adicionar(chaveErro, $"Campo '{chave.Trim()}' não existe em '{entidade.Chave}'.");
        else if (!campo.Ativo)
            erros.Adicionar(chaveErro, $"O campo '{entidade.Chave}.{campo.Chave}' está inativo.");
        return campo is { Ativo: true } ? campo : null;
    }

    /// <summary>
    /// Ciclo entre dependências: o valor de um campo dependente depende do via e da origem dele, que podem ter, por sua
    /// vez, dependências. Há ciclo se, seguindo essas ligações a partir do campo, se volta a ele.
    /// </summary>
    public static bool TemCiclo(Grupo grupo, CampoDependencia nova)
    {
        var dependencias = grupo.Entidades
            .SelectMany(e => e.Dependencias)
            .Where(d => d.Campo.Id != nova.Campo.Id)
            .Append(nova)
            .ToDictionary(d => d.Campo.Id);

        var visitados = new HashSet<Guid>();
        var pilha = new Stack<Guid>(Ligacoes(nova));
        while (pilha.Count > 0)
        {
            var atual = pilha.Pop();
            if (atual == nova.Campo.Id)
                return true;
            if (!visitados.Add(atual) || !dependencias.TryGetValue(atual, out var dependencia))
                continue;
            foreach (var proximo in Ligacoes(dependencia))
                pilha.Push(proximo);
        }

        return false;

        static IEnumerable<Guid> Ligacoes(CampoDependencia d) =>
            d.CampoVia is null ? [d.CampoOrigem.Id] : [d.CampoVia.Id, d.CampoOrigem.Id];
    }

    // ---- Usos (para desativar e excluir, RN69, RN70, RN76) ----

    /// <summary>
    /// Onde o campo é usado: campo de exibição, restrição única e dependências (como campo dependente, via, origem ou
    /// alvo, em qualquer entidade do grupo). <paramref name="soDependenciasAtivas"/>: ignora dependências de campos inativos.
    /// </summary>
    public static IReadOnlyList<string> UsosDoCampo(Grupo grupo, Entidade entidade, Campo campo, bool soDependenciasAtivas)
    {
        var usos = new List<string>();
        if (entidade.CampoExibicaoId == campo.Id)
            usos.Add($"campo de exibição de '{entidade.Chave}'");

        usos.AddRange(entidade.RestricoesUnicas
            .Where(r => r.Campos.Any(c => c.Id == campo.Id))
            .Select(r => $"restrição única '{r.Nome}'"));

        foreach (var dependencia in grupo.Entidades.SelectMany(e => e.Dependencias))
        {
            if (soDependenciasAtivas && !dependencia.Campo.Ativo)
                continue;

            var papel = dependencia.Campo.Id == campo.Id ? "campo dependente"
                : dependencia.CampoVia?.Id == campo.Id ? "via"
                : dependencia.CampoOrigem.Id == campo.Id ? "origem"
                : dependencia.CampoAlvo.Id == campo.Id ? "alvo"
                : null;
            if (papel is not null)
            {
                var dono = grupo.ObterEntidade(dependencia.Campo.EntidadeId)?.Chave;
                usos.Add($"dependência de '{dono}.{dependencia.Campo.Chave}' (como {papel})");
            }
        }

        return usos;
    }

    /// <summary>Campos de OUTRAS entidades que apontam para esta (RN76: não se exclui entidade referenciada).</summary>
    public static IReadOnlyList<string> ReferenciasParaEntidade(Grupo grupo, Entidade entidade) =>
        grupo.Entidades
            .Where(e => e.Id != entidade.Id)
            .SelectMany(e => e.Campos.Where(c => c.EntidadeReferenciadaId == entidade.Id).Select(c => $"campo '{e.Chave}.{c.Chave}'"))
            .ToList();

    // ---- Apoio ----

    /// <summary>
    /// RN53: chave nova no padrão ^[a-z0-9_]+$, até 100 caracteres, única no escopo (sem diferenciar maiúsculas, como a
    /// collation do banco). Campo: também não pode ser chave reservada (RN14) nem "ordem" (rota).
    /// </summary>
    private static string ValidarChaveNova(
        string? texto, IEnumerable<string> existentes, string chaveErro, string oQue, ErrosPorCampo erros, bool ehCampo = false)
    {
        var chave = texto?.Trim() ?? "";
        if (chave.Length == 0)
            erros.Adicionar(chaveErro, "Obrigatória.");
        else if (chave.Length > TamanhoMaximoChave)
            erros.Adicionar(chaveErro, $"Deve ter no máximo {TamanhoMaximoChave} caracteres.");
        else if (!PadraoChave().IsMatch(chave))
            erros.Adicionar(chaveErro, "Use só letras minúsculas sem acento, números e \"_\" (ex.: data_aquisicao).");
        else if (existentes.Contains(chave, StringComparer.OrdinalIgnoreCase))
            erros.Adicionar(chaveErro, $"Já existe {oQue} com a chave '{chave}'.");
        else if (ehCampo && ChavesReservadas.EhReservada(chave))
            erros.Adicionar(chaveErro, $"'{chave}' é chave reservada (RN14): escolha outra.");
        else if (ehCampo && chave == ChaveDeRota)
            erros.Adicionar(chaveErro, $"'{ChaveDeRota}' é usada na rota de reordenação dos campos: escolha outra.");
        return chave;
    }

    /// <summary>RN51: a chave não muda. Ausente, vale a atual; diferente, é erro.</summary>
    private static string ConferirImutavel(string? enviada, string atual, string chaveErro, ErrosPorCampo erros)
    {
        if (!string.IsNullOrWhiteSpace(enviada) && enviada.Trim() != atual)
        {
            erros.Adicionar(chaveErro,
                $"A chave não muda ('{atual}'): ela faz parte das URLs, do JSON dos registros e do contrato com o front. Mude o nome.");
        }

        return atual;
    }

    private static string? ValidarNome(string? texto, ErrosPorCampo erros)
    {
        var nome = texto?.Trim();
        if (string.IsNullOrEmpty(nome))
            erros.Adicionar("nome", "Obrigatório.");
        else if (nome.Length > TamanhoMaximoNome)
            erros.Adicionar("nome", $"Deve ter no máximo {TamanhoMaximoNome} caracteres.");
        return nome;
    }

    private static string? ValidarDescricao(string? texto, ErrosPorCampo erros)
    {
        var descricao = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (descricao?.Length > TamanhoMaximoDescricao)
            erros.Adicionar("descricao", $"Deve ter no máximo {TamanhoMaximoDescricao} caracteres.");
        return descricao;
    }

    private static string TiposAceitos() => string.Join(", ", Enum.GetValues<TipoDado>().Select(TipoDadoConversor.ParaTexto));

    private static int Contar(ErrosPorCampo erros) => erros.ParaDicionario().Sum(e => e.Value.Length);

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex PadraoChave();

    [GeneratedRegex("^pi-[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex PadraoIcone();
}
