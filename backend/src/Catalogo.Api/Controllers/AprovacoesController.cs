using Catalogo.Application.CasosDeUso.Aprovacao;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>Fila de aprovação do GESTOR (das suas equipes) e do ADMINISTRADOR (de todas). RN36.</summary>
[ApiController]
[Route(RotasApi.V1 + "/aprovacoes")]
[Produces("application/json")]
public sealed class AprovacoesController(ListarPendentes listarPendentes) : ControllerBase
{
    /// <summary>Registros PENDENTES, dos mais antigos para os mais novos.</summary>
    /// <param name="grupo">Só deste grupo (chave).</param>
    /// <param name="entidade">Só desta entidade (chave).</param>
    /// <param name="equipeId">Só desta equipe.</param>
    /// <param name="pagina">Página, a partir de 1.</param>
    /// <param name="tamanho">Itens por página (máximo 100).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("pendentes")]
    [ProducesResponseType<PaginaDto<PendenteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaDto<PendenteDto>>> Pendentes(
        // Declarados só para aparecerem no Swagger: a leitura é feita por ParametrosDaUrl.
        [FromQuery] string? grupo, [FromQuery] string? entidade, [FromQuery] string? equipeId,
        [FromQuery] string? pagina, [FromQuery] string? tamanho, CancellationToken cancellationToken) =>
        Ok(await listarPendentes.ExecutarAsync(ParametrosDaUrl.Ler(Request), cancellationToken));
}
