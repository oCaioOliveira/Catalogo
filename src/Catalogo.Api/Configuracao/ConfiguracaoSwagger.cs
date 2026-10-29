using System.Reflection;
using System.Text.Json.Nodes;
using Catalogo.Api.Controllers;
using Catalogo.Api.Erros;
using Catalogo.Domain.Excecoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Geração do documento OpenAPI com Swashbuckle (no .NET 8 a geração nativa ainda não existe).
/// O Swagger UI só é publicado em Development (ver Program.cs). O documento gerado é o CONTRATO da API:
/// fica salvo em backend/openapi/v1.json e um teste falha se ele mudar sem querer (docs/adr/0024).
/// </summary>
public static class ConfiguracaoSwagger
{
    public const string Documento = "v1";

    /// <summary>
    /// Respostas que TODO endpoint pode dar e que não dependem da ação: 401 (sem token ou sessão inválida),
    /// 403 (troca de senha obrigatória, perfil sem permissão) e 500. Cada ação declara as demais.
    /// </summary>
    public static void DeclararRespostasComuns(MvcOptions opcoes)
    {
        opcoes.Filters.Add(new ProducesResponseTypeAttribute<ProblemDetails>(StatusCodes.Status401Unauthorized));
        opcoes.Filters.Add(new ProducesResponseTypeAttribute<ProblemDetails>(StatusCodes.Status403Forbidden));
        opcoes.Filters.Add(new ProducesResponseTypeAttribute<ProblemDetails>(StatusCodes.Status500InternalServerError));
    }

    public static IServiceCollection AdicionarSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opcoes =>
        {
            opcoes.SwaggerDoc(Documento, new OpenApiInfo
            {
                Title = "Catalogo API",
                Version = Documento,
                Description = "Cadastro genérico guiado por metadados. Guia de uso em docs/api/README.md; " +
                              "códigos de erro em docs/api/codigos-de-erro.md; regras em docs/regras-de-negocio.md.",
            });

            // Os <summary> dos controllers viram as descrições dos endpoints.
            opcoes.IncludeXmlComments(Assembly.GetExecutingAssembly());

            // Os dados de um registro são um objeto JSON livre: as chaves são as "chave" dos campos da entidade
            // (consulte GET /api/v1/grupos/{grupo}/entidades para saber quais são).
            opcoes.MapType<JsonObject>(() => new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalPropertiesAllowed = true,
                Description = "Objeto com propriedades livres: as chaves são as chaves dos campos da entidade (GET .../entidades).",
            });

            opcoes.SchemaFilter<EsquemaDosErros>();
            opcoes.SchemaFilter<ExemploDeRegistro>();
            opcoes.OperationFilter<RespostasDeErro>();
            opcoes.OperationFilter<ExemploDeCorpoDeRegistro>();

            // Botão "Authorize" do Swagger UI: cole o token devolvido por POST /api/v1/auth/login.
            opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = $"Token devolvido por POST /{RotasApi.V1}/auth/login.",
            });
            opcoes.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", documento)] = [],
            });
        });

        return services;
    }

    /// <summary>"codigo", "traceId" e "detalhes" nos esquemas de erro (docs/api/codigos-de-erro.md).</summary>
    private sealed class EsquemaDosErros : ISchemaFilter
    {
        private static readonly JsonNode[] Codigos = typeof(CodigosErro)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(c => c.IsLiteral)
            .Select(c => (JsonNode)JsonValue.Create((string)c.GetRawConstantValue()!)!)
            .ToArray();

        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (!typeof(ProblemDetails).IsAssignableFrom(context.Type) || schema is not OpenApiSchema esquema)
                return;

            esquema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            esquema.Properties[ExtensoesProblema.Codigo] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Código estável do erro. É por ele que o front decide o que fazer (docs/api/codigos-de-erro.md).",
                Enum = Codigos.ToList(),
            };
            esquema.Properties[ExtensoesProblema.TraceId] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Identificador da requisição: o mesmo do log. Informe ao suporte.",
            };
            esquema.Properties[ExtensoesProblema.Detalhes] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object | JsonSchemaType.Null,
                AdditionalPropertiesAllowed = true,
                Description = "Só em alguns erros: dados para o front (ex.: as dependências de EXCLUSAO_BLOQUEADA).",
            };
            esquema.Required ??= new HashSet<string>();
            esquema.Required.Add(ExtensoesProblema.Codigo);
            esquema.Required.Add(ExtensoesProblema.TraceId);
        }
    }

    /// <summary>
    /// Erros saem como application/problem+json. Endpoints anônimos (login) não respondem 403.
    /// </summary>
    private sealed class RespostasDeErro : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var anonimo = context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
            if (anonimo)
                operation.Responses?.Remove(StatusCodes.Status403Forbidden.ToString());

            foreach (var (status, resposta) in operation.Responses ?? [])
            {
                if (int.Parse(status) < 400 || resposta is not OpenApiResponse { Content: { } conteudo })
                    continue;

                var esquema = conteudo.Values.Select(c => c.Schema).FirstOrDefault(s => s is not null);
                conteudo.Clear();
                conteudo["application/problem+json"] = new OpenApiMediaType { Schema = esquema };
            }
        }
    }

    /// <summary>Exemplo de um registro de cenário de teste (listagem de Testes de Desempenho) no esquema do RegistroDto.</summary>
    private sealed class ExemploDeRegistro : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(Application.Dtos.RegistroDto) && schema is OpenApiSchema esquema)
                esquema.Example = ExemplosOpenApi.RegistroDevolvido();
        }
    }

    /// <summary>
    /// Exemplo do corpo do POST e do PUT de registros. O corpo é dinâmico (depende da entidade): o exemplo é de
    /// um cenário de teste da listagem de Testes de Desempenho.
    /// </summary>
    private sealed class ExemploDeCorpoDeRegistro : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.MethodInfo.DeclaringType != typeof(RegistrosController)
                || operation.RequestBody?.Content?.TryGetValue("application/json", out var corpo) != true)
            {
                return;
            }

            corpo!.Example = context.MethodInfo.Name switch
            {
                nameof(RegistrosController.Criar) => ExemplosOpenApi.CorpoDeCriacao(),
                nameof(RegistrosController.Atualizar) => ExemplosOpenApi.CorpoDeAtualizacao(),
                _ => corpo.Example,
            };
        }
    }
}
