using System.Diagnostics;
using Catalogo.Api.Configuracao;
using Catalogo.Domain.Excecoes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Erros;

/// <summary>
/// Único lugar que converte exceções em respostas HTTP (ProblemDetails, RFC 9457).
/// Por isso os controllers NÃO têm try/catch. Tabela exceção → status em src/Catalogo.Api/AGENTS.md.
///
/// Toda resposta tem "codigo" (de <see cref="CodigosErro"/>) e "traceId". Dados para o front (dependências que
/// bloqueiam uma exclusão, quantidade de pendentes...) vão em "detalhes" (formatos em docs/api/codigos-de-erro.md).
/// </summary>
internal sealed class TratadorExcecoesGlobal(
    IProblemDetailsService problemDetailsService,
    AmbienteCatalogo ambiente,
    ILogger<TratadorExcecoesGlobal> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problema = CriarProblema(exception);
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (problema.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro inesperado em {Metodo} {Caminho} (traceId {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path, traceId);
        }
        else
        {
            // Erros de negócio são esperados: registra sem stack trace.
            logger.LogInformation("Requisição {Metodo} {Caminho} recusada com {Status} {Codigo}: {Mensagem}",
                httpContext.Request.Method, httpContext.Request.Path, problema.Status, problema.Extensions[ExtensoesProblema.Codigo],
                exception.Message);
        }

        httpContext.Response.StatusCode = problema.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception,
        });
    }

    private ProblemDetails CriarProblema(Exception exception)
    {
        var problema = exception switch
        {
            // 400 com os erros por campo: { "errors": { "codigo_sigla": ["..."], ... } }
            ValidacaoRegistroException validacao => new ValidationProblemDetails(validacao.Erros.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Um ou mais campos são inválidos.",
            },
            NaoAutenticadoException => Problema(StatusCodes.Status401Unauthorized, "Não autenticado.", exception.Message),
            AcessoNegadoException => Problema(StatusCodes.Status403Forbidden, "Acesso negado.", exception.Message),
            RecursoNaoEncontradoException => Problema(StatusCodes.Status404NotFound, "Recurso não encontrado.", exception.Message),
            RegraDeNegocioException => Problema(StatusCodes.Status409Conflict, "Operação não permitida.", exception.Message),
            ConflitoUnicidadeException => Problema(StatusCodes.Status409Conflict, "Registro duplicado.", exception.Message),
            ConflitoConcorrenciaException => Problema(StatusCodes.Status409Conflict, "Conflito de concorrência.", exception.Message),
            ExclusaoBloqueadaException bloqueio => Problema(StatusCodes.Status409Conflict, "Exclusão bloqueada.",
                bloqueio.Message + " " + string.Join("; ", bloqueio.Dependencias.Select(d =>
                    $"{d.Quantidade} registro(s) de '{d.Entidade}' pelo campo '{d.Campo}'")) + "."),
            // Requisição que o Kestrel recusa antes do controller (ex.: corpo grande demais): o status vem nela.
            BadHttpRequestException requisicao => Problema(requisicao.StatusCode, "Requisição inválida.", null),
            _ => ProblemaInterno(exception),
        };

        problema.Extensions[ExtensoesProblema.Codigo] = exception switch
        {
            ExcecaoDominio dominio => dominio.Codigo,
            BadHttpRequestException => CodigosErro.RequisicaoInvalida,
            _ => CodigosErro.ErroInterno,
        };

        if (Detalhes(exception) is { } detalhes)
            problema.Extensions[ExtensoesProblema.Detalhes] = detalhes;

        return problema;
    }

    /// <summary>Dados que o front usa para montar a mensagem ou decidir o que fazer. Formatos documentados.</summary>
    private static object? Detalhes(Exception exception) => exception switch
    {
        // { restricao: "uq_comunidade_nome", campos: ["nome"] } (RN09)
        ConflitoUnicidadeException conflito => new { restricao = conflito.NomeRestricao, campos = conflito.Campos },
        // { dependencias: [{ entidade, campo, quantidade, exemplos }] } (RN16)
        ExclusaoBloqueadaException bloqueio => new { dependencias = bloqueio.Dependencias },
        // Ex.: APROVACOES_PENDENTES → { quantidade: 3 }; MODELO_ATIVO_EXISTENTE → { modeloId: "..." }
        RegraDeNegocioException { Dados.Count: > 0 } regra => regra.Dados,
        // { camposDesatualizados: ["chave"] }: campos desconhecidos ou inativos (RN01, RN02); o front recarrega os metadados.
        ValidacaoRegistroException { CamposDesatualizados.Count: > 0 } validacao => new { camposDesatualizados = validacao.CamposDesatualizados },
        _ => null,
    };

    private ProblemDetails ProblemaInterno(Exception exception)
    {
        var problema = Problema(StatusCodes.Status500InternalServerError, "Erro interno.",
            "Ocorreu um erro inesperado. Informe o traceId ao suporte.");

        // Detalhes técnicos SÓ em LOCAL (ENVIRONMENT): fora dele podem expor informação interna.
        if (ambiente.MostraDetalhesDeErro)
        {
            problema.Detail = exception.Message;
            problema.Extensions["excecao"] = exception.ToString();
        }

        return problema;
    }

    private static ProblemDetails Problema(int status, string titulo, string? detalhe) =>
        new() { Status = status, Title = titulo, Detail = detalhe };
}
