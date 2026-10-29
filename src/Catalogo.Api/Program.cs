using Catalogo.Api.Autenticacao;
using Catalogo.Api.Configuracao;
using Catalogo.Api.Erros;
using Catalogo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Api. Contrato JSON (docs/api/README.md): camelCase, nulos explícitos, enums e datas como texto, datas em UTC com "Z".
// As respostas comuns a todos os endpoints (401, 403, 500) são declaradas para o OpenAPI em ConfiguracaoSwagger.
builder.Services.AddControllers(opcoes =>
    {
        ConfiguracaoSwagger.DeclararRespostasComuns(opcoes);
        // Instância (e não Add<T>): assim vale o Order do filtro, que precisa rodar depois do [Produces].
        opcoes.Filters.Add(new ProblemaComoProblemJson());
    })
    .AddJsonOptions(opcoes => opcoes.JsonSerializerOptions.Converters.Add(new ConversorDataUtc()));
// "codigo" e "traceId" em TODO ProblemDetails, inclusive os do próprio ASP.NET (docs/adr/0023).
builder.Services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = ExtensoesProblema.Completar);
// O TratadorExcecoesGlobal já registra os erros. O log padrão do ExceptionHandlerMiddleware
// (que registraria até os 400 como erro, com stack trace) fica desligado no appsettings.json.
builder.Services.AddExceptionHandler<TratadorExcecoesGlobal>();
builder.Services.AdicionarSwagger();

// Autenticação (JWT), autorização padrão (tudo exige login) e rate limiting do login. Ver docs/seguranca.md.
builder.Services.AdicionarSeguranca();
// CORS para o front: origens de "Cors:OrigensPermitidas" (docs/seguranca.md#cors).
builder.Services.AdicionarCors();

// Domain + Application
builder.Services.AdicionarAplicacao();

// Infrastructure (EF Core, repositórios, cache de metadados, health check do banco).
// Este é o ÚNICO ponto da Api que usa a Infrastructure (garantido por teste de arquitetura).
builder.Services.AdicionarInfraestrutura();

var app = builder.Build();

// Comando de linha "criar-admin": cria o primeiro administrador e termina, sem subir a API.
if (ComandoCriarAdmin.EhEsteComando(args))
{
    return await ComandoCriarAdmin.ExecutarAsync(
        app.Services, args, Environment.GetEnvironmentVariable(ComandoCriarAdmin.VariavelSenha), Console.Out);
}

// Falha na inicialização, com mensagem clara, se a connection string ou a chave do JWT não foram configuradas.
ConfiguracaoInfraestrutura.ObterConnectionString(app.Configuration);
OpcoesJwt.Ler(app.Configuration);
ConfiguracaoCors.LerOrigens(app.Configuration);

app.UseExceptionHandler();
// Respostas de erro sem corpo (ex.: 401 sem token, 404 de rota inexistente) também viram ProblemDetails.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Antes da autenticação: o preflight (OPTIONS) não tem token, e as respostas de erro também levam os cabeçalhos de CORS.
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Situação atual do usuário: desativado → 401; senha provisória pendente → 403 (RN23, RN25).
app.UseMiddleware<VerificacaoUsuarioMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
return 0;

// Permite que os testes de integração (WebApplicationFactory) enxerguem a classe Program.
public partial class Program;
