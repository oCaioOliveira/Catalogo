using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>Conveniências por campo, para o front montar formulários.</summary>
[ApiController]
[Route(RotasApi.V1 + "/grupos/{grupo}/entidades/{entidade}/campos/{campo}")]
[Produces("application/json")]
public sealed class CamposController(ListarOpcoes listarOpcoes) : ControllerBase
{
    /// <summary>
    /// Opções de um campo REFERENCIA, ordenadas por rótulo (RN21). Se o campo depende de outro
    /// (veja "dependeDe" em GET .../entidades), informe o valor em <paramref name="dependeDe"/>.
    /// </summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade do formulário.</param>
    /// <param name="campo">Chave do campo REFERENCIA.</param>
    /// <param name="dependeDe">Id do registro escolhido no campo de que este depende.</param>
    /// <param name="busca">Trecho do rótulo, sem diferenciar maiúsculas nem acentos.</param>
    /// <param name="pagina">Página, a partir de 1 (padrão 1).</param>
    /// <param name="tamanho">Itens por página (padrão 20, máximo 100).</param>
    /// <param name="equipeId">Equipe do registro do formulário (RN27, RN29): obrigatória para o administrador e para quem tem mais de uma equipe.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("opcoes")]
    [ProducesResponseType<PaginaDto<OpcaoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaDto<OpcaoDto>>> Opcoes(
        string grupo, string entidade, string campo,
        // Declarados só para aparecerem no Swagger: a leitura é feita por ParametrosDaUrl.
        [FromQuery] string? dependeDe, [FromQuery] string? busca, [FromQuery] string? pagina, [FromQuery] string? tamanho,
        [FromQuery] string? equipeId,
        CancellationToken cancellationToken)
    {
        return Ok(await listarOpcoes.ExecutarAsync(grupo, entidade, campo, ParametrosDaUrl.Ler(Request), cancellationToken));
    }
}
