using System.Text.Json.Nodes;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Avaliacoes;

namespace Catalogo.Application.Mapeamentos;

/// <summary>Mapeamento manual dos modelos e avaliações (sem AutoMapper, ver docs/adr/0004).</summary>
public static class AvaliacaoMapeamentos
{
    public static CriterioAvaliacaoDto ParaDto(this CriterioAvaliacao criterio, bool? possuiRespostas = null) => new(
        criterio.Id,
        criterio.Chave,
        criterio.Nome,
        criterio.Descricao,
        AvaliacaoConversor.ParaTexto(criterio.Tipo),
        criterio.Obrigatorio,
        AvaliacaoConversor.ParaTexto(criterio.Comentario),
        criterio.TamanhoMaximoTexto,
        criterio.Ordem,
        criterio.Ativo,
        possuiRespostas);

    /// <summary>Todos os critérios, em ordem (ativos e inativos).</summary>
    public static IEnumerable<CriterioAvaliacao> CriteriosEmOrdem(this ModeloAvaliacao modelo) =>
        modelo.Criterios.OrderBy(c => c.Ordem).ThenBy(c => c.Chave, StringComparer.Ordinal);

    /// <summary>O formulário para o front (só critérios ativos).</summary>
    public static FormularioAvaliacaoDto ParaFormularioDto(this ModeloAvaliacao modelo) =>
        new(modelo.Id, modelo.Nome, modelo.Descricao, modelo.CriteriosAtivos.Select(c => c.ParaDto()).ToList());

    public static ModeloResumoDto ParaResumoDto(this ModeloAvaliacao modelo) => new(modelo.Id, modelo.Nome, modelo.Ativo);

    /// <summary>
    /// Objeto PLANO do modelo para o histórico (RN32): uma chave por critério ("criterios.qualidade"), para
    /// as mudanças aparecerem critério a critério.
    /// </summary>
    public static JsonObject ParaJsonHistorico(this ModeloAvaliacao modelo)
    {
        var json = new JsonObject
        {
            ["modelo"] = modelo.Id.ToString("D"),
            ["nome"] = modelo.Nome,
            ["descricao"] = modelo.Descricao,
            ["ativo"] = modelo.Ativo,
        };

        foreach (var criterio in modelo.CriteriosEmOrdem())
        {
            json[$"criterios.{criterio.Chave}"] = new JsonObject
            {
                ["nome"] = criterio.Nome,
                ["descricao"] = criterio.Descricao,
                ["tipo"] = AvaliacaoConversor.ParaTexto(criterio.Tipo),
                ["obrigatorio"] = criterio.Obrigatorio,
                ["comentario"] = AvaliacaoConversor.ParaTexto(criterio.Comentario),
                ["tamanhoMaximoTexto"] = criterio.TamanhoMaximoTexto,
                ["ordem"] = criterio.Ordem,
                ["ativo"] = criterio.Ativo,
            };
        }

        return json;
    }

    /// <summary>
    /// Respostas no formato da entrada: { "qualidade": { "estrelas": 4, "comentario": "..." }, "obs": { "texto": "..." } }.
    /// </summary>
    /// <param name="somenteAtivos">Para exibição: critérios inativos somem (RN43). No histórico, vai tudo.</param>
    public static JsonObject RespostasParaJson(this Avaliacao avaliacao, ModeloAvaliacao modelo, bool somenteAtivos = true)
    {
        var json = new JsonObject();
        foreach (var criterio in modelo.CriteriosEmOrdem().Where(c => c.Ativo || !somenteAtivos))
        {
            var resposta = avaliacao.Respostas.FirstOrDefault(r => r.CriterioAvaliacaoId == criterio.Id);
            if (resposta is null)
                continue;

            json[criterio.Chave] = criterio.Tipo == TipoCriterio.Estrelas
                ? new JsonObject
                {
                    [RegrasAvaliacao.PropriedadeEstrelas] = resposta.Estrelas,
                    [RegrasAvaliacao.PropriedadeComentario] = resposta.Texto,
                }
                : new JsonObject { [RegrasAvaliacao.PropriedadeTexto] = resposta.Texto };
        }

        return json;
    }

    /// <summary>
    /// Objeto PLANO da avaliação para o histórico (RN45): o modelo e uma chave por resposta
    /// ("qualidade.estrelas", "qualidade.comentario", "observacoes.texto"), com TODOS os critérios.
    /// </summary>
    public static JsonObject ParaJsonHistorico(this Avaliacao avaliacao, ModeloAvaliacao modelo)
    {
        var json = new JsonObject
        {
            ["avaliacao"] = avaliacao.Id.ToString("D"),
            ["modelo"] = modelo.Nome,
        };

        foreach (var (chave, resposta) in avaliacao.RespostasParaJson(modelo, somenteAtivos: false))
        {
            foreach (var (propriedade, valor) in (JsonObject)resposta!)
            {
                if (valor is not null)
                    json[$"{chave}.{propriedade}"] = valor.DeepClone();
            }
        }

        return json;
    }

    public static AvaliacaoDto ParaDto(
        this Avaliacao avaliacao, ModeloAvaliacao modelo, IReadOnlyDictionary<Guid, UsuarioResumoDto> usuarios) => new(
        avaliacao.Id,
        modelo.ParaResumoDto(),
        usuarios.GetValueOrDefault(avaliacao.UsuarioId),
        avaliacao.CriadoEm,
        avaliacao.AtualizadoEm,
        Convert.ToBase64String(avaliacao.Versao),
        avaliacao.RespostasParaJson(modelo));
}
