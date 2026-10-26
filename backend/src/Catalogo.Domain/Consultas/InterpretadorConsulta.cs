using System.Globalization;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Consultas;

/// <summary>
/// Interpreta os parâmetros da URL da listagem (RN17 a RN19) e devolve uma
/// <see cref="ConsultaRegistros"/> tipada. Todos os erros voltam juntos numa
/// <see cref="ValidacaoRegistroException"/> (400), com a chave = nome do parâmetro.
///
/// Parâmetros:
///   pagina, tamanho          → paginação (padrão 1 e 20; tamanho máximo 100)
///   ordenacao=[-]chave       → campo ou criadoEm/atualizadoEm; "-" = decrescente (padrão -criadoEm). REFERENCIA ordena
///                              pelo campo de exibição do registro apontado, sem referência por último (RN19)
///   chave=valor              → igual (todos os tipos)
///   chave.contem=valor       → TEXTO, TEXTO_LONGO
///   chave.de=valor, chave.ate=valor → INTEIRO, DECIMAL, DATA, DATA_HORA (inclusivos)
///   referencia.chave..., referencia.referencia.chave... → o mesmo, num campo do registro APONTADO (até 2 saltos, RN89)
///   equipeId=guid            → só registros dessa equipe (entidades por equipe, RN27)
///   situacaoAprovacao=...    → PENDENTE, APROVADO ou REJEITADO (RN37)
///   avaliacao.criterio.de=4, avaliacao.criterio.ate=4 → média de um critério ESTRELAS (RN48)
///   ordenacao=[-]avaliacao.criterio → pela média do critério, itens sem nota por último (RN48)
/// </summary>
public static class InterpretadorConsulta
{
    public const string OperadorContem = "contem";
    public const string OperadorDe = "de";
    public const string OperadorAte = "ate";

    /// <summary>Prefixo de ordenação e filtro pela média de um critério de avaliação.</summary>
    public const string PrefixoAvaliacao = ChavesReservadas.Avaliacao + ".";

    /// <param name="criteriosAvaliacao">
    /// Chaves dos critérios ESTRELAS ativos nos modelos de avaliação ativos da entidade (nas equipes que o
    /// usuário vê). Só elas são aceitas em "avaliacao.&lt;criterio&gt;". Nulo ou vazio = nenhum.
    /// </param>
    /// <param name="grupo">O grupo da entidade: de onde vêm as entidades apontadas nos filtros por caminho (RN89). Nulo = só campos diretos.</param>
    public static ConsultaRegistros Interpretar(
        Entidade entidade, IEnumerable<KeyValuePair<string, string>> parametros, IReadOnlySet<string>? criteriosAvaliacao = null,
        Grupo? grupo = null)
    {
        var lista = parametros.ToList();
        var erros = new ErrosPorCampo();
        criteriosAvaliacao ??= new HashSet<string>();

        var paginacao = InterpretarPaginacao(lista, erros);
        var ordenacao = InterpretarOrdenacao(grupo, entidade, lista, criteriosAvaliacao, erros);
        var condicoes = new List<CondicaoFiltro>();
        var condicoesAvaliacao = new List<CondicaoAvaliacao>();
        var filtroEquipe = InterpretarEquipe(entidade, lista, erros);
        var filtroSituacao = InterpretarSituacao(lista, erros);

        foreach (var (chave, valor) in lista)
        {
            // "equipeId" é filtro de sistema, já lido acima (RN27).
            if (string.Equals(chave, ChavesReservadas.EquipeId, StringComparison.Ordinal)
                || string.Equals(chave, ChavesReservadas.SituacaoAprovacao, StringComparison.Ordinal))
            {
                continue;
            }

            if (chave.StartsWith(PrefixoAvaliacao, StringComparison.Ordinal))
            {
                if (InterpretarFiltroAvaliacao(chave, valor, criteriosAvaliacao, erros) is { } condicaoAvaliacao)
                    condicoesAvaliacao.Add(condicaoAvaliacao);
                continue;
            }

            if (ChavesReservadas.DaConsulta.Contains(chave))
            {
                if (chave is ChavesReservadas.Busca or ChavesReservadas.DependeDe)
                    erros.Adicionar(chave, "Parâmetro válido só no endpoint de opções de campo.");
                else if (chave is ChavesReservadas.Formato)
                    erros.Adicionar(chave, "Parâmetro válido só na exportação (.../registros/exportar).");
                else if (chave is ChavesReservadas.AgruparPor or ChavesReservadas.Metricas or ChavesReservadas.OrdenarPor
                         or ChavesReservadas.Limite or ChavesReservadas.De or ChavesReservadas.Ate)
                    erros.Adicionar(chave, "Parâmetro válido só no painel (/api/v1/painel/...).");
                continue;
            }

            condicoes.AddRange(InterpretarFiltro(grupo, entidade, chave, valor, erros));
        }

        erros.LancarSeHouverErros();
        return new ConsultaRegistros(condicoes, ordenacao, paginacao, filtroEquipe, FiltroSituacao: filtroSituacao,
            CondicoesAvaliacao: condicoesAvaliacao);
    }

