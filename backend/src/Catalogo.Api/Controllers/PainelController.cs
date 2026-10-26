using Catalogo.Application.CasosDeUso.Painel;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Painel gerencial (RN78 a RN88, docs/api/painel.md): só GESTOR e ADMINISTRADOR; INTERNO e EXTERNO recebem 403. Os números
/// respeitam a mesma visibilidade da listagem. Controller fino: cada ação só chama um caso de uso.
/// </summary>
[ApiController]
[Route(RotasApi.V1 + "/painel/grupos/{grupo}")]
[Produces("application/json")]
public sealed class PainelController(
    ObterVisaoGeral obterVisaoGeral,
    CalcularIndicadores calcularIndicadores,
    ExportarIndicadores exportarIndicadores,
    ILogger<PainelController> logger) : ControllerBase
{
    /// <summary>
    /// Visão geral do grupo: volume (por entidade, por equipe, criados por mês, criados e alterados no período), aprovação
    /// (onde está ativa), avaliação (onde há modelo ativo: médias, distribuição e ranking) e os usuários mais ativos.
    /// </summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="de">Início do período (aaaa-mm-dd, no fuso de negócio, inclusivo).</param>
    /// <param name="ate">Fim do período (aaaa-mm-dd, no fuso de negócio, inclusivo).</param>
    /// <param name="equipeId">Só os registros de uma equipe (as entidades globais continuam). GESTOR: só das equipes dele.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("visao-geral")]
    [ProducesResponseType<VisaoGeralDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisaoGeralDto>> VisaoGeral(
        string grupo,
        // Declarados só para aparecerem no Swagger: a leitura é feita por ParametrosDaUrl.
        [FromQuery] string? de, [FromQuery] string? ate, [FromQuery] string? equipeId,
        CancellationToken cancellationToken)
    {
        return Ok(await obterVisaoGeral.ExecutarAsync(grupo, ParametrosDaUrl.Ler(Request), cancellationToken));
    }

    /// <summary>
    /// Agregação configurável de uma entidade: até 2 dimensões e 5 métricas, com os mesmos filtros da listagem (inclusive por caminho).
    /// Ex.: agruparPor=relatorio_teste.ambiente,relatorio_teste.data_execucao:mes&amp;metricas=media:vazao_media_tps&amp;relatorio_teste.tipo_teste=Carga.
    /// </summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="agruparPor">Até 2 dimensões, separadas por vírgula: campo LISTA, BOOLEANO, REFERENCIA ou TEXTO; campo DATA/DATA_HORA com :dia, :mes, :trimestre ou :ano; equipe; situacaoAprovacao; criadoEm:&lt;período&gt;; um caminho "referencia.campo" ou "referencia.referencia.campo" (campo de um registro apontado, até 2 saltos). Sem agruparPor: uma linha com o total (sempre uma, mesmo sem registros).</param>
    /// <param name="metricas">Até 5: contagem (padrão), media:campo, soma:campo, minimo:campo, maximo:campo (INTEIRO e DECIMAL; o campo também por caminho, ex.: media:relatorio_teste.duracao) e media_avaliacao:criterio.</param>
    /// <param name="ordenarPor">Chave de uma dimensão ou métrica; "-" na frente = decrescente. Padrão: pelas dimensões.</param>
    /// <param name="limite">Máximo de linhas (padrão 100, máximo 500). Se havia mais, "truncado" vem true.</param>
    /// <param name="de">Criados a partir desta data (aaaa-mm-dd, fuso de negócio).</param>
    /// <param name="ate">Criados até esta data (aaaa-mm-dd, fuso de negócio, inclusiva).</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("entidades/{entidade}/indicadores")]
    [ProducesResponseType<IndicadoresDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IndicadoresDto>> Indicadores(
        string grupo, string entidade,
        // Declarados só para aparecerem no Swagger: a leitura (inclusive dos filtros da listagem) é feita por ParametrosDaUrl.
        [FromQuery] string? agruparPor, [FromQuery] string? metricas, [FromQuery] string? ordenarPor, [FromQuery] string? limite,
        [FromQuery] string? de, [FromQuery] string? ate,
        CancellationToken cancellationToken)
    {
        return Ok(await calcularIndicadores.ExecutarAsync(grupo, entidade, ParametrosDaUrl.Ler(Request), cancellationToken));
    }

    /// <summary>
    /// O resultado da agregação em CSV ou XLSX: os mesmos parâmetros e as mesmas linhas de .../indicadores. O arquivo se chama
    /// &lt;entidade&gt;_indicadores_&lt;aaaa-mm-dd&gt;.&lt;formato&gt; (cabeçalho Content-Disposition).
    /// </summary>
    /// <param name="grupo">Chave do grupo.</param>
    /// <param name="entidade">Chave da entidade.</param>
    /// <param name="formato">csv (UTF-8 com BOM, separador ";" e vírgula decimal) ou xlsx.</param>
    /// <param name="agruparPor">Igual ao de .../indicadores.</param>
    /// <param name="metricas">Igual ao de .../indicadores.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    [HttpGet("entidades/{entidade}/indicadores/exportar")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportarIndicadores(
        string grupo, string entidade,
        // Declarados só para aparecerem no Swagger: a leitura é feita por ParametrosDaUrl.
        [FromQuery] string? formato, [FromQuery] string? agruparPor, [FromQuery] string? metricas,
        CancellationToken cancellationToken)
    {
        var arquivo = await exportarIndicadores.ExecutarAsync(grupo, entidade, ParametrosDaUrl.Ler(Request), cancellationToken);

        logger.LogInformation("Exportação de indicadores {Arquivo} de {Grupo}/{Entidade}", arquivo.NomeArquivo, grupo, entidade);
        return new ArquivoExportadoResult(arquivo);
    }
}
