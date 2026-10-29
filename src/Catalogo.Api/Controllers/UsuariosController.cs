using Catalogo.Application.CasosDeUso.Administracao;
using Catalogo.Application.CasosDeUso.Historico;
using Catalogo.Application.Dtos;
using Catalogo.Domain.Historico;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Administração de usuários: só ADMINISTRADOR (403 para os demais). Usuário não é excluído, só
/// desativado. Nenhuma resposta traz senha ou hash, exceto a senha PROVISÓRIA, devolvida uma vez.
/// </summary>
[ApiController]
[Route(RotasApi.V1 + "/usuarios")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class UsuariosController(
    ListarUsuarios listarUsuarios,
    ObterUsuario obterUsuario,
    CriarUsuario criarUsuario,
    AtualizarUsuario atualizarUsuario,
    AlterarAtivacaoUsuario alterarAtivacao,
    RedefinirSenhaUsuario redefinirSenha,
    ListarHistoricoAcesso listarHistorico,
    ILogger<UsuariosController> logger) : ControllerBase
{
    /// <summary>Lista usuários, paginado, com filtros opcionais.</summary>
    /// <param name="equipeId">Só usuários desta equipe.</param>
    /// <param name="perfil">EXTERNO, INTERNO, GESTOR ou ADMINISTRADOR.</param>
    /// <param name="ativo">true ou false.</param>
    /// <param name="busca">Contido na matrícula ou no nome (sem diferenciar maiúsculas nem acentos; até 100 caracteres).</param>
    /// <param name="ordenacao">nome (padrão), matricula, ultimoAcessoEm ou criadoEm; "-" na frente = decrescente.</param>
    /// <param name="pagina">Página, a partir de 1.</param>
    /// <param name="tamanho">Itens por página (máximo 100).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<PaginaDto<UsuarioDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<UsuarioDto>>> Listar(
        // Declarados só para aparecerem no Swagger: a leitura é feita por ParametrosDaUrl.
        [FromQuery] string? equipeId, [FromQuery] string? perfil, [FromQuery] string? ativo,
        [FromQuery] string? busca, [FromQuery] string? ordenacao, [FromQuery] string? pagina, [FromQuery] string? tamanho, CancellationToken cancellationToken) =>
        Ok(await listarUsuarios.ExecutarAsync(ParametrosDaUrl.Ler(Request), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await obterUsuario.ExecutarAsync(id, cancellationToken));

    /// <summary>Cria o usuário com uma senha provisória gerada pelo sistema, devolvida só nesta resposta.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<UsuarioCriadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioCriadoDto>> Criar([FromBody] UsuarioEntrada entrada, CancellationToken cancellationToken)
    {
        var criado = await criarUsuario.ExecutarAsync(entrada, cancellationToken);
        logger.LogInformation("Usuário {Matricula} criado com perfil {Perfil}", criado.Usuario.Matricula, criado.Usuario.Perfil);
        return CreatedAtAction(nameof(Obter), new { id = criado.Usuario.Id }, criado);
    }

    /// <summary>Altera nome, perfil e equipes. A matrícula não muda.</summary>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Atualizar(Guid id, [FromBody] UsuarioEntrada entrada, CancellationToken cancellationToken) =>
        Ok(await atualizarUsuario.ExecutarAsync(id, entrada, cancellationToken));

    /// <summary>Ativa ou desativa: { "ativo": false }.</summary>
    [HttpPatch("{id:guid}/ativacao")]
    [Consumes("application/json")]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UsuarioDto>> AlterarAtivacao(
        Guid id, [FromBody] AtivacaoUsuarioEntrada entrada, CancellationToken cancellationToken) =>
        Ok(await alterarAtivacao.ExecutarAsync(id, entrada, cancellationToken));

    /// <summary>Gera uma nova senha provisória (devolvida só nesta resposta) e obriga a troca.</summary>
    [HttpPost("{id:guid}/redefinir-senha")]
    [ProducesResponseType<SenhaProvisoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SenhaProvisoriaDto>> RedefinirSenha(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await redefinirSenha.ExecutarAsync(id, cancellationToken);
        logger.LogInformation("Senha do usuário {UsuarioId} redefinida", id);
        return Ok(resultado);
    }

    /// <summary>Histórico do usuário (sem senhas), do mais recente para o mais antigo (RN32).</summary>
    [HttpGet("{id:guid}/historico")]
    [ProducesResponseType<PaginaDto<HistoricoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<HistoricoDto>>> Historico(
        Guid id, [FromQuery] string? pagina, [FromQuery] string? tamanho, CancellationToken cancellationToken) =>
        Ok(await listarHistorico.ExecutarAsync(AlvoHistorico.Usuario, id, ParametrosDaUrl.Ler(Request), cancellationToken));
}
