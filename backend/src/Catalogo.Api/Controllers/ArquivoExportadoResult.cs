using Catalogo.Application.CasosDeUso;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Catalogo.Api.Controllers;

/// <summary>
/// Escreve um <see cref="ArquivoExportado"/> direto na resposta, como download (Content-Disposition: attachment),
/// enquanto as linhas são lidas do banco. As validações (permissão, filtros, limite) já foram feitas pelo caso
/// de uso ANTES daqui: um erro depois que o arquivo começou a sair não vira mais ProblemDetails.
/// </summary>
internal sealed class ArquivoExportadoResult(ArquivoExportado arquivo) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var resposta = context.HttpContext.Response;
        resposta.StatusCode = StatusCodes.Status200OK;
        resposta.ContentType = arquivo.TipoConteudo;
        resposta.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = arquivo.NomeArquivo,
            FileNameStar = arquivo.NomeArquivo,
        }.ToString();

        await arquivo.EscreverAsync(resposta.Body, context.HttpContext.RequestAborted);
    }
}
