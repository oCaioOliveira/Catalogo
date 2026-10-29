using System.Text.Json.Nodes;
using Catalogo.Application.CasosDeUso;
using Catalogo.Application.CasosDeUso.Aprovacao;
using Catalogo.Application.CasosDeUso.Historico;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Registros de uma entidade. Controller fino: cada ação só chama um caso de uso.
/// Erros viram ProblemDetails no TratadorExcecoesGlobal (sem try/catch aqui).
/// </summary>
[ApiController]
[Route(RotasApi.V1 + "/grupos/{grupo}/entidades/{entidade}/registros")]
[Produces("application/json")]
public sealed class RegistrosController(
    ListarRegistros listarRegistros,
    ExportarRegistros exportarRegistros,
    CriarRegistro criarRegistro,
    ObterRegistro obterRegistro,
    ListarRelacionados listarRelacionados,
    AtualizarRegistro atualizarRegistro,
    ExcluirRegistro excluirRegistro,
    ListarHistoricoRegistro listarHistorico,
    DecidirAprovacao decidirAprovacao,
    ILogger<RegistrosController> logger) : ControllerBase
{
    /// <summary>
    /// Lista os registros, paginados (RN17), com filtros (RN18) e ordenação (RN19).
    /// Filtros: chave=valor, chave.contem=valor, chave.de=valor, chave.ate=valor. A chave pode ser um caminho por referência,
    /// até 2 saltos (RN89): relatorio_teste.tipo_teste=Carga, relatorio_teste.data_execucao.de=2026-01-01.
    /// </summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="pagina">Página, a partir de 1 (padrão 1).</param>
    /// <param name="tamanho">Itens por página (padrão 20, máximo 100).</param>
    /// <param name="ordenacao">Chave do campo, criadoEm ou atualizadoEm; "-" na frente = decrescente (padrão -criadoEm).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<PaginaDto<RegistroDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaDto<RegistroDto>>> Listar(
        string grupo, string entidade,
        // Declarados só para aparecerem no Swagger: a leitura de todos os parâmetros (inclusive os
        // filtros, que dependem da entidade) é feita por ParametrosDaUrl.
        [FromQuery] string? pagina, [FromQuery] string? tamanho, [FromQuery] string? ordenacao,
        CancellationToken cancellationToken)
    {
        return Ok(await listarRegistros.ExecutarAsync(grupo, entidade, ParametrosDaUrl.Ler(Request), cancellationToken));
    }

    /// <summary>
    /// Exporta a listagem em CSV ou XLSX, com os MESMOS filtros, ordenação e regras de visibilidade da listagem
    /// (sem paginação). Colunas: os campos ativos na ordem dos metadados (referências pelo rótulo) e, no fim, equipe,
    /// situação de aprovação (se ativa), médias das avaliações (se há modelo ativo), criado por, criado em e atualizado em.
    /// Acima do limite de linhas (padrão 10.000): 400 com codigo EXPORTACAO_MUITO_GRANDE.
    /// O arquivo se chama &lt;entidade&gt;_&lt;aaaa-mm-dd&gt;.&lt;formato&gt; (cabeçalho Content-Disposition).
    /// </summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="formato">csv (UTF-8 com BOM, separador ";" e vírgula decimal) ou xlsx.</param>
    /// <param name="ordenacao">Igual à da listagem.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("exportar")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Exportar(
        string grupo, string entidade,
        // Declarados só para aparecerem no Swagger: a leitura (inclusive dos filtros) é feita por ParametrosDaUrl.
        [FromQuery] string? formato, [FromQuery] string? ordenacao, CancellationToken cancellationToken)
    {
        var arquivo = await exportarRegistros.ExecutarAsync(grupo, entidade, ParametrosDaUrl.Ler(Request), cancellationToken);

        logger.LogInformation("Exportação {Arquivo} de {Grupo}/{Entidade}", arquivo.NomeArquivo, grupo, entidade);
        return new ArquivoExportadoResult(arquivo);
    }

    /// <summary>
    /// Cria um registro. O corpo é um objeto plano com os campos da entidade
    /// (referências como o id do registro apontado).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<RegistroDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistroDto>> Criar(
        string grupo, string entidade, [FromBody] JsonObject dados, CancellationToken cancellationToken)
    {
        var registro = await criarRegistro.ExecutarAsync(grupo, entidade, dados, cancellationToken);

        logger.LogInformation("Registro {RegistroId} criado em {Grupo}/{Entidade}", registro.Id, grupo, entidade);

        // 201 com o header Location apontando para o GET abaixo.
        return CreatedAtAction(nameof(Obter), new { grupo, entidade, id = registro.Id }, registro);
    }

    /// <summary>Busca um registro pelo id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RegistroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegistroDto>> Obter(
        string grupo, string entidade, Guid id, CancellationToken cancellationToken)
    {
        return Ok(await obterRegistro.ExecutarAsync(grupo, entidade, id, cancellationToken));
    }

    /// <summary>
    /// Os itens relacionados ao registro (RN94): uma entrada por campo REFERENCIA ativo, de entidade ativa do grupo, que aponta para
    /// esta entidade, com a quantidade de itens que o usuário vê (mesma visibilidade da listagem; zero também vem). Os itens de cada
    /// entrada vêm da listagem filtrada: GET .../entidades/{entidade.chave}/registros?{campo.chave}={id}. Registro que o usuário
    /// não vê: 404, como o GET dele.
    /// </summary>
    [HttpGet("{id:guid}/relacionados")]
    [ProducesResponseType<IReadOnlyList<RelacionadoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RelacionadoDto>>> Relacionados(
        string grupo, string entidade, Guid id, CancellationToken cancellationToken)
    {
        return Ok(await listarRelacionados.ExecutarAsync(grupo, entidade, id, cancellationToken));
    }

    /// <summary>
    /// Substitui o registro inteiro (RN15). O corpo traz todos os campos e a "versao" devolvida pelo
    /// GET (RN12). Campo opcional ausente = campo removido.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<RegistroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistroDto>> Atualizar(
        string grupo, string entidade, Guid id, [FromBody] JsonObject dados, CancellationToken cancellationToken)
    {
        var registro = await atualizarRegistro.ExecutarAsync(grupo, entidade, id, dados, cancellationToken);

        logger.LogInformation("Registro {RegistroId} atualizado em {Grupo}/{Entidade}", id, grupo, entidade);
        return Ok(registro);
    }

    /// <summary>Exclui o registro, aplicando CASCATA e RESTRINGIR (RN16). 409 lista o que impede a exclusão.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(string grupo, string entidade, Guid id, CancellationToken cancellationToken)
    {
        await excluirRegistro.ExecutarAsync(grupo, entidade, id, cancellationToken);

        logger.LogInformation("Registro {RegistroId} excluído em {Grupo}/{Entidade}", id, grupo, entidade);
        return NoContent();
    }

    /// <summary>
    /// Aprova ou rejeita um registro PENDENTE (RN36): { "decisao": "APROVAR" | "REJEITAR", "justificativa": "..." }.
    /// Só o GESTOR da equipe do registro ou o ADMINISTRADOR. Justificativa de 10 a 1000 caracteres.
    /// </summary>
    [HttpPost("{id:guid}/aprovacao")]
    [Consumes("application/json")]
    [ProducesResponseType<RegistroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistroDto>> Decidir(
        string grupo, string entidade, Guid id, [FromBody] DecisaoEntrada entrada, CancellationToken cancellationToken)
    {
        var registro = await decidirAprovacao.ExecutarAsync(grupo, entidade, id, entrada, cancellationToken);
        logger.LogInformation("Registro {RegistroId} em {Grupo}/{Entidade}: {Situacao}", id, grupo, entidade, registro.SituacaoAprovacao);
        return Ok(registro);
    }

    /// <summary>
    /// Histórico do registro, do mais recente para o mais antigo, com as mudanças por campo (RN32).
    /// Quem vê o registro vê o histórico; funciona também para registro já excluído.
    /// </summary>
    [HttpGet("{id:guid}/historico")]
    [ProducesResponseType<PaginaDto<HistoricoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaDto<HistoricoDto>>> Historico(
        string grupo, string entidade, Guid id, [FromQuery] string? pagina, [FromQuery] string? tamanho,
        CancellationToken cancellationToken)
    {
        return Ok(await listarHistorico.ExecutarAsync(grupo, entidade, id, ParametrosDaUrl.Ler(Request), cancellationToken));
    }
}
