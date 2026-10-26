using Catalogo.Application.CasosDeUso.Administracao;
using Catalogo.Application.CasosDeUso.Historico;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Historico;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>Administração de equipes: só ADMINISTRADOR (403 para os demais). Equipe não é excluída, só desativada.</summary>
[ApiController]
[Route(RotasApi.V1 + "/equipes")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class EquipesController(
    ListarEquipes listarEquipes,
    ObterEquipe obterEquipe,
    CriarEquipe criarEquipe,
    AtualizarEquipe atualizarEquipe,
    AlterarAtivacaoEquipe alterarAtivacao,
    ListarHistoricoAcesso listarHistorico) : ControllerBase
{
    /// <summary>Lista as equipes, por nome, paginadas, com a quantidade de membros ativos de cada uma.</summary>
    /// <param name="ativa">true ou false: filtra pela situação (ausente = todas).</param>
    /// <param name="busca">Contido no nome (sem diferenciar maiúsculas nem acentos; até 100 caracteres).</param>
    /// <param name="pagina">Página, a partir de 1 (padrão 1).</param>
    /// <param name="tamanho">Itens por página (padrão 20, máximo 100).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<PaginaDto<EquipeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<EquipeDto>>> Listar(
        // Declarados só para aparecerem no Swagger: a leitura é feita por ParametrosDaUrl.
        [FromQuery] string? ativa, [FromQuery] string? busca, [FromQuery] string? pagina, [FromQuery] string? tamanho, CancellationToken cancellationToken) =>
        Ok(await listarEquipes.ExecutarAsync(ParametrosDaUrl.Ler(Request), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EquipeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipeDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await obterEquipe.ExecutarAsync(id, cancellationToken));

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<EquipeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EquipeDto>> Criar([FromBody] EquipeEntrada entrada, CancellationToken cancellationToken)
    {
        var equipe = await criarEquipe.ExecutarAsync(entrada, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = equipe.Id }, equipe);
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<EquipeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EquipeDto>> Atualizar(Guid id, [FromBody] EquipeEntrada entrada, CancellationToken cancellationToken) =>
        Ok(await atualizarEquipe.ExecutarAsync(id, entrada, cancellationToken));

    /// <summary>Ativa ou desativa: { "ativa": false }.</summary>
    [HttpPatch("{id:guid}/ativacao")]
    [Consumes("application/json")]
    [ProducesResponseType<EquipeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EquipeDto>> AlterarAtivacao(
        Guid id, [FromBody] AtivacaoEquipeEntrada entrada, CancellationToken cancellationToken) =>
        Ok(await alterarAtivacao.ExecutarAsync(id, entrada, cancellationToken));

    /// <summary>Histórico da equipe, do mais recente para o mais antigo (RN32).</summary>
    [HttpGet("{id:guid}/historico")]
    [ProducesResponseType<PaginaDto<HistoricoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<HistoricoDto>>> Historico(
        Guid id, [FromQuery] string? pagina, [FromQuery] string? tamanho, CancellationToken cancellationToken) =>
        Ok(await listarHistorico.ExecutarAsync(AlvoHistorico.Equipe, id, ParametrosDaUrl.Ler(Request), cancellationToken));
}
