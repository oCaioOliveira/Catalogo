using Catalogo.Application.CasosDeUso.Avaliacoes;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Modelo de avaliação de uma entidade numa equipe (RN40 a RN43), no mesmo padrão da configuração de aprovação.
/// Só o GESTOR da equipe (404 para as outras) e o ADMINISTRADOR (qualquer equipe); INTERNO e EXTERNO recebem 403.
/// </summary>
[ApiController]
[Route(RotasApi.V1 + "/equipes/{equipeId:guid}/configuracoes/grupos/{grupo}/entidades/{entidade}/avaliacao")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ModelosAvaliacaoController(
    ObterModeloAvaliacao obterModelo,
    ListarModelosAvaliacao listarModelos,
    CriarModeloAvaliacao criarModelo,
    AtualizarModeloAvaliacao atualizarModelo,
    AlterarAtivacaoModeloAvaliacao alterarAtivacao,
    ListarHistoricoModeloAvaliacao listarHistorico,
    ILogger<ModelosAvaliacaoController> logger) : ControllerBase
{
    /// <summary>O modelo ATIVO, com todos os critérios em ordem. 404 se não há modelo ativo.</summary>
    [HttpGet]
    [ProducesResponseType<ModeloAvaliacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModeloAvaliacaoDto>> Obter(
        Guid equipeId, string grupo, string entidade, CancellationToken cancellationToken) =>
        Ok(await obterModelo.ExecutarAsync(equipeId, grupo, entidade, cancellationToken));

    /// <summary>Todos os modelos (ativo e inativos), mais recentes primeiro.</summary>
    [HttpGet("modelos")]
    [ProducesResponseType<IReadOnlyList<ModeloAvaliacaoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ModeloAvaliacaoDto>>> ListarModelos(
        Guid equipeId, string grupo, string entidade, CancellationToken cancellationToken) =>
        Ok(await listarModelos.ExecutarAsync(equipeId, grupo, entidade, cancellationToken));

    /// <summary>Cria o modelo com a lista de critérios. 409 se já existe um modelo ativo (RN41).</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ModeloAvaliacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeloAvaliacaoDto>> Criar(
        Guid equipeId, string grupo, string entidade, [FromBody] ModeloAvaliacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var modelo = await criarModelo.ExecutarAsync(equipeId, grupo, entidade, entrada, cancellationToken);
        logger.LogInformation("Modelo de avaliação {ModeloId} criado para {Grupo}/{Entidade} na equipe {EquipeId}",
            modelo.Id, grupo, entidade, equipeId);
        return CreatedAtAction(nameof(Obter), new { equipeId, grupo, entidade }, modelo);
    }

    /// <summary>
    /// Altera nome, descrição e critérios do modelo ativo, com a "versao" do GET (RN12). Critério com respostas
    /// não pode ser excluído nem mudar de tipo: desative-o (RN43).
    /// </summary>
    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<ModeloAvaliacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeloAvaliacaoDto>> Atualizar(
        Guid equipeId, string grupo, string entidade, [FromBody] ModeloAvaliacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var modelo = await atualizarModelo.ExecutarAsync(equipeId, grupo, entidade, entrada, cancellationToken);
        logger.LogInformation("Modelo de avaliação {ModeloId} alterado", modelo.Id);
        return Ok(modelo);
    }

    /// <summary>{ "ativo": false } desativa o modelo ativo; { "ativo": true, "modeloId": "..." } reativa um modelo.</summary>
    [HttpPatch("ativacao")]
    [Consumes("application/json")]
    [ProducesResponseType<ModeloAvaliacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeloAvaliacaoDto>> AlterarAtivacao(
        Guid equipeId, string grupo, string entidade, [FromBody] AtivacaoModeloEntrada entrada, CancellationToken cancellationToken)
    {
        var modelo = await alterarAtivacao.ExecutarAsync(equipeId, grupo, entidade, entrada, cancellationToken);
        logger.LogInformation("Modelo de avaliação {ModeloId}: ativo = {Ativo}", modelo.Id, modelo.Ativo);
        return Ok(modelo);
    }

    /// <summary>Histórico dos modelos desta entidade na equipe, do mais recente para o mais antigo (RN32).</summary>
    [HttpGet("historico")]
    [ProducesResponseType<PaginaDto<HistoricoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<HistoricoDto>>> Historico(
        Guid equipeId, string grupo, string entidade, [FromQuery] string? pagina, [FromQuery] string? tamanho,
        CancellationToken cancellationToken) =>
        Ok(await listarHistorico.ExecutarAsync(equipeId, grupo, entidade, ParametrosDaUrl.Ler(Request), cancellationToken));
}