    /// <summary>"avaliacao.&lt;criterio&gt;.de=4" ou ".ate=4": média de 1 a 5, com ponto decimal (RN48).</summary>
    private static CondicaoAvaliacao? InterpretarFiltroAvaliacao(
        string parametro, string valor, IReadOnlySet<string> criteriosAvaliacao, ErrosPorCampo erros)
    {
        var resto = parametro[PrefixoAvaliacao.Length..];
        var ponto = resto.LastIndexOf('.');
        var criterio = ponto < 0 ? resto : resto[..ponto];
        var operador = ponto < 0 ? null : resto[(ponto + 1)..];

        if (!criteriosAvaliacao.Contains(criterio))
        {
            erros.Adicionar(parametro, MensagemCriterioDesconhecido(criterio, criteriosAvaliacao));
            return null;
        }

        var comparacao = operador switch
        {
            OperadorDe => Comparacao.MaiorOuIgual,
            OperadorAte => Comparacao.MenorOuIgual,
            _ => (Comparacao?)null,
        };
        if (comparacao is null)
        {
            erros.Adicionar(parametro, $"Use avaliacao.{criterio}.de=valor ou avaliacao.{criterio}.ate=valor.");
            return null;
        }

        if (!decimal.TryParse(valor.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var media)
            || media is < 1 or > 5)
        {
            erros.Adicionar(parametro, "Deve ser uma média de 1 a 5, com ponto decimal (ex.: 4 ou 3.5).");
            return null;
        }

        // O filtro vale para a média como a API MOSTRA (uma casa, arredondada): "de=4" inclui a média 3.96
        // (exibida 4.0). Com arredondamento "metade para cima", isso vira um limite sobre a média exata:
        //   exibida >= v  ⇔  média >= v' - 0.05, com v' = v arredondado PARA CIMA numa casa;
        //   exibida <= v  ⇔  média <  v'' + 0.05, com v'' = v arredondado PARA BAIXO numa casa.
        return comparacao == Comparacao.MaiorOuIgual
            ? new CondicaoAvaliacao(criterio, Comparacao.MaiorOuIgual, Math.Ceiling(media * 10) / 10 - 0.05m)
            : new CondicaoAvaliacao(criterio, Comparacao.Menor, Math.Floor(media * 10) / 10 + 0.05m);
    }

    private static string MensagemCriterioDesconhecido(string criterio, IReadOnlySet<string> criteriosAvaliacao) =>
        criteriosAvaliacao.Count == 0
            ? "Esta entidade não tem modelo de avaliação ativo com critérios de estrelas."
            : $"Critério de avaliação '{criterio}' desconhecido. Use um critério de estrelas ativo: {string.Join(", ", criteriosAvaliacao.Order(StringComparer.Ordinal))}.";

    /// <summary>"situacaoAprovacao=PENDENTE|APROVADO|REJEITADO" (RN37).</summary>
    private static SituacaoAprovacao? InterpretarSituacao(IReadOnlyList<KeyValuePair<string, string>> parametros, ErrosPorCampo erros)
    {
        var texto = LerUnico(parametros, ChavesReservadas.SituacaoAprovacao, erros);
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        if (AprovacaoConversor.TentarConverterSituacao(texto, out var situacao))
            return situacao;

        erros.Adicionar(ChavesReservadas.SituacaoAprovacao, "Deve ser PENDENTE, APROVADO ou REJEITADO.");
        return null;
    }

