using System.Text.RegularExpressions;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Avaliacoes;

/// <summary>
/// Um critério como veio da API, SEM validação (textos crus, tudo anulável). Ver <see cref="RegrasModeloAvaliacao.Validar"/>.
/// </summary>
public sealed record DefinicaoCriterio(
    string? Chave,
    string? Nome,
    string? Descricao,
    string? Tipo,
    bool? Obrigatorio = null,
    string? Comentario = null,
    int? TamanhoMaximoTexto = null,
    int? Ordem = null,
    bool? Ativo = null);

/// <summary>O modelo como veio da API (POST e PUT), sem validação.</summary>
public sealed record DefinicaoModelo(string? Nome, string? Descricao, IReadOnlyList<DefinicaoCriterio?>? Criterios);

/// <summary>Um critério já validado e normalizado.</summary>
public sealed record CriterioDefinido(
    string Chave, string Nome, string? Descricao, TipoCriterio Tipo, bool Obrigatorio,
    ExigenciaComentario Comentario, int TamanhoMaximoTexto, int Ordem, bool Ativo);

/// <summary>O modelo já validado e normalizado.</summary>
public sealed record ModeloDefinido(string Nome, string? Descricao, IReadOnlyList<CriterioDefinido> Criterios);

/// <summary>
/// Regras do modelo de avaliação (RN42 e RN43), como código puro: validar o que veio da API, criar o
/// modelo e alterá-lo sem corromper as avaliações já feitas. Os erros vão por campo, todos juntos (RN05),
/// com chaves como "nome", "criterios" e "criterios[1].tipo".
/// </summary>
public static partial class RegrasModeloAvaliacao
{
    public const int TamanhoMaximoNome = 150;
    public const int TamanhoMaximoDescricao = 500;
    public const int TamanhoMaximoChave = 100;

    /// <summary>RN42: valida e normaliza a definição. Devolve nulo se houver erros (já adicionados em <paramref name="erros"/>).</summary>
    public static ModeloDefinido? Validar(DefinicaoModelo definicao, ErrosPorCampo erros)
    {
        var errosAntes = ContarMensagens(erros);

        var nome = Texto(definicao.Nome);
        if (nome is null)
            erros.Adicionar("nome", "Obrigatório.");
        else if (nome.Length > TamanhoMaximoNome)
            erros.Adicionar("nome", $"Deve ter no máximo {TamanhoMaximoNome} caracteres.");

        var descricao = Texto(definicao.Descricao);
        if (descricao?.Length > TamanhoMaximoDescricao)
            erros.Adicionar("descricao", $"Deve ter no máximo {TamanhoMaximoDescricao} caracteres.");

        var criterios = new List<CriterioDefinido>();
        var lista = definicao.Criterios ?? [];
        var chavesVistas = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < lista.Count; i++)
        {
            var criterio = ValidarCriterio(lista[i], i, chavesVistas, erros);
            if (criterio is not null)
                criterios.Add(criterio);
        }

        // Pelo menos um critério ATIVO (inativos sozinhos não formam um formulário).
        if (!lista.Any(c => c is not null && (c.Ativo ?? true)))
            erros.Adicionar("criterios", "Informe pelo menos um critério ativo.");

