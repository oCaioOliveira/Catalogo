using Catalogo.Application.CasosDeUso.Aprovacao;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Configurações de uma entidade para uma equipe (RN33). Só o GESTOR da equipe (404 para as outras) e o
/// ADMINISTRADOR (qualquer equipe); INTERNO e EXTERNO recebem 403.
/// </summary>
[ApiController]
[Route(RotasApi.V1 + "/equipes/{equipeId:guid}/configuracoes/grupos/{grupo}/entidades/{entidade}/aprovacao")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ConfiguracoesController(
    ObterConfiguracaoAprovacao obterConfiguracao,
    DefinirConfiguracaoAprovacao definirConfiguracao,
    ListarHistoricoConfiguracao listarHistorico,
    ILogger<ConfiguracoesController> logger) : ControllerBase
{
    /// <summary>Aprovação ativa para a entidade nesta equipe?</summary>
    [HttpGet]
    [ProducesResponseType<ConfiguracaoAprovacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConfiguracaoAprovacaoDto>> Obter(
        Guid equipeId, string grupo, string entidade, CancellationToken cancellationToken) =>
        Ok(await obterConfiguracao.ExecutarAsync(equipeId, grupo, entidade, cancellationToken));

    /// <summary>Ativa ou desativa: { "ativa": true }. Desativar com registros pendentes → 409.</summary>
    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<ConfiguracaoAprovacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConfiguracaoAprovacaoDto>> Definir(
        Guid equipeId, string grupo, string entidade, [FromBody] ConfiguracaoAprovacaoEntrada entrada, CancellationToken cancellationToken)
    {
        var configuracao = await definirConfiguracao.ExecutarAsync(equipeId, grupo, entidade, entrada, cancellationToken);
        logger.LogInformation("Aprovação de {Grupo}/{Entidade} na equipe {EquipeId}: {Ativa}", grupo, entidade, equipeId, configuracao.Ativa);
        return Ok(configuracao);
    }

    /// <summary>Histórico desta configuração, do mais recente para o mais antigo (RN32).</summary>
    [HttpGet("historico")]
    [ProducesResponseType<PaginaDto<HistoricoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<HistoricoDto>>> Historico(
        Guid equipeId, string grupo, string entidade, [FromQuery] string? pagina, [FromQuery] string? tamanho,
        CancellationToken cancellationToken) =>
        Ok(await listarHistorico.ExecutarAsync(equipeId, grupo, entidade, ParametrosDaUrl.Ler(Request), cancellationToken));
}