    /// <summary>"equipeId=&lt;guid&gt;": só registros dessa equipe. Não existe em entidades globais.</summary>
    private static Guid? InterpretarEquipe(Entidade entidade, IReadOnlyList<KeyValuePair<string, string>> parametros, ErrosPorCampo erros)
    {
        var texto = LerUnico(parametros, ChavesReservadas.EquipeId, erros)?.Trim();
        if (string.IsNullOrEmpty(texto))
            return null;

        if (entidade.EhGlobal)
        {
            erros.Adicionar(ChavesReservadas.EquipeId, $"'{entidade.Nome}' é global: os registros não têm equipe.");
            return null;
        }

        if (!Guid.TryParseExact(texto, "D", out var equipeId))
        {
            erros.Adicionar(ChavesReservadas.EquipeId, "Deve ser o id (GUID) de uma equipe.");
            return null;
        }

        return equipeId;
    }

    /// <summary>Lê "pagina" e "tamanho". Usado também pelo endpoint de opções.</summary>
    public static Paginacao InterpretarPaginacao(IReadOnlyList<KeyValuePair<string, string>> parametros, ErrosPorCampo erros)
    {
        var pagina = LerInteiro(parametros, ChavesReservadas.Pagina, 1, minimo: 1, maximo: int.MaxValue, erros);
        var tamanho = LerInteiro(parametros, ChavesReservadas.Tamanho, Paginacao.TamanhoPadrao, minimo: 1, maximo: Paginacao.TamanhoMaximo, erros);
        return new Paginacao(pagina, tamanho);
    }

    /// <summary>Valor único de um parâmetro (nulo se ausente). Repetido é erro.</summary>
    public static string? LerUnico(IReadOnlyList<KeyValuePair<string, string>> parametros, string nome, ErrosPorCampo erros)
    {
        var valores = parametros.Where(p => p.Key == nome).Select(p => p.Value).ToList();
        if (valores.Count > 1)
            erros.Adicionar(nome, "Informe este parâmetro uma única vez.");

        return valores.Count == 0 ? null : valores[0];
    }

    private static int LerInteiro(
        IReadOnlyList<KeyValuePair<string, string>> parametros, string nome, int padrao, int minimo, int maximo, ErrosPorCampo erros)
    {
        var texto = LerUnico(parametros, nome, erros);
        if (texto is null)
            return padrao;

        if (!int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) || numero < minimo)
        {
            erros.Adicionar(nome, $"Deve ser um número inteiro maior ou igual a {minimo}.");
            return padrao;
        }

        if (numero > maximo)
        {
            erros.Adicionar(nome, $"Deve ser no máximo {maximo}.");
            return padrao;
        }