        return ContarMensagens(erros) > errosAntes ? null : new ModeloDefinido(nome!, descricao, criterios);
    }

    private static CriterioDefinido? ValidarCriterio(DefinicaoCriterio? definicao, int indice, HashSet<string> chavesVistas, ErrosPorCampo erros)
    {
        var prefixo = $"criterios[{indice}]";
        if (definicao is null)
        {
            erros.Adicionar(prefixo, "Critério vazio.");
            return null;
        }

        var valido = true;
        void Erro(string campo, string mensagem)
        {
            erros.Adicionar($"{prefixo}.{campo}", mensagem);
            valido = false;
        }

        var chave = definicao.Chave?.Trim() ?? "";
        if (chave.Length == 0)
            Erro("chave", "Obrigatória.");
        else if (chave.Length > TamanhoMaximoChave)
            Erro("chave", $"Deve ter no máximo {TamanhoMaximoChave} caracteres.");
        else if (!PadraoChave().IsMatch(chave))
            Erro("chave", "Use só letras minúsculas sem acento, números e \"_\" (ex.: tempo_execucao).");
        else if (!chavesVistas.Add(chave))
            Erro("chave", $"A chave '{chave}' aparece mais de uma vez no modelo.");

        var nome = Texto(definicao.Nome);
        if (nome is null)
            Erro("nome", "Obrigatório.");
        else if (nome.Length > TamanhoMaximoNome)
            Erro("nome", $"Deve ter no máximo {TamanhoMaximoNome} caracteres.");

        var descricao = Texto(definicao.Descricao);
        if (descricao?.Length > TamanhoMaximoDescricao)
            Erro("descricao", $"Deve ter no máximo {TamanhoMaximoDescricao} caracteres.");

        var tipo = TipoCriterio.Estrelas;
        if (string.IsNullOrWhiteSpace(definicao.Tipo))
            Erro("tipo", "Obrigatório: ESTRELAS ou TEXTO.");
        else if (!AvaliacaoConversor.TentarConverterTipo(definicao.Tipo, out tipo))
            Erro("tipo", "Deve ser ESTRELAS ou TEXTO.");

        var comentario = ExigenciaComentario.Nenhum;
        if (definicao.Comentario is not null && !AvaliacaoConversor.TentarConverterComentario(definicao.Comentario, out comentario))
            Erro("comentario", "Deve ser NENHUM, OPCIONAL ou OBRIGATORIO.");
        else if (comentario != ExigenciaComentario.Nenhum && tipo == TipoCriterio.Texto)
            Erro("comentario", "Só critérios ESTRELAS aceitam comentário. Em TEXTO, use NENHUM.");

        var tamanho = definicao.TamanhoMaximoTexto ?? CriterioAvaliacao.TamanhoMaximoTextoPadrao;
        if (tamanho is < 1 or > CriterioAvaliacao.TamanhoMaximoTextoLimite)
            Erro("tamanhoMaximoTexto", $"Deve estar entre 1 e {CriterioAvaliacao.TamanhoMaximoTextoLimite}.");

        return valido
            ? new CriterioDefinido(chave, nome!, descricao, tipo, definicao.Obrigatorio ?? false, comentario, tamanho,
                definicao.Ordem ?? indice + 1, definicao.Ativo ?? true)
            : null;
    }

    /// <summary>
    /// RN43: alterar um modelo que já tem avaliações. Um critério COM respostas não pode ser excluído
    /// (deve ser desativado) nem mudar de tipo. Todo o resto pode: nome, descrição, ordem, obrigatoriedade,
    /// comentário, tamanho e critérios novos.
    /// </summary>
    /// <param name="criteriosComRespostas">Ids dos critérios do modelo que têm pelo menos uma resposta.</param>
    public static void ValidarAlteracao(
        ModeloAvaliacao atual, ModeloDefinido novo, IReadOnlySet<Guid> criteriosComRespostas, ErrosPorCampo erros)
    {
        foreach (var criterio in atual.Criterios.Where(c => criteriosComRespostas.Contains(c.Id)).OrderBy(c => c.Ordem))
        {
            var indice = novo.Criterios.ToList().FindIndex(c => c.Chave == criterio.Chave);
            if (indice < 0)
            {
                erros.Adicionar("criterios",
                    $"O critério '{criterio.Chave}' já tem respostas e não pode ser excluído. Para tirá-lo de uso, desative-o: envie-o com \"ativo\": false.");
            }
            else if (novo.Criterios[indice].Tipo != criterio.Tipo)
            {
                erros.Adicionar($"criterios[{indice}].tipo",
                    $"O critério '{criterio.Chave}' já tem respostas: o tipo não pode mudar. Desative-o e crie um critério novo.");
            }
        }
    }

    /// <summary>Modelo novo, ativo, a partir da definição validada.</summary>
    public static ModeloAvaliacao Criar(ModeloDefinido definido, Guid entidadeId, Guid equipeId, Guid autorId) => new()
    {
        EntidadeId = entidadeId,
        EquipeId = equipeId,
        Nome = definido.Nome,
        Descricao = definido.Descricao,
        Ativo = true,
        CriadoPorId = autorId,
        Criterios = definido.Criterios.Select(c => Preencher(new CriterioAvaliacao(), c)).ToList(),
    };

    /// <summary>
    /// Aplica a definição (já validada, inclusive por <see cref="ValidarAlteracao"/>) ao modelo. Critérios
    /// são identificados pela chave: os que existem são atualizados, os novos são incluídos, e os que
    /// sumiram da lista saem do modelo. Devolve os que saíram (para o repositório excluir).
    /// </summary>
    public static IReadOnlyList<CriterioAvaliacao> Aplicar(ModeloAvaliacao modelo, ModeloDefinido novo, Guid autorId, DateTime agoraUtc)
    {
        modelo.Nome = novo.Nome;
        modelo.Descricao = novo.Descricao;
        modelo.AtualizadoPorId = autorId;
        modelo.AtualizadoEm = agoraUtc;

        var removidos = modelo.Criterios.Where(c => novo.Criterios.All(n => n.Chave != c.Chave)).ToList();
        foreach (var removido in removidos)
            modelo.Criterios.Remove(removido);

        foreach (var definido in novo.Criterios)
        {
            var existente = modelo.ObterCriterio(definido.Chave);
            if (existente is null)
                modelo.Criterios.Add(Preencher(new CriterioAvaliacao { ModeloAvaliacaoId = modelo.Id }, definido));
            else
                Preencher(existente, definido);
        }

        return removidos;
    }

    private static CriterioAvaliacao Preencher(CriterioAvaliacao criterio, CriterioDefinido definido)
    {
        criterio.Chave = definido.Chave;
        criterio.Nome = definido.Nome;
        criterio.Descricao = definido.Descricao;
        criterio.Tipo = definido.Tipo;
        criterio.Obrigatorio = definido.Obrigatorio;
        criterio.Comentario = definido.Comentario;
        criterio.TamanhoMaximoTexto = definido.TamanhoMaximoTexto;
        criterio.Ordem = definido.Ordem;
        criterio.Ativo = definido.Ativo;
        return criterio;
    }

    private static int ContarMensagens(ErrosPorCampo erros) => erros.ParaDicionario().Sum(e => e.Value.Length);

    /// <summary>Texto aparado; vazio vira nulo.</summary>
    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex PadraoChave();
}
