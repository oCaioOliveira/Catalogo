# Catalogo — back-end

API .NET 8 do Catalogo. Visão geral do repositório no [README da raiz](../README.md).
Instruções para agentes de IA (e para quem está chegando): [`AGENTS.md`](AGENTS.md).

**Todos os comandos abaixo são rodados de dentro de `backend/`.**

## Pré-requisitos

- **.NET SDK 8** (`dotnet --list-sdks` deve mostrar um `8.0.x`). O [`global.json`](global.json) fixa a versão 8.
- **Docker** com Compose, para o SQL Server local. Ver [`database/README.md`](../database/README.md).

## 1. Subir o banco local

Siga o [`database/README.md`](../database/README.md) (criar o `database/.env` e subir o container). Resumo:

```bash
cp ../database/.env.example ../database/.env      # e troque as senhas
(cd ../database/docker && docker compose --env-file ../.env up -d)
```

Isso cria dois bancos com o schema e o seed: `ListagemGenerica` (para rodar a API) e
`ListagemGenerica_Testes` (para os testes de integração).

## 2. Configurar a API e os testes

A API lê só as seis chaves do deploy ([ADR 0053](../docs/adr/0053-configuracao-do-deploy-caixa-e-chave-jwt-no-banco.md)). Localmente,
elas ficam no `src/Catalogo.Api/appsettings.Development.json`, **fora do git**. Copie o modelo e troque `__DB_PASSWORD__` pelo
`APP_DB_PASSWORD` do `database/.env`:

```bash
cp src/Catalogo.Api/appsettings.Development.json.exemplo src/Catalogo.Api/appsettings.Development.json
```

```json
{
  "ENVIRONMENT": "LOCAL",
  "CORS_ALLOW": "http://localhost:4200,http://localhost:5173",
  "DB_DATABASE": "ListagemGenerica",
  "DB_SERVER": "localhost,1433",
  "DB_USER": "listagem_app",
  "DB_PASSWORD": "<senha>"
}
```

Os testes de integração continuam com a connection string do banco de **testes** no user-secrets do projeto de testes (a
`BancoDeTestesFixture` a desmonta nas chaves `DB_*` da API, com `ENVIRONMENT` = `TQS`):

```bash
dotnet user-secrets set "ConnectionStrings:CatalogoTestes" \
  "Server=localhost,1433;Database=ListagemGenerica_Testes;User Id=listagem_app;Password=<senha>;Encrypt=True;TrustServerCertificate=True" \
  --project tests/Catalogo.Api.IntegrationTests
# Só para o teste OutroSistemaNoMesmoBancoTests, que cria dbo.Usuario "de outro sistema" no banco de TESTES (o listagem_app
# não tem DDL nem enxerga dbo). <senha_sa> = MSSQL_SA_PASSWORD do database/.env. Nunca aponte para outro banco.
dotnet user-secrets set "ConnectionStrings:CatalogoTestesAdmin" \
  "Server=localhost,1433;Database=ListagemGenerica_Testes;User Id=sa;Password=<senha_sa>;Encrypt=True;TrustServerCertificate=True" \
  --project tests/Catalogo.Api.IntegrationTests
```

### No ambiente da CAIXA (banco `CATDB001`, sem Docker)

O banco é o `CATDB001` (servidor `scthmdadnt0017`, SQL Server 2014), no schema `cat` ([ADR 0052](../docs/adr/0052-padrao-de-nomes-caixa-e-sql-server-2014.md)).
No `appsettings.Development.json`, use `DB_SERVER` = `scthmdadnt0017`, `DB_DATABASE` = `CATDB001` e o usuário e a senha passados pelo
DBA. O login não cria outro banco: os testes de integração usam o schema **`cat_testes`** do próprio `CATDB001` (só apagam dados num
schema terminado em `_testes`). Em PowerShell:

```powershell
$cs = "Server=scthmdadnt0017;Database=CATDB001;User Id=<usuario>;Password=<senha>;Encrypt=True;TrustServerCertificate=True"
dotnet user-secrets set "ConnectionStrings:CatalogoTestes" $cs --project tests/Catalogo.Api.IntegrationTests
dotnet user-secrets set "SchemaTestes" "cat_testes" --project tests/Catalogo.Api.IntegrationTests
# Monta (ou recria, apagando os dados de teste) o schema cat_testes. Rode de novo depois de cada script V0xx novo.
../database/ferramentas/estrutura_padrao_caixa.ps1 -Modo SchemaTestes -ConnectionString $cs -Schema cat_testes
# O schema de testes também precisa da tabela da chave do JWT: rode o V018 trocando "cat." por "cat_testes.".
```

Sem a `CatalogoTestesAdmin`, o `OutroSistemaNoMesmoBancoTests` aparece como ignorado (ele só faz sentido no Docker local).

### Servidores (DES, TQS, HMP, PRD)

