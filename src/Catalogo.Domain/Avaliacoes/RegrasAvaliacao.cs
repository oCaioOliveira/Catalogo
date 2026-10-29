using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Aprovacao;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Avaliacoes;

/// <summary>A resposta a um critério, já validada e normalizada (texto aparado).</summary>
public sealed record RespostaValidada(CriterioAvaliacao Criterio, byte? Estrelas, string? Texto);

/// <summary>
/// Regras de uma avaliação (RN44 a RN47), como código puro. QUEM avalia fica na PoliticaPermissao;
/// aqui ficam O QUE pode ser avaliado e se as respostas são válidas.
/// </summary>
public static class RegrasAvaliacao
{
    public const string PropriedadeEstrelas = "estrelas";
    public const string PropriedadeComentario = "comentario";
    public const string PropriedadeTexto = "texto";

    /// <summary>
    /// RN44: só itens APROVADOS são avaliados. Com a aprovação ativa, isso exclui PENDENTES e REJEITADOS
    /// (409); sem ela, os registros já nascem APROVADOS.
    /// </summary>
    public static bool PodeSerAvaliado(SituacaoAprovacao situacao) => situacao == SituacaoAprovacao.Aprovado;

    /// <summary>
    /// Média com uma casa decimal (RN47), sempre com a casa: 4 vira 4.0 no JSON (somar 0.0m fixa a escala do decimal
    /// em 1). Nula se ninguém deu nota.
    /// </summary>
    public static decimal? ArredondarMedia(double? media) =>
        media is double valor ? Math.Round((decimal)valor, 1, MidpointRounding.AwayFromZero) + 0.0m : null;

    /// <summary>
    /// RN46: valida as respostas contra os critérios ATIVOS do modelo e devolve TODOS os erros juntos, com
    /// chaves "respostas.&lt;criterio&gt;" e "respostas.&lt;criterio&gt;.&lt;propriedade&gt;".
    /// Formato: { "qualidade": { "estrelas": 4, "comentario": "..." }, "observacoes": { "texto": "..." } }.
    /// Critério ausente ou nulo = não respondido (erro só se for obrigatório). Vale o modelo ATUAL: uma
    /// avaliação antiga só é revalidada quando é editada (RN43).
    /// </summary>
    public static IReadOnlyList<RespostaValidada> ValidarRespostas(ModeloAvaliacao modelo, JsonObject? respostas, ErrosPorCampo erros)
    {
        if (respostas is null)
        {
            erros.Adicionar("respostas", "Obrigatório: envie as respostas por critério, ex.: { \"qualidade\": { \"estrelas\": 4 } }.");
            return [];
        }

        var validadas = new List<RespostaValidada>();
        foreach (var (chave, valor) in respostas)
        {
            var criterio = modelo.ObterCriterio(chave);
            if (criterio is null || !criterio.Ativo)
            {
                erros.Adicionar($"respostas.{chave}", "Critério desconhecido ou inativo neste modelo de avaliação.");
                continue;
            }

            if (valor is null)
                continue;

            var resposta = criterio.Tipo == TipoCriterio.Estrelas
                ? ValidarEstrelas(criterio, valor, $"respostas.{chave}", erros)
                : ValidarTexto(criterio, valor, $"respostas.{chave}", erros);
            if (resposta is not null)
                validadas.Add(resposta);
        }

        foreach (var criterio in modelo.CriteriosAtivos.Where(c => c.Obrigatorio))
        {
            if (respostas[criterio.Chave] is null)
                erros.Adicionar($"respostas.{criterio.Chave}", $"Obrigatório: responda '{criterio.Nome}'.");
        }

        return validadas;
    }

    /// <summary>
    /// Grava as respostas validadas na avaliação (nova ou editada). Só mexe nos critérios ATIVOS: respostas
    /// antigas a critérios desativados ficam como estão (RN43). Critério ativo sem resposta = resposta removida.
    /// Devolve as respostas removidas (para o repositório excluir).
    /// </summary>
    public static IReadOnlyList<AvaliacaoResposta> AplicarRespostas(
        Avaliacao avaliacao, ModeloAvaliacao modelo, IReadOnlyList<RespostaValidada> respostas)
    {
        var removidas = new List<AvaliacaoResposta>();
        foreach (var criterio in modelo.CriteriosAtivos)
        {
            var nova = respostas.FirstOrDefault(r => r.Criterio.Id == criterio.Id);
            var atual = avaliacao.Respostas.FirstOrDefault(r => r.CriterioAvaliacaoId == criterio.Id);

            if (nova is null)
            {
                if (atual is not null)
                {
                    avaliacao.Respostas.Remove(atual);
                    removidas.Add(atual);
                }
            }
            else if (atual is null)
            {
                avaliacao.Respostas.Add(new AvaliacaoResposta
                {
                    AvaliacaoId = avaliacao.Id,
                    CriterioAvaliacaoId = criterio.Id,
                    Estrelas = nova.Estrelas,
                    Texto = nova.Texto,
                });
            }
            else
            {
                atual.Estrelas = nova.Estrelas;
                atual.Texto = nova.Texto;
            }
        }

        return removidas;
    }

