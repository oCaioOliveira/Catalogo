using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>Metadados das entidades de um grupo (para o front montar formulários).</summary>
[ApiController]
[Route(RotasApi.V1 + "/grupos/{grupo}/entidades")]
[Produces("application/json")]
public sealed class EntidadesController(ListarEntidades listarEntidades) : ControllerBase
{
    /// <summary>
    /// Lista as entidades do grupo com os seus campos ativos e restrições únicas. Com <paramref name="equipeId"/>,
    /// cada entidade informa <c>requerAprovacao</c> nessa equipe (RN33).
    /// </summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="equipeId">Equipe para informar a aprovação (opcional).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EntidadeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EntidadeDto>>> Listar(
        string grupo, [FromQuery] string? equipeId, CancellationToken cancellationToken)
    {
        return Ok(await listarEntidades.ExecutarAsync(grupo, equipeId, cancellationToken));
    }
}
