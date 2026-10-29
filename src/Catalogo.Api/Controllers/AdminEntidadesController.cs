using Catalogo.Application.CasosDeUso.Metadados;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Administração de metadados: entidades (tipos de item) e restrições únicas (RN50 a RN55, RN71, RN72, RN74 a RN76). Só
/// ADMINISTRADOR. Toda escrita leva a "versao" (do grupo, ao criar; da entidade, nas demais) e aceita <c>?simular=true</c>.
/// </summary>
[ApiController]
[Route(RotasApi.AdminMetadados + "/grupos/{grupo}/entidades")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class AdminEntidadesController(
    CriarEntidade criarEntidade,
    AtualizarEntidade atualizarEntidade,
    AlterarAtivacaoEntidade alterarAtivacao,
    ExcluirEntidade excluirEntidade,
    CriarRestricao criarRestricao,
    ExcluirRestricao excluirRestricao,
    ILogger<AdminEntidadesController> logger) : ControllerBase
{
    /// <summary>
    /// Cria uma entidade, ativa e sem campos, com a "versao" do GRUPO: { versao, chave, nome, descricao, ordem, escopoEquipe }.
    /// escopoEquipe: true (padrão) = registros por equipe; false = globais (RN27).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Criar(
        string grupo, [FromBody] EntidadeEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken)
    {
        var resultado = await criarEntidade.ExecutarAsync(grupo, entrada, simular, cancellationToken);
        return resultado.Simulado
            ? Ok(resultado)
            : Criada(grupo, entrada.Chave ?? string.Empty, resultado);
    }

    /// <summary>
    /// Altera nome, descrição, ordem, campo de exibição (chave de um campo ativo, não REFERENCIA nem TEXTO_LONGO) e escopo, com a
    /// "versao" da entidade. Mudar o escopo só sem registros nem configuração de equipe (RN74). A chave não muda (RN51).
    /// </summary>
    [HttpPut("{entidade}")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Atualizar(
        string grupo, string entidade, [FromBody] EntidadeEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await atualizarEntidade.ExecutarAsync(grupo, entidade, entrada, simular, cancellationToken));

    /// <summary>
    /// { "versao", "ativo": false } desativa: some para os não-administradores e fica somente leitura para o administrador; as
    /// referências a ela continuam, sem aceitar vínculos novos (RN75).
    /// </summary>
    [HttpPatch("{entidade}/ativacao")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> AlterarAtivacao(
        string grupo, string entidade, [FromBody] AtivacaoMetadadoEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await alterarAtivacao.ExecutarAsync(grupo, entidade, entrada, simular, cancellationToken));

    /// <summary>Exclui a entidade, só se nunca teve registros e não é referenciada (RN76). Senão: desative-a.</summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="versao">A "versao" da entidade (URL-encoded).</param>
    /// <param name="simular">true: só valida, sem excluir.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpDelete("{entidade}")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> Excluir(
        string grupo, string entidade, [FromQuery] string? versao, [FromQuery] bool simular, CancellationToken cancellationToken) =>
        Ok(await excluirEntidade.ExecutarAsync(grupo, entidade, versao, simular, cancellationToken));

    /// <summary>
    /// Cria uma restrição única: { versao, nome, campos: ["chave", ...] } (1 a 5 campos ativos, sem TEXTO_LONGO). Bloqueada se os
    /// registros já têm duplicatas (o relatório lista os grupos); se passar, gera as chaves de todos os registros (RN71).
    /// </summary>
    [HttpPost("{entidade}/restricoes")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> CriarRestricao(
        string grupo, string entidade, [FromBody] RestricaoEntrada entrada, [FromQuery] bool simular, CancellationToken cancellationToken)
    {
        var resultado = await criarRestricao.ExecutarAsync(grupo, entidade, entrada, simular, cancellationToken);
        return resultado.Simulado ? Ok(resultado) : CreatedAtAction(nameof(AdminGruposController.Obter), "AdminGrupos", new { grupo }, resultado);
    }

    private CreatedAtActionResult Criada(string grupo, string chaveEntidade, ResultadoAdministracaoDto resultado)
    {
        logger.LogInformation("Entidade {Entidade} criada no grupo {Grupo}", chaveEntidade, grupo);
        return CreatedAtAction(nameof(AdminGruposController.Obter), "AdminGrupos", new { grupo }, resultado);
    }

    /// <summary>Remove a restrição única e as chaves dela (RN72).</summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="restricao">Nome da restrição.</param>
    /// <param name="versao">A "versao" da entidade (URL-encoded).</param>
    /// <param name="simular">true: só valida, sem remover.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpDelete("{entidade}/restricoes/{restricao}")]
    [ProducesResponseType<ResultadoAdministracaoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoAdministracaoDto>> ExcluirRestricao(
        string grupo, string entidade, string restricao, [FromQuery] string? versao, [FromQuery] bool simular,
        CancellationToken cancellationToken) =>
        Ok(await excluirRestricao.ExecutarAsync(grupo, entidade, restricao, versao, simular, cancellationToken));
}