    private static RespostaValidada? ValidarEstrelas(CriterioAvaliacao criterio, JsonNode valor, string chave, ErrosPorCampo erros)
    {
        if (valor is not JsonObject objeto)
        {
            erros.Adicionar(chave, "Deve ser um objeto: { \"estrelas\": 1 a 5, \"comentario\": \"...\" }.");
            return null;
        }

        var valido = PropriedadesConhecidas(objeto, chave, erros, PropriedadeEstrelas, PropriedadeComentario);

        byte? estrelas = null;
        var noEstrelas = objeto[PropriedadeEstrelas];
        if (noEstrelas is null)
        {
            erros.Adicionar($"{chave}.{PropriedadeEstrelas}", "Obrigatório: de 1 a 5.");
            valido = false;
        }
        // O número é lido pelo texto JSON: funciona igual para JSON recebido ("4", "4.0") e para valores criados em código.
        else if (noEstrelas is not JsonValue numero || numero.GetValueKind() != JsonValueKind.Number
                 || !decimal.TryParse(numero.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var nota)
                 || nota % 1 != 0 || nota is < 1 or > 5)
        {
            erros.Adicionar($"{chave}.{PropriedadeEstrelas}", "Deve ser um número inteiro de 1 a 5.");
            valido = false;
        }
        else
        {
            estrelas = (byte)nota;
        }

        var (comentarioValido, comentario) = LerTexto(objeto, PropriedadeComentario, $"{chave}.{PropriedadeComentario}", erros);
        valido &= comentarioValido;
        if (comentarioValido)
        {
            // Texto em branco num comentário = comentário não enviado.
            comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
            if (comentario is not null && criterio.Comentario == ExigenciaComentario.Nenhum)
            {
                erros.Adicionar($"{chave}.{PropriedadeComentario}", "Este critério não aceita comentário.");
                valido = false;
            }
            else if (comentario is null && criterio.Comentario == ExigenciaComentario.Obrigatorio)
            {
                erros.Adicionar($"{chave}.{PropriedadeComentario}", "Obrigatório: justifique a nota.");
                valido = false;
            }
            else if (comentario?.Length > criterio.TamanhoMaximoTexto)
            {
                erros.Adicionar($"{chave}.{PropriedadeComentario}", $"Deve ter no máximo {criterio.TamanhoMaximoTexto} caracteres.");
                valido = false;
            }
        }

        return valido ? new RespostaValidada(criterio, estrelas, comentario) : null;
    }

    private static RespostaValidada? ValidarTexto(CriterioAvaliacao criterio, JsonNode valor, string chave, ErrosPorCampo erros)
    {
        if (valor is not JsonObject objeto)
        {
            erros.Adicionar(chave, "Deve ser um objeto: { \"texto\": \"...\" }.");
            return null;
        }

        var valido = PropriedadesConhecidas(objeto, chave, erros, PropriedadeTexto);
        var (textoValido, texto) = LerTexto(objeto, PropriedadeTexto, $"{chave}.{PropriedadeTexto}", erros);
        if (!textoValido)
            return null;

        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            erros.Adicionar($"{chave}.{PropriedadeTexto}", "Texto vazio: preencha ou não envie este critério.");
            return null;
        }

        if (texto.Length > criterio.TamanhoMaximoTexto)
        {
            erros.Adicionar($"{chave}.{PropriedadeTexto}", $"Deve ter no máximo {criterio.TamanhoMaximoTexto} caracteres.");
            return null;
        }

        return valido ? new RespostaValidada(criterio, null, texto) : null;
    }

    /// <summary>Lê uma propriedade de texto opcional. Falso se veio algo que não é texto.</summary>
    private static (bool Valido, string? Texto) LerTexto(JsonObject objeto, string propriedade, string chave, ErrosPorCampo erros)
    {
        var no = objeto[propriedade];
        if (no is null)
            return (true, null);

        if (no is JsonValue valor && valor.GetValueKind() == JsonValueKind.String && valor.TryGetValue<string>(out var texto))
            return (true, texto);

        erros.Adicionar(chave, "Deve ser um texto.");
        return (false, null);
    }

    private static bool PropriedadesConhecidas(JsonObject objeto, string chave, ErrosPorCampo erros, params string[] permitidas)
    {
        var valido = true;
        foreach (var (propriedade, _) in objeto)
        {
            if (!permitidas.Contains(propriedade, StringComparer.Ordinal))
            {
                erros.Adicionar($"{chave}.{propriedade}", $"Propriedade desconhecida. Use: {string.Join(", ", permitidas)}.");
                valido = false;
            }
        }

        return valido;
    }
}
