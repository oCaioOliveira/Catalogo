# AGENTS.md — Catalogo.Api

Complementa o [`AGENTS.md` da raiz](../../AGENTS.md). O caminho de uma requisição até o banco e o
papel do tratador de erros estão em [`docs/arquitetura.md`](../../../docs/arquitetura.md#tratamento-de-erros).
As mensagens de erro de validação seguem [`docs/regras-de-negocio.md`](../../../docs/regras-de-negocio.md#rn05--todos-os-erros-de-uma-vez).

## Convenção de rotas

Toda rota começa com **`/api/v1`**: use `[Route(RotasApi.V1 + "/...")]` (constante em `Controllers/RotasApi.cs`), nunca o
texto `"api/v1"` solto. O `/health` fica fora da versão ([ADR 0022](../../../docs/adr/0022-versionamento-por-prefixo-de-rota.md)).
Os registros ficam sob `/api/v1/grupos/{grupo}/entidades`. `{grupo}` e `{entidade}` são as **chaves** dos
metadados (ex.: `testes_desempenho`, `cenario_teste`) e não diferenciam maiúsculas.

| Método | Rota | Controller | Sucesso |
|---|---|---|---|
| GET | `/api/v1/grupos` | `GruposController.Listar` (menu do front, sem paginação) | 200 |
| GET | `/api/v1/grupos/{grupo}/entidades` | `EntidadesController.Listar` (`?equipeId=` devolve `requerAprovacao`) | 200 |
| GET | `/api/v1/grupos/{grupo}/entidades/{entidade}/registros` | `RegistrosController.Listar` | 200 (página, RN17) |
| GET | `.../registros/exportar?formato=csv\|xlsx` | `RegistrosController.Exportar` (mesmos filtros da listagem; `ArquivoExportadoResult`) | 200 arquivo (RN49) |
| POST | `/api/v1/grupos/{grupo}/entidades/{entidade}/registros` | `RegistrosController.Criar` | 201 + header `Location` |
| GET | `/api/v1/grupos/{grupo}/entidades/{entidade}/registros/{id:guid}` | `RegistrosController.Obter` | 200 |
| PUT | `/api/v1/grupos/{grupo}/entidades/{entidade}/registros/{id:guid}` | `RegistrosController.Atualizar` | 200 (RN15) |
| DELETE | `/api/v1/grupos/{grupo}/entidades/{entidade}/registros/{id:guid}` | `RegistrosController.Excluir` | 204 (RN16) |
| GET | `/api/v1/grupos/{grupo}/entidades/{entidade}/campos/{campo}/opcoes` | `CamposController.Opcoes` | 200 (RN21) |
| GET | `.../registros/{id:guid}/historico` | `RegistrosController.Historico` | 200 (RN32) |
| POST | `.../registros/{id:guid}/aprovacao` | `RegistrosController.Decidir` (`{ decisao, justificativa }`) | 200 com o registro (RN36) |
| GET, PUT | `/api/v1/equipes/{equipeId:guid}/configuracoes/grupos/{grupo}/entidades/{entidade}/aprovacao` | `ConfiguracoesController` (`{ ativa }`) | 200 (RN33) |
| GET | `.../aprovacao/historico` (mesma rota acima) | `ConfiguracoesController.Historico` | 200 (RN32, RN33) |
| GET, POST, PUT | `/api/v1/equipes/{equipeId:guid}/configuracoes/grupos/{grupo}/entidades/{entidade}/avaliacao` | `ModelosAvaliacaoController` (modelo ativo; POST 201, 409 se já há ativo) | 200/201 (RN40 a RN43) |
| GET, PATCH `.../ativacao`, GET `.../historico` | `.../avaliacao/modelos`, `.../avaliacao/ativacao`, `.../avaliacao/historico` | `ModelosAvaliacaoController` | 200 |
| GET, PUT `minha`, DELETE `minha`, GET `historico` | `.../registros/{id:guid}/avaliacoes` | `AvaliacoesController` (PUT: 201 ao criar, 200 ao editar) | RN44 a RN47 |
| GET, POST, PUT, PATCH, DELETE | `/api/v1/admin/metadados/grupos/...` (grupos, entidades, campos, ordem, restrições, dependências, histórico) | `AdminGruposController`, `AdminEntidadesController`, `AdminCamposController` (só ADMINISTRADOR; toda escrita aceita `?simular=true` e devolve `ResultadoAdministracaoDto`; POST 201) | 200/201 (RN50 a RN77) |
| GET | `/api/v1/painel/grupos/{grupo}/visao-geral`, `.../entidades/{entidade}/indicadores`, `.../indicadores/exportar?formato=csv\|xlsx` | `PainelController` (só GESTOR e ADMINISTRADOR; parâmetros lidos por `ParametrosDaUrl`) | 200 (RN78 a RN88, [`docs/api/painel.md`](../../../docs/api/painel.md)) |
| GET, POST | `.../entidades/{entidade}/importacao/modelo`, `.../importacoes` (multipart, campo `arquivo`), `.../importacoes/{id:guid}` (GET), `.../validar`, `.../executar`, `.../relatorio` (GET, XLSX), `.../desfazer` | `ImportacoesController` (RN97 a RN104, [`docs/api/importacao.md`](../../../docs/api/importacao.md)) | 201 no envio, 200 nos demais |
| GET | `/api/v1/aprovacoes/pendentes` | `AprovacoesController.Pendentes` (filtros `grupo`, `entidade`, `equipeId`) | 200 (página, RN36) |
| POST | `/api/v1/auth/login` | `AuthController.Login` (anônimo, com rate limiting) | 200 + token |
| POST | `/api/v1/auth/trocar-senha` | `AuthController.TrocarSenha` | 204 |
| GET | `/api/v1/auth/eu` | `AuthController.Eu` | 200 |
| GET, POST, PUT, PATCH `.../ativacao`, GET `.../historico` | `/api/v1/equipes` | `EquipesController` (só ADMINISTRADOR; GET paginado) | |
| GET, POST, PUT, PATCH `.../ativacao`, POST `.../redefinir-senha`, GET `.../historico` | `/api/v1/usuarios` | `UsuariosController` (só ADMINISTRADOR) | |
| GET | `/health` | health check (inclui o banco, anônimo) | 200 `Healthy` |

Rotas novas: substantivos no plural, em português, minúsculas (ex.: `/registros/{id}/historico`).

**O contrato só cresce na v1**: pode acrescentar campos, endpoints, parâmetros opcionais e códigos de erro. Remover ou
renomear campo, mudar tipo, status HTTP ou código de erro só numa v2 ([ADR 0022](../../../docs/adr/0022-versionamento-por-prefixo-de-rota.md)).
Listagens de dados devolvem `PaginaDto` (com `pagina`/`tamanho` lidos por `InterpretadorConsulta.InterpretarPaginacao`);
só metadados e configurações pequenas (grupos, entidades, modelos de avaliação) voltam como array.

## Todo endpoint exige autenticação

- Há uma **política de fallback**: todo endpoint exige um usuário autenticado. Exceções documentadas:
  `POST /api/v1/auth/login` (`[AllowAnonymous]`) e `/health`. Endpoint novo anônimo precisa de justificativa no PR
  e de uma linha nesta seção.
- Endpoints que funcionam com a troca de senha pendente levam `[PermitirSenhaProvisoria]` (hoje só
  `trocar-senha` e `eu`). Os demais respondem 403 `TROCA_DE_SENHA_OBRIGATORIA` (RN25).
- O controller **não** decide permissão: o caso de uso consulta a `PoliticaPermissao`. Nada de
  `[Authorize(Roles = ...)]` nem `User.IsInRole` aqui.
- Nunca registre senha, token ou hash no log. Use a matrícula para identificar quem fez.
- Configuração e funcionamento: [`docs/seguranca.md`](../../../docs/seguranca.md).

## Controllers finos

- Use `Controller`s com `[ApiController]` (sem Minimal APIs), dentro de `Controllers/`, com nome
  terminado em `Controller`. Garantido por `Controllers_FicamNaPastaControllersETerminamComController`.
- Cada ação só chama **um** caso de uso e devolve o resultado. Nada de regra de negócio aqui.
- **Sem `try/catch`**: as exceções sobem para o `Erros/TratadorExcecoesGlobal.cs`.
- Só o `Program.cs` pode usar a Infrastructure (`Api_SoOProgramUsaAInfrastructure`).
- **Parâmetros de URL dinâmicos** (filtros da listagem, que dependem dos campos da entidade): leia com
  `ParametrosDaUrl.Ler(Request)` e passe ao caso de uso. Quem interpreta é o Domain (`InterpretadorConsulta`).
  Os parâmetros fixos (`pagina`, `tamanho`...) são declarados como `string?` na ação só para aparecerem no Swagger.
- Download de arquivo: devolva `ArquivoExportadoResult`, que escreve na resposta enquanto lê do banco. Toda validação
  acontece no caso de uso **antes** (depois que o arquivo começou a sair, um erro não vira mais ProblemDetails).

## Todo endpoint declara as respostas

- Declare em cada ação, com `[ProducesResponseType<Tipo>(status)]`, o sucesso (com o tipo do DTO) e os erros que **ela**
  pode dar: 400 (`ValidationProblemDetails`) se tem corpo ou parâmetros, 404 (`ProblemDetails`) se tem `{grupo}`, `{id}`...
  na rota, 409 quando há regra ou concorrência. 401, 403 e 500 já vêm para todos de `ConfiguracaoSwagger.DeclararRespostasComuns`.
- O `<summary>` da ação vira a descrição no OpenAPI (o XML de documentação está ligado no `.csproj`).
- `ContratoOpenApiTests.TodoEndpoint_DeclaraSucessoEErrosComProblemDetails` falha se faltar sucesso, 401, 500, 400 (com corpo
  ou `pagina`) ou 404 (com parâmetro de rota), ou se um erro não for ProblemDetails.

## Mudança no `openapi/v1.json` precisa ser intencional

O OpenAPI gerado é o **contrato** com o front e fica salvo em [`backend/openapi/v1.json`](../../openapi/v1.json)
([ADR 0024](../../../docs/adr/0024-contrato-openapi-versionado-e-verificado.md)). O teste `ContratoOpenApiTests` falha se o
documento gerado mudar. Mudou o contrato **de propósito** (DTO, rota, status, `<summary>` de controller)? Regenere e
**revise o diff** antes de commitar, de dentro de `backend/`:

```bash
ATUALIZAR_OPENAPI=1 dotnet test --filter "FullyQualifiedName~ContratoOpenApiTests"
```

Se o diff mostrar um campo removido ou renomeado, um tipo trocado ou um status diferente, é quebra de contrato: só numa v2.
Exemplos do corpo dos registros (dinâmico) ficam em `Configuracao/ExemplosOpenApi.cs`.

## Todo erro tem `codigo`

Toda resposta de erro é ProblemDetails (`application/problem+json`) com **`codigo`** (de `CodigosErro`, no Domain) e
**`traceId`** (o mesmo do log). Dados para o front vão em **`detalhes`** ([ADR 0023](../../../docs/adr/0023-codigos-de-erro-estaveis-no-problemdetails.md)).
A tabela para o front, com o que fazer em cada código, é [`docs/api/codigos-de-erro.md`](../../../docs/api/codigos-de-erro.md).

- O código vem da exceção (`ExcecaoDominio.Codigo`): quem lança escolhe o código. Nunca escreva o código como string solta.
- `ExtensoesProblema.Completar` (ligado em `AddProblemDetails`) completa `traceId` e um código padrão por status nos erros do
  próprio ASP.NET (rota inexistente, 405, 415, JSON malformado, 401 sem token). `ProblemaComoProblemJson` garante o
  `application/problem+json` mesmo com o `[Produces("application/json")]` dos controllers.
- Erro escrito à mão (middleware, rate limiting): ponha o `codigo` explicitamente, como em `VerificacaoUsuarioMiddleware`.
- `ErrosApiTests` percorre os principais cenários e falha se algum erro sair sem `codigo` ou `traceId`.
- Código novo: constante em `CodigosErro`, linha em `docs/api/codigos-de-erro.md` e regenerar o `openapi/v1.json` (o enum do `codigo` está lá).

## Exceção → status HTTP

Feito **só** no `TratadorExcecoesGlobal` (um `IExceptionHandler`).

| Exceção | Status | `codigo` | Corpo |
|---|---|---|---|
| `ValidacaoRegistroException` | 400 | `VALIDACAO` (ou o da exceção, ex.: `EXPORTACAO_MUITO_GRANDE`) | `ValidationProblemDetails` com `errors` por campo (ou por parâmetro de URL) |
| JSON inválido ou corpo que não é objeto | 400 | `VALIDACAO` | automático do `[ApiController]` |
| `BadHttpRequestException` (Kestrel: corpo grande demais...) | o da exceção | `REQUISICAO_INVALIDA` | sem `detail` |
| `RecursoNaoEncontradoException` | 404 | o da exceção (`REGISTRO_NAO_ENCONTRADO`, `ENTIDADE_NAO_ENCONTRADA`...) | `detail` com o que não foi encontrado |
| rota inexistente, método errado, `Content-Type` errado | 404, 405, 415 | `ROTA_NAO_ENCONTRADA`, `METODO_NAO_PERMITIDO`, `TIPO_DE_CONTEUDO_NAO_SUPORTADO` | do framework, completado por `ExtensoesProblema` |
| `ConflitoUnicidadeException` | 409 | `CONFLITO_UNICIDADE` | `detalhes`: `{ restricao, campos }` |
| `ConflitoConcorrenciaException` | 409 | `CONFLITO_CONCORRENCIA` | `detail` |
| `ExclusaoBloqueadaException` | 409 | `EXCLUSAO_BLOQUEADA` | `detalhes`: `{ dependencias: [{ entidade, campo, quantidade, exemplos }] }` |
| `RegraDeNegocioException` | 409 | o da exceção (ex.: `ULTIMO_ADMINISTRADOR`, `APROVACOES_PENDENTES`) | `detalhes` = os `Dados` da exceção, se houver (ex.: `{ quantidade }`) |
| `NaoAutenticadoException` (e token ausente/inválido) | 401 | `NAO_AUTENTICADO` ou `CREDENCIAIS_INVALIDAS` (login) | `detail` |
| `AcessoNegadoException` | 403 | `SEM_PERMISSAO` ou o da exceção (`TROCA_DE_SENHA_OBRIGATORIA`) | `detail` |
| rate limiting do login | 429 | `MUITAS_TENTATIVAS` | `detail` |
| qualquer outra | 500 | `ERRO_INTERNO` | mensagem genérica. `detail` e `excecao` (stack trace) **só em Development** |

Exceção de domínio nova? Ela herda `ExcecaoDominio` e recebe um código; adicione o `case` no tratador **e** a linha nesta tabela.

## Ambientes e configuração

| Ambiente (`ASPNETCORE_ENVIRONMENT`) | Arquivo | Particularidades |
|---|---|---|
| `Development` | `appsettings.Development.json` | Swagger UI em `/swagger`, SQL do EF no log, connection string do **user-secrets** |
| `Homologacao` | `appsettings.Homologacao.json` | log em JSON no console |
| `Production` | `appsettings.Production.json` | log em JSON no console |
| `Testes` | (nenhum) | usado só pelos testes de integração |

- Chave do JWT: `Jwt:ChaveAssinatura` (user-secrets em dev; `Jwt__ChaveAssinatura` nos servidores). Sem ela, a API não sobe.
- CORS: `Cors:OrigensPermitidas` (Development: `localhost:5173` e `localhost:4200`; servidores: vazia no arquivo, preenchida por
  `Cors__OrigensPermitidas__0=...`). "*" faz a API não subir. Ver `Configuracao/ConfiguracaoCors.cs` e [`docs/seguranca.md`](../../../docs/seguranca.md#cors).
- Exportação: `Exportacao:LimiteLinhas` (padrão 10.000) e `Exportacao:FusoHorario` (padrão `America/Sao_Paulo`), lidos em
  `ConfiguracaoAplicacao.LerOpcoesExportacao` (RN49).
- Painel: `Painel:FusoHorario` (padrão `America/Sao_Paulo`), `Painel:DiasPendenteAntigo` (padrão 7) e `Painel:MinimoAvaliacoesRanking`
  (padrão 3), lidos em `ConfiguracaoAplicacao.LerOpcoesPainel` (RN80, RN82, RN83).
- JSON: datas de sistema sempre em UTC com `Z` (`Configuracao/ConversorDataUtc.cs`), nulos explícitos, enums como texto.
- Importação: `Importacao:IntervaloLimpezaMinutos` (padrão 60; 0 desliga, como nos testes) é o intervalo do `LimpezaImportacoes`
  (`Configuracao/`, um `BackgroundService` que chama o caso de uso `LimparImportacoes`, RN104). O envio aceita até 12 MB no corpo
  (`[RequestSizeLimit]`); o limite do arquivo (5 MB) é conferido no caso de uso, com mensagem clara.
- Comando `criar-admin` (primeiro administrador): `Configuracao/ComandoCriarAdmin.cs`, chamado no início do `Program.cs`. Com
  `--gerar-sql --saida <arquivo>` ele não acessa o banco: escreve o script do administrador inicial do pacote de entrega
  (`GerarSqlAdministradorInicial`, só o hash). `--aceitar-senha-fora-da-politica` só vale nesse modo.
- Connection string: chave `ConnectionStrings:Catalogo`. Em desenvolvimento vem do user-secrets
  (ver [README](../../README.md)). Nos servidores vem da variável de ambiente
  `ConnectionStrings__Catalogo`. **Nunca** em arquivo versionado. Sem ela, a API não sobe e mostra mensagem clara.
- Logging: o logger padrão (`ILogger<T>`) com **message templates** (`"Registro {RegistroId} criado"`,
  nunca interpolação `$"..."`), para os campos virarem propriedades no log JSON.
- Ficam desligadas no `appsettings.json` duas categorias de log que repetiam como erro o que o tratador
  já registra: `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` e `Microsoft.EntityFrameworkCore.Update`.
  Os 500 continuam registrados como erro, com a exceção completa, pelo `TratadorExcecoesGlobal`.