        return numero;
    }

    private static Ordenacao InterpretarOrdenacao(
        Grupo? grupo,
        Entidade entidade, IReadOnlyList<KeyValuePair<string, string>> parametros, IReadOnlySet<string> criteriosAvaliacao, ErrosPorCampo erros)
    {
        var texto = LerUnico(parametros, ChavesReservadas.Ordenacao, erros)?.Trim();
        if (string.IsNullOrEmpty(texto))
            return Ordenacao.Padrao;

        var descendente = texto.StartsWith('-');
        var chave = descendente ? texto[1..] : texto;

        if (chave == ChavesReservadas.CriadoEm)
            return new Ordenacao(null, CampoSistema.CriadoEm, descendente);
        if (chave == ChavesReservadas.AtualizadoEm)
            return new Ordenacao(null, CampoSistema.AtualizadoEm, descendente);

        // RN48: pela média de um critério de avaliação.
        if (chave.StartsWith(PrefixoAvaliacao, StringComparison.Ordinal))
        {
            var criterio = chave[PrefixoAvaliacao.Length..];
            if (criteriosAvaliacao.Contains(criterio))
                return new Ordenacao(null, null, descendente, criterio);

            erros.Adicionar(ChavesReservadas.Ordenacao, MensagemCriterioDesconhecido(criterio, criteriosAvaliacao));
            return Ordenacao.Padrao;
        }

        var campo = entidade.ObterCampo(chave);
        if (campo is null || !campo.Ativo)
        {
            erros.Adicionar(ChavesReservadas.Ordenacao, $"Campo '{chave}' desconhecido em '{entidade.Nome}'. Use a chave de um campo, criadoEm ou atualizadoEm.");
            return Ordenacao.Padrao;
        }

        if (campo.TipoDado is TipoDado.TextoLongo)
        {
            erros.Adicionar(ChavesReservadas.Ordenacao, $"Não é possível ordenar por campos {TipoDadoConversor.ParaTexto(campo.TipoDado)}.");
            return Ordenacao.Padrao;
        }

        // RN19: REFERENCIA ordena pelo campo de exibição do registro apontado (o rótulo), com o tipo dele.
        if (campo.EhReferencia)
        {
            var apontada = campo.EntidadeReferenciadaId is Guid id ? grupo?.ObterEntidade(id) : null;
            var exibicao = apontada?.CampoExibicaoId is Guid exibicaoId ? apontada.ObterCampo(exibicaoId) : null;
            if (exibicao is null || !exibicao.Ativo)
            {
                erros.Adicionar(ChavesReservadas.Ordenacao,
                    $"Não é possível ordenar por '{campo.Chave}': a entidade apontada não tem campo de exibição.");
                return Ordenacao.Padrao;
            }

            return new Ordenacao(campo, null, descendente, CampoExibicaoReferenciada: exibicao);
        }

        return new Ordenacao(campo, null, descendente);
    }

    private static IEnumerable<CondicaoFiltro> InterpretarFiltro(Grupo? grupo, Entidade entidade, string parametro, string valor, ErrosPorCampo erros)
    {
        if (ChavesReservadas.DoRegistro.Contains(parametro.Split('.')[0]))
        {
            erros.Adicionar(parametro, "Chave reservada: não pode ser usada como filtro.");
            return [];
        }

        // "nome_servico.contem" → campo "nome_servico" + operador "contem"; "metodo" → igual;
        // "relatorio_teste.data_execucao.de" → o campo data_execucao do relatório apontado (RN89) + operador "de".
        var (caminho, operador, erroCaminho) = SepararOperador(grupo, entidade, parametro);
        if (caminho is null)
        {
            erros.Adicionar(parametro, erroCaminho!);
            return [];
        }

        var campo = caminho.Campo;

        var comparacao = operador switch
        {
            null => Comparacao.Igual,
            OperadorContem => Comparacao.Contem,
            OperadorDe => Comparacao.MaiorOuIgual,
            OperadorAte => Comparacao.MenorOuIgual,
            _ => (Comparacao?)null,
        };

        if (comparacao is null)
        {
            erros.Adicionar(parametro, $"Operador '{operador}' desconhecido. Use: {OperadoresPermitidos(campo.TipoDado)}.");
            return [];
        }

        if (!OperadorPermitido(campo.TipoDado, comparacao.Value))
        {
            erros.Adicionar(parametro,
                $"Operador '{operador}' não é aceito para campos {TipoDadoConversor.ParaTexto(campo.TipoDado)}. Use: {OperadoresPermitidos(campo.TipoDado)}.");
            return [];
        }

        var condicoes = Converter(campo, comparacao.Value, valor, out var erro);
        if (erro is not null)
        {
            erros.Adicionar(parametro, erro);
            return [];
        }

        return caminho.Saltos.Count == 0 ? condicoes : condicoes.Select(c => c with { Saltos = caminho.Saltos }).ToList();
    }

    private static readonly IReadOnlySet<string> Operadores = new HashSet<string>([OperadorContem, OperadorDe, OperadorAte], StringComparer.Ordinal);

    /// <summary>
    /// Se a chave inteira é um caminho até um campo, é igualdade (protege chaves que tenham ponto). Senão, o último trecho é o
    /// operador. Quando nenhuma das duas leituras vale, o erro é o da mais provável:
    ///   - último trecho é um operador conhecido ("relatorio.nao_existe.de"): o erro do caminho sem ele;
    ///   - antes dele vem uma REFERENCIA ("relatorio.nao_existe"): quem escreveu estava percorrendo um caminho; o erro do caminho inteiro.
    /// </summary>
    private static (CaminhoCampo? Caminho, string? Operador, string? Erro) SepararOperador(Grupo? grupo, Entidade entidade, string parametro)
    {
        var inteiro = CaminhoCampo.Resolver(grupo, entidade, parametro, out var erroInteiro);
        var ponto = parametro.LastIndexOf('.');
        if (inteiro is not null || ponto < 0)
            return (inteiro, null, erroInteiro);

        var operador = parametro[(ponto + 1)..];
        var semOperador = CaminhoCampo.Resolver(grupo, entidade, parametro[..ponto], out var erroSemOperador);
        if (Operadores.Contains(operador))
            return (semOperador, operador, erroSemOperador);

        return semOperador is { Campo.EhReferencia: false } ? (semOperador, operador, null) : (null, null, erroInteiro);
    }

    private static bool OperadorPermitido(TipoDado tipo, Comparacao comparacao) => comparacao switch
    {
        Comparacao.Igual => true,
        Comparacao.Contem => tipo is TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Url,
        Comparacao.MaiorOuIgual or Comparacao.MenorOuIgual =>
            tipo is TipoDado.Inteiro or TipoDado.Decimal or TipoDado.Data or TipoDado.DataHora,
        _ => false,
    };

    private static string OperadoresPermitidos(TipoDado tipo) => tipo switch
    {
        TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Url => "chave=valor (igual) ou chave.contem=valor",
        TipoDado.Inteiro or TipoDado.Decimal or TipoDado.Data or TipoDado.DataHora => "chave=valor (igual), chave.de=valor ou chave.ate=valor",
        _ => "chave=valor (igual)",
    };

    /// <summary>Converte o texto da URL para o tipo do campo. Devolve as condições ou preenche <paramref name="erro"/>.</summary>
    private static IReadOnlyList<CondicaoFiltro> Converter(Campo campo, Comparacao comparacao, string valor, out string? erro)
    {
        erro = null;
        var texto = valor.Trim();
        if (texto.Length == 0)
        {
            erro = "Informe um valor para o filtro.";
            return [];
        }

        switch (campo.TipoDado)
        {
            case TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Url:
                return [new CondicaoFiltro(campo, comparacao, Texto: texto)];

            case TipoDado.Lista:
                if (!campo.Opcoes.Contains(texto, StringComparer.Ordinal))
                {
                    erro = $"Deve ser uma das opções: {string.Join(", ", campo.Opcoes)}.";
                    return [];
                }
                return [new CondicaoFiltro(campo, comparacao, Texto: texto)];

            case TipoDado.Inteiro:
                if (!int.TryParse(texto, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var inteiro))
                {
                    erro = "Deve ser um número inteiro (ex.: 42).";
                    return [];
                }
                return [new CondicaoFiltro(campo, comparacao, Numero: inteiro)];

            case TipoDado.Decimal:
                if (!decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero))
                {
                    erro = "Deve ser um número com ponto decimal (ex.: 12.5).";
                    return [];
                }
                return [new CondicaoFiltro(campo, comparacao, Numero: numero)];

            case TipoDado.Data:
                if (!DataValidador.TentarConverter(texto, out var data))
                {
                    erro = "Deve ser uma data no formato aaaa-mm-dd.";
                    return [];
                }
                return [new CondicaoFiltro(campo, comparacao, Data: data.ToDateTime(TimeOnly.MinValue))];

            case TipoDado.DataHora:
                return ConverterDataHora(campo, comparacao, texto, out erro);

            case TipoDado.Booleano:
                if (texto is not ("true" or "false"))
                {
                    erro = "Deve ser true ou false.";
                    return [];
                }
                return [new CondicaoFiltro(campo, comparacao, Booleano: texto == "true")];

            case TipoDado.Referencia:
                if (!Guid.TryParseExact(texto, "D", out var id))
                {
                    erro = "Deve ser o id (GUID) de um registro.";
                    return [];
                }
                return [new CondicaoFiltro(campo, comparacao, Referencia: id)];

            default:
                erro = "Tipo de campo sem filtro.";
                return [];
        }
    }

    /// <summary>
    /// DATA_HORA aceita data e hora ISO 8601 com fuso (instante exato) ou só a data "aaaa-mm-dd".
    /// Só a data = o DIA INTEIRO em UTC: "igual" vira [dia, dia+1), "ate" vira &lt; dia+1.
    /// </summary>
    private static IReadOnlyList<CondicaoFiltro> ConverterDataHora(Campo campo, Comparacao comparacao, string texto, out string? erro)
    {
        erro = null;

        if (DataHoraValidador.TentarConverter(texto, out var instante))
            return [new CondicaoFiltro(campo, comparacao, Data: instante.UtcDateTime)];

        if (!DataValidador.TentarConverter(texto, out var dia))
        {
            erro = "Deve ser uma data (aaaa-mm-dd) ou data e hora ISO 8601 com fuso (ex.: 2026-10-21T14:30:00-03:00).";
            return [];
        }

        var inicio = dia.ToDateTime(TimeOnly.MinValue);
        var diaSeguinte = inicio.AddDays(1);

        return comparacao switch
        {
            Comparacao.Igual =>
            [
                new CondicaoFiltro(campo, Comparacao.MaiorOuIgual, Data: inicio),
                new CondicaoFiltro(campo, Comparacao.Menor, Data: diaSeguinte),
            ],
            Comparacao.MenorOuIgual => [new CondicaoFiltro(campo, Comparacao.Menor, Data: diaSeguinte)],
            _ => [new CondicaoFiltro(campo, comparacao, Data: inicio)],
        };
    }
}
