using Catalogo.Application.CasosDeUso.Importacoes;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Importação de itens por planilha (XLSX ou CSV) num tipo de item (RN97 a RN104). Fluxo: baixar o modelo → enviar o arquivo (devolve
/// a sugestão de mapeamento) → validar (a prévia, sem gravar) → executar → (opcional) desfazer. Controller fino: cada ação só chama
/// um caso de uso; as regras são as mesmas do cadastro manual.
/// </summary>
[ApiController]
[Route(RotasApi.V1 + "/grupos/{grupo}/entidades/{entidade}")]
[Produces("application/json")]
public sealed class ImportacoesController(
    ObterModeloImportacao obterModelo,
    EnviarImportacao enviar,
    ListarImportacoes listar,
    ObterImportacao obter,
    ValidarImportacao validar,
    ExecutarImportacao executar,
    BaixarRelatorioImportacao baixarRelatorio,
    DesfazerImportacao desfazer,
    ILogger<ImportacoesController> logger) : ControllerBase
{
    /// <summary>
    /// Limite do corpo do envio. O limite do ARQUIVO (5 MB) é conferido pelo caso de uso, com uma mensagem clara; acima deste, o
    /// próprio servidor recusa.
    /// </summary>
    private const long LimiteRequisicao = 12 * 1024 * 1024;

    /// <summary>
    /// O modelo XLSX do tipo de item: os campos ativos na ordem do formulário ("*" nos obrigatórios), listas suspensas (LISTA, BOOLEANO
    /// e referências com até 1.000 itens visíveis), formatos de data e número e a aba "Instruções". Só para quem pode criar itens aqui.
    /// </summary>
    [HttpGet("importacao/modelo")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Modelo(string grupo, string entidade, CancellationToken cancellationToken)
    {
        return new ArquivoExportadoResult(await obterModelo.ExecutarAsync(grupo, entidade, cancellationToken));
    }

    /// <summary>
    /// Envia o arquivo (multipart/form-data, campo "arquivo"): até 5 MB, 5.000 linhas e 100 colunas; o tipo é conferido pelo conteúdo.
    /// Guarda o arquivo e devolve as colunas, com até 3 valores de exemplo e a sugestão de mapeamento (o nome ou a chave do campo, sem
    /// diferenciar maiúsculas e acentos; "&lt;campo&gt; (ID)" para o identificador de uma referência; "Equipe"). Nada é gravado ainda.
    /// </summary>
    [HttpPost("importacoes")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteRequisicao)]
    [ProducesResponseType<ImportacaoEnviadaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ImportacaoEnviadaDto>> Enviar(
        string grupo, string entidade, IFormFile? arquivo, CancellationToken cancellationToken)
    {
        var conteudo = await LerConteudoAsync(arquivo, cancellationToken);

        var enviada = await enviar.ExecutarAsync(grupo, entidade, arquivo?.FileName, conteudo, cancellationToken);
        logger.LogInformation("Importação {ImportacaoId} enviada em {Grupo}/{Entidade}: {Linhas} linhas", enviada.Id, grupo, entidade, enviada.TotalLinhas);
        return CreatedAtAction(nameof(Obter), new { grupo, entidade, id = enviada.Id }, enviada);
    }

    /// <summary>
    /// As importações deste tipo de item, das mais recentes para as mais antigas, paginadas: as próprias; para o GESTOR, também as das
    /// suas equipes; para o ADMINISTRADOR, todas.
    /// </summary>
    [HttpGet("importacoes")]
    [ProducesResponseType<PaginaDto<ImportacaoResumoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaDto<ImportacaoResumoDto>>> Listar(
        string grupo, string entidade, [FromQuery] string? pagina, [FromQuery] string? tamanho, CancellationToken cancellationToken)
    {
        return Ok(await listar.ExecutarAsync(grupo, entidade, ParametrosDaUrl.Ler(Request), cancellationToken));
    }

    /// <summary>
    /// A prévia (ou o resultado, depois de executar) já calculada, com as linhas paginadas. somenteComErro=true mostra só as linhas
    /// com erro.
    /// </summary>
    [HttpGet("importacoes/{id:guid}")]
    [ProducesResponseType<PreviaImportacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PreviaImportacaoDto>> Obter(
        string grupo, string entidade, Guid id, [FromQuery] string? pagina, [FromQuery] string? tamanho,
        [FromQuery] string? somenteComErro, CancellationToken cancellationToken)
    {
        return Ok(await obter.ExecutarAsync(grupo, entidade, id, ParametrosDaUrl.Ler(Request), cancellationToken));
    }

    /// <summary>
    /// A prévia: recebe o mapeamento (coluna → destino) e as opções (duplicados PULAR ou ATUALIZAR; linhas com erro SO_VALIDAS ou
    /// TUDO_OU_NADA; equipe padrão) e simula cada linha com as mesmas regras do cadastro manual, sem gravar nenhum item. Devolve os
    /// totais (novos, atualizações, ignorados, com erro) e as linhas, paginadas, com os erros por coluna e as referências resolvidas.
    /// </summary>
    [HttpPost("importacoes/{id:guid}/validar")]
    [Consumes("application/json")]
    [ProducesResponseType<PreviaImportacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PreviaImportacaoDto>> Validar(
        string grupo, string entidade, Guid id, [FromBody] ValidarImportacaoEntrada entrada,
        [FromQuery] string? pagina, [FromQuery] string? tamanho, [FromQuery] string? somenteComErro, CancellationToken cancellationToken)
    {
        return Ok(await validar.ExecutarAsync(grupo, entidade, id, entrada, ParametrosDaUrl.Ler(Request), cancellationToken));
    }

    /// <summary>
    /// Executa exatamente o que a prévia mostrou, numa única transação, pelo mesmo caminho de gravação do cadastro manual. Se os dados
    /// mudaram desde a prévia: nada é gravado, a prévia é refeita e a resposta é 409 IMPORTACAO_DADOS_MUDARAM.
    /// </summary>
    [HttpPost("importacoes/{id:guid}/executar")]
    [ProducesResponseType<ImportacaoResumoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ImportacaoResumoDto>> Executar(string grupo, string entidade, Guid id, CancellationToken cancellationToken)
    {
        var resumo = await executar.ExecutarAsync(grupo, entidade, id, cancellationToken);
        logger.LogInformation("Importação {ImportacaoId} executada em {Grupo}/{Entidade}", id, grupo, entidade);
        return Ok(resumo);
    }

    /// <summary>O relatório XLSX: as linhas originais e as colunas "Resultado" e "Mensagens".</summary>
    [HttpGet("importacoes/{id:guid}/relatorio")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Relatorio(string grupo, string entidade, Guid id, CancellationToken cancellationToken)
    {
        return new ArquivoExportadoResult(await baixarRelatorio.ExecutarAsync(grupo, entidade, id, cancellationToken));
    }

    /// <summary>
    /// Desfaz a importação: exclui os itens CRIADOS por ela (as atualizações ficam), se nenhum foi alterado depois nem é referenciado por
    /// outro item. Senão, 409 IMPORTACAO_NAO_PODE_SER_DESFEITA com os motivos e os itens que impedem. Só quem pode excluir os itens.
    /// </summary>
    [HttpPost("importacoes/{id:guid}/desfazer")]
    [ProducesResponseType<ImportacaoResumoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ImportacaoResumoDto>> Desfazer(string grupo, string entidade, Guid id, CancellationToken cancellationToken)
    {
        var resumo = await desfazer.ExecutarAsync(grupo, entidade, id, cancellationToken);
        logger.LogInformation("Importação {ImportacaoId} desfeita em {Grupo}/{Entidade}", id, grupo, entidade);
        return Ok(resumo);
    }

    private static Task<byte[]?> LerConteudoAsync(IFormFile? arquivo, CancellationToken cancellationToken) =>
        arquivo is null
            ? Task.FromResult<byte[]?>(null)
            : LerConteudoDeArquivoAsync(arquivo, cancellationToken);

    private static async Task<byte[]?> LerConteudoDeArquivoAsync(IFormFile arquivo, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }
}
