using Catalogo.Application.CasosDeUso.Avaliacoes;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Avaliações de um item (RN44 a RN47). Avaliam INTERNO, GESTOR e ADMINISTRADOR; o EXTERNO vê só o resumo.
/// </summary>
[ApiController]
[Route(RotasApi.V1 + "/grupos/{grupo}/entidades/{entidade}/registros/{id:guid}/avaliacoes")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class AvaliacoesController(
    ListarAvaliacoesRegistro listarAvaliacoes,
    AvaliarRegistro avaliarRegistro,
    RemoverMinhaAvaliacao removerMinhaAvaliacao,
    ListarHistoricoAvaliacoes listarHistorico,
    ILogger<AvaliacoesController> logger) : ControllerBase
{
    /// <summary>
    /// Resumo (médias, quantidades, distribuição e a sua avaliação) e as avaliações individuais, paginadas,
    /// das mais recentes para as mais antigas. Para o EXTERNO, "avaliacoes" vem nulo (RN47).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<AvaliacoesDoRegistroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AvaliacoesDoRegistroDto>> Listar(
        string grupo, string entidade, Guid id, [FromQuery] string? pagina, [FromQuery] string? tamanho,
        CancellationToken cancellationToken) =>
        Ok(await listarAvaliacoes.ExecutarAsync(grupo, entidade, id, ParametrosDaUrl.Ler(Request), cancellationToken));

    /// <summary>
    /// Cria (201) ou edita (200) a sua avaliação: { "versao": "...", "respostas": { "qualidade": { "estrelas": 4,
    /// "comentario": "..." } } }. A "versao" é obrigatória só ao editar.
    /// </summary>
    [HttpPut("minha")]
    [Consumes("application/json")]
    [ProducesResponseType<AvaliacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AvaliacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AvaliacaoDto>> Avaliar(
        string grupo, string entidade, Guid id, [FromBody] AvaliacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var resultado = await avaliarRegistro.ExecutarAsync(grupo, entidade, id, entrada, cancellationToken);
        logger.LogInformation("Avaliação {AvaliacaoId} do registro {RegistroId} {Operacao}",
            resultado.Avaliacao.Id, id, resultado.Criada ? "criada" : "atualizada");

        return resultado.Criada
            ? CreatedAtAction(nameof(Listar), new { grupo, entidade, id }, resultado.Avaliacao)
            : Ok(resultado.Avaliacao);
    }

    /// <summary>Remove a sua avaliação deste item (no modelo ativo).</summary>
    [HttpDelete("minha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Remover(string grupo, string entidade, Guid id, CancellationToken cancellationToken)
    {
        await removerMinhaAvaliacao.ExecutarAsync(grupo, entidade, id, cancellationToken);
        logger.LogInformation("Avaliação do registro {RegistroId} removida pelo autor", id);
        return NoContent();
    }

    /// <summary>Histórico das avaliações deste item (criação, edição e remoção). EXTERNO: 403 (RN47).</summary>
    [HttpGet("historico")]
    [ProducesResponseType<PaginaDto<HistoricoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<HistoricoDto>>> Historico(
        string grupo, string entidade, Guid id, [FromQuery] string? pagina, [FromQuery] string? tamanho,
        CancellationToken cancellationToken) =>
        Ok(await listarHistorico.ExecutarAsync(grupo, entidade, id, ParametrosDaUrl.Ler(Request), cancellationToken));
}
