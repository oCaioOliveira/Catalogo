using Catalogo.Application.CasosDeUso.Metadados;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Administração de metadados: campos e dependências (RN50 a RN55, RN59 a RN70, RN73). Só ADMINISTRADOR. Toda escrita leva a
/// "versao" da ENTIDADE e aceita <c>?simular=true</c>. Mudanças que deixariam registros inválidos: 409
/// ALTERACAO_INCOMPATIVEL_COM_DADOS, com o relatório de impacto em "detalhes".
/// </summary>
[ApiController]
[Route(RotasApi.AdminMetadados + "/grupos/{grupo}/entidades/{entidade}/campos")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class AdminCamposController(
    CriarCampo criarCampo,
    AtualizarCampo atualizarCampo,
    AlterarAtivacaoCampo alterarAtivacao,
    ExcluirCampo excluirCampo,
    ReordenarCampos reordenarCampos,
    DefinirDependencia definirDependencia,
    ExcluirDependencia excluirDependencia,
    ILogger<AdminCamposController> logger) : ControllerBase
{
    /// <summary>
    /// Cria um campo: { versao, chave, nome, tipoDado, obrigatorio, tamanhoMaximo, precisao, escala, opcoes, entidadeReferenciada,
    /// acaoExclusao, ordem, valorPadrao }. Obrigatório numa entidade com registros: informe "valorPadrao", gravado em todos (RN60).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Criar(
        string grupo, string entidade, [FromBody] CampoEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken)
    {
        var resultado = await criarCampo.ExecutarAsync(grupo, entidade, entrada, simular, cancellationToken);
        if (resultado.Simulado)
            return Ok(resultado);

        logger.LogInformation("Campo {Campo} criado em {Grupo}/{Entidade}", entrada.Chave, grupo, entidade);
        return CreatedAtAction(nameof(AdminGruposController.Obter), "AdminGrupos", new { grupo }, resultado);
    }

    /// <summary>
    /// Reordena TODOS os campos de uma vez: { versao, campos: ["chave", ...] } com todos os campos, ativos e inativos (RN53).
    /// </summary>
    [HttpPut("ordem")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Reordenar(
        string grupo, string entidade, [FromBody] OrdemCamposEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await reordenarCampos.ExecutarAsync(grupo, entidade, entrada, simular, cancellationToken));

    /// <summary>
    /// Altera o campo. Chave, entidade referenciada e tipo não mudam (exceto TEXTO → TEXTO_LONGO, RN51, RN66). Tornar obrigatório,
    /// diminuir tamanho, precisão ou escala e remover opção são conferidos contra os dados (RN61 a RN68).
    /// </summary>
    [HttpPut("{campo}")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Atualizar(
        string grupo, string entidade, string campo, [FromBody] CampoEntrada entrada, [FromQuery] bool simular,
        CancellationToken cancellationToken) =>
        Ok(await atualizarCampo.ExecutarAsync(grupo, entidade, campo, entrada, simular, cancellationToken));

    /// <summary>
    /// { "versao", "ativo": false } desativa: bloqueado se o campo é de exibição, de restrição única ou de dependência ativa. Os
    /// valores ficam guardados e voltam ao reativar (RN69).
    /// </summary>
    [HttpPatch("{campo}/ativacao")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> AlterarAtivacao(
        string grupo, string entidade, string campo, [FromBody] AtivacaoMetadadoEntrada entrada, [FromQuery] bool simular,
        CancellationToken cancellationToken) =>
        Ok(await alterarAtivacao.ExecutarAsync(grupo, entidade, campo, entrada, simular, cancellationToken));

    /// <summary>Exclui o campo, só se nenhum registro tem valor nele e ele não é usado (RN70). Senão: desative-o.</summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="campo">Chave do campo.</param>
    /// <param name="versao">A "versao" da entidade (URL-encoded).</param>
    /// <param name="simular">true: só valida, sem excluir.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpDelete("{campo}")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Excluir(
        string grupo, string entidade, string campo, [FromQuery] string? versao, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await excluirCampo.ExecutarAsync(grupo, entidade, campo, versao, simular, cancellationToken));

    /// <summary>
    /// Cria ou substitui a dependência do campo REFERENCIA (RN08): { versao, campoVia, campoOrigem, campoAlvo }. Bloqueada se algum
    /// registro já viola a regra (RN73).
    /// </summary>
    [HttpPut("{campo}/dependencia")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> DefinirDependencia(
        string grupo, string entidade, string campo, [FromBody] DependenciaEntrada entrada, [FromQuery] bool simular,
        CancellationToken cancellationToken) =>
        Ok(await definirDependencia.ExecutarAsync(grupo, entidade, campo, entrada, simular, cancellationToken));

    /// <summary>Remove a dependência do campo (RN73).</summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="campo">Chave do campo.</param>
    /// <param name="versao">A "versao" da entidade (URL-encoded).</param>
    /// <param name="simular">true: só valida, sem remover.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpDelete("{campo}/dependencia")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> ExcluirDependencia(
        string grupo, string entidade, string campo, [FromQuery] string? versao, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await excluirDependencia.ExecutarAsync(grupo, entidade, campo, versao, simular, cancellationToken));
}