O `appsettings.json` publicado tem as seis chaves com o marcador (`__DB_PASSWORD__`...), que o deploy substitui. Sem uma delas, a API
não sobe e diz quais faltam. Antes do primeiro deploy desta versão, os DBAs aplicam o pacote `database/entrega/2026-10-30-chave-jwt`.

### Chave do JWT

Não há nada a configurar: na primeira subida, a API gera uma chave aleatória e grava no banco (`cat.CATTB025_CHAVE_JWT`, script
`V018`); nas próximas, lê a mesma. Detalhes e como trocar a chave em [`docs/seguranca.md`](../docs/seguranca.md#chave-de-assinatura-do-jwt).

## Primeiro administrador

Todo endpoint exige login. O primeiro usuário, um ADMINISTRADOR, é criado por um comando, que **só
funciona se não houver nenhum administrador ativo**. A senha vem de uma variável de ambiente (nunca de
argumento, que ficaria no histórico do terminal) e precisa ser trocada no primeiro acesso:

```bash
CATALOGO_ADMIN_SENHA='UmaSenhaForte2026' \
  dotnet run --project src/Catalogo.Api -- criar-admin --matricula admin --nome "Administrador"
```

Para um banco instalado pelos DBAs (pacote de `database/entrega/`), o mesmo comando **gera o script** do administrador inicial,
sem acessar o banco, com **só o hash** da senha (use caminho absoluto em `--saida`: o `dotnet run` roda na pasta do projeto):

```bash
CATALOGO_ADMIN_SENHA='...' dotnet run --project src/Catalogo.Api -- criar-admin --gerar-sql \
  --matricula C161587 --nome "Caio" --saida "$PWD/../database/entrega/<pacote>/03_administrador_inicial.sql"
```

A senha segue a política (RN25), a não ser com `--aceitar-senha-fora-da-politica`, exceção só deste modo e só para a senha
provisória do administrador inicial: o usuário nasce com troca obrigatória, e a senha nova segue a política.

Depois, faça login (`POST /api/v1/auth/login`), troque a senha (`POST /api/v1/auth/trocar-senha`) e crie as
equipes e os usuários pela API (`/api/v1/equipes`, `/api/v1/usuarios`). Os usuários recebem uma senha provisória,
que é mostrada uma única vez.

## 3. Rodar a API

```bash
dotnet run --project src/Catalogo.Api --launch-profile Local
```

- **Swagger UI**: <http://localhost:5080/swagger> (só com `ENVIRONMENT` = `LOCAL` ou `DES`). Faça o login em
  `POST /api/v1/auth/login` e cole o `token` no botão **Authorize**.
- **Health check**: <http://localhost:5080/health> (inclui a conexão com o banco).

### Endpoints

Toda rota começa com `/api/v1` ([ADR 0022](../docs/adr/0022-versionamento-por-prefixo-de-rota.md)). O contrato completo é o
[`openapi/v1.json`](openapi/v1.json); o guia para quem consome a API é [`docs/api/README.md`](../docs/api/README.md), e os
códigos de erro estão em [`docs/api/codigos-de-erro.md`](../docs/api/codigos-de-erro.md). Regras em
[`docs/regras-de-negocio.md`](../docs/regras-de-negocio.md).

| Método | Rota | O que faz |
|---|---|---|
| GET | `/api/v1/grupos` | grupos (listagens) disponíveis, para o menu |
| GET | `/api/v1/grupos/{grupo}/entidades?equipeId=` | metadados para montar formulários (com `equipeId`, informa `requerAprovacao`) |
| GET | `.../{entidade}/registros?pagina=1&tamanho=20&ordenacao=-criadoEm&<filtros>` | lista paginada com filtros (RN17 a RN19) |
| GET | `.../{entidade}/registros/exportar?formato=csv\|xlsx&<filtros>` | exporta a listagem, com os mesmos filtros e visibilidade (RN49) |
| POST | `.../{entidade}/registros` | cria (201 + `Location`) |
| GET | `.../{entidade}/registros/{id}` | busca por id |
| PUT | `.../{entidade}/registros/{id}` | substitui o registro inteiro, com a `versao` no corpo (RN15) |
| DELETE | `.../{entidade}/registros/{id}` | exclui, aplicando CASCATA/RESTRINGIR (RN16) |
| GET | `.../{entidade}/campos/{campo}/opcoes?equipeId=&dependeDe=&busca=` | opções de um campo REFERENCIA (RN21) |
| GET | `.../{entidade}/registros/{id}/historico` | histórico do registro (RN32) |
| POST | `.../{entidade}/registros/{id}/aprovacao` | aprova ou rejeita um registro pendente, com justificativa (RN36) |
| GET/PUT | `/api/v1/equipes/{equipeId}/configuracoes/grupos/{grupo}/entidades/{entidade}/aprovacao` | consulta ou liga/desliga a aprovação de uma entidade na equipe (RN33) |
| GET/POST/PUT | `/api/v1/equipes/{equipeId}/configuracoes/grupos/{grupo}/entidades/{entidade}/avaliacao` | modelo de avaliação da entidade na equipe (RN40 a RN43); também `.../modelos`, `PATCH .../ativacao` e `.../historico` |
| GET | `.../{entidade}/registros/{id}/avaliacoes` | resumo e avaliações individuais do item (RN47) |
| PUT/DELETE | `.../{entidade}/registros/{id}/avaliacoes/minha` | cria/edita ou remove a sua avaliação (RN45) |
| GET | `.../{entidade}/registros?ordenacao=-avaliacao.qualidade&avaliacao.qualidade.de=4` | listagem pela média de um critério (RN48) |
| GET | `/api/v1/aprovacoes/pendentes?grupo=&entidade=&equipeId=` | fila de registros pendentes, do mais antigo para o mais novo (RN36) |
| POST | `/api/v1/auth/login` | login com matrícula e senha, devolve o JWT (RN23) |
| POST | `/api/v1/auth/trocar-senha` | troca a própria senha (RN25) |
| GET | `/api/v1/auth/eu` | dados do usuário logado |
| GET/POST/PUT/PATCH | `/api/v1/equipes`, `/api/v1/usuarios` | administração, só ADMINISTRADOR |
| GET/POST/PUT/PATCH/DELETE | `/api/v1/admin/metadados/grupos/...` | administração de metadados: listagens, entidades, campos, restrições e dependências, com `?simular=true` (RN50 a RN77, [passo a passo](../docs/api/administracao-de-metadados.md)), só ADMINISTRADOR |

Todos exigem `Authorization: Bearer <token>`, exceto o login e o `/health`. Quem pode o quê: RN26 em
[`docs/regras-de-negocio.md`](../docs/regras-de-negocio.md#rn26--matriz-de-permissões).

Experimente:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/v1/auth/login -H "Content-Type: application/json" \
  -d '{"matricula": "admin", "senha": "..."}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["token"])')

curl -H "Authorization: Bearer $TOKEN" http://localhost:5080/api/v1/grupos/testes_desempenho/entidades

curl -i -X POST http://localhost:5080/api/v1/grupos/testes_desempenho/entidades/comunidade/registros \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"nome": "Comunidade Ágil"}'
```

## Testes

```bash
dotnet test                                                   # todos (precisa do banco no ar)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"   # só os que não usam banco
```

| Projeto | O que cobre |
|---|---|
| `tests/Catalogo.Domain.Tests` | validadores, chave única, plano de exclusão, interpretação de filtros |
| `tests/Catalogo.Application.Tests` | casos de uso, com dublês |
| `tests/Catalogo.ArchitectureTests` | regra de dependência, pacotes proibidos, sem migrations, sem SQL manual |
| `tests/Catalogo.Api.IntegrationTests` | API de ponta a ponta contra o `ListagemGenerica_Testes` |

Os testes de integração apagam os dados (não os metadados) do banco de testes antes de cada teste
e se recusam a rodar num banco cujo nome não termine em `_Testes`. Detalhes em [`tests/AGENTS.md`](tests/AGENTS.md).

## Estrutura

```
src/
  Catalogo.Domain/          motor genérico (sem dependências)
  Catalogo.Application/     casos de uso, DTOs, interfaces
  Catalogo.Infrastructure/  EF Core (SQL Server), consultas LINQ, cache de metadados
  Catalogo.Api/             controllers, erros, OpenAPI, configuração
tests/                      Domain, Application, Architecture e Integration
```

## Pacotes

As versões ficam só em [`Directory.Packages.props`](Directory.Packages.props) (Central Package Management).

- **Permitidos**: `Microsoft.EntityFrameworkCore.SqlServer` 8.x, `Swashbuckle.AspNetCore`,
  `FluentValidation`, xUnit (com asserts próprios ou Shouldly), `NetArchTest.Rules`, `Respawn`,
  `Microsoft.AspNetCore.Mvc.Testing`, os health checks do EF Core, `Microsoft.Extensions.Identity.Core` 8.x
  (só o `PasswordHasher`, [ADR 0016](../docs/adr/0016-passwordhasher-sem-identity.md)) e
  `Microsoft.AspNetCore.Authentication.JwtBearer` 8.x e `ClosedXML` (licença MIT, só na Infrastructure e nos testes de
  integração, para a exportação em XLSX).
- **Proibidos**: MediatR, AutoMapper, MassTransit, FluentAssertions ≥ 8 ([ADR 0004](../docs/adr/0004-sem-mediatr-automapper-fluentassertions.md))
  e Dapper ([ADR 0011](../docs/adr/0011-consultas-somente-linq.md)).
- Qualquer outro pacote precisa de aprovação **antes** de entrar.

## Ambientes

`LOCAL`, `DES`, `TQS`, `HMP` e `PRD`, pela chave `ENVIRONMENT` do `appsettings.json` (não pelo `ASPNETCORE_ENVIRONMENT`). Um
`appsettings.json` só para todos os servidores; localmente, o `appsettings.Development.json`. Detalhes em [`src/Catalogo.Api/AGENTS.md`](src/Catalogo.Api/AGENTS.md#ambientes-e-configuração).
