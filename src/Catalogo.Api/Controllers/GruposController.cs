using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>Grupos (áreas de gestão, "listagens" na API) disponíveis, para o front montar o menu, e o que é do usuário em cada um.</summary>
[ApiController]
[Route(RotasApi.V1 + "/grupos")]
[Produces("application/json")]
public sealed class GruposController(ListarGrupos listarGrupos, ListarRecentes listarRecentes) : ControllerBase
{
    /// <summary>
    /// Todos os grupos, por nome, com chave, nome e descrição. A chave é a usada em
    /// /api/v1/grupos/{grupo}/entidades. Não é paginada: são poucos e o menu precisa de todos.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GrupoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GrupoDto>>> Listar(CancellationToken cancellationToken) =>
        Ok(await listarGrupos.ExecutarAsync(cancellationToken));

    /// <summary>
    /// Os itens que o usuário logado criou ou alterou por último neste grupo, do mais recente para o mais antigo (no máximo 8),
    /// para o "Continuar de onde parou" da página Início (RN96). Vem do histórico, só com os itens que ele ainda vê (o mesmo
    /// escopo da listagem): excluídos, de outra equipe ou de tipo de item desativado ficam de fora.
    /// </summary>
    [HttpGet("{grupo}/eu/recentes")]
    [ProducesResponseType<IReadOnlyList<RecenteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RecenteDto>>> Recentes(string grupo, CancellationToken cancellationToken) =>
        Ok(await listarRecentes.ExecutarAsync(grupo, cancellationToken));
}
