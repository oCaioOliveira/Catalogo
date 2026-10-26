# AGENTS.md — backend

Complementa o [`AGENTS.md` da raiz](../AGENTS.md). Cada projeto tem o seu próprio `AGENTS.md`:
leia sempre o mais próximo do código que você vai mudar. **Todos os comandos e caminhos daqui são
relativos a `backend/`.**

API .NET 8 em Clean Architecture enxuta ([ADR 0002](../docs/adr/0002-clean-architecture-enxuta.md)).
Visão geral e fluxos em [`docs/arquitetura.md`](../docs/arquitetura.md).

## Mapa

| Caminho | O que tem | Leia antes de mexer |
|---|---|---|
| `src/Catalogo.Domain/` | motor genérico: metadados, validadores, chave única, plano de exclusão, filtros, parâmetros do painel, **política de permissões**, histórico | [`src/Catalogo.Domain/AGENTS.md`](src/Catalogo.Domain/AGENTS.md) |
| `src/Catalogo.Application/` | casos de uso, DTOs, mapeamentos, interfaces | [`src/Catalogo.Application/AGENTS.md`](src/Catalogo.Application/AGENTS.md) |
| `src/Catalogo.Infrastructure/` | EF Core, repositório, consultas LINQ (inclusive as agregações do painel), cache de metadados | [`src/Catalogo.Infrastructure/AGENTS.md`](src/Catalogo.Infrastructure/AGENTS.md) |
| `src/Catalogo.Api/` | controllers, autenticação JWT, CORS, erros, OpenAPI, DI, configuração | [`src/Catalogo.Api/AGENTS.md`](src/Catalogo.Api/AGENTS.md) |
| `openapi/v1.json` | o **contrato** da API (OpenAPI gerado), conferido por teste | [ADR 0024](../docs/adr/0024-contrato-openapi-versionado-e-verificado.md) |
| `tests/` | testes de unidade, arquitetura e integração | [`tests/AGENTS.md`](tests/AGENTS.md) |

## Comandos

Rode **de dentro de `backend/`**:

```bash
# Subir o SQL Server local (cria/atualiza os bancos ListagemGenerica e ListagemGenerica_Testes)
(cd ../database/docker && docker compose --env-file ../.env up -d)

# Build (precisa sair com 0 warnings)
dotnet build

# Todos os testes (os de integração precisam do banco no ar e do user-secret, ver README.md)
dotnet test

# Só os testes que não usam banco
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Testes lentos (desempenho do painel; ficam fora do "dotnet test" padrão, ver tests/AGENTS.md)
RODAR_TESTES_LENTOS=1 dotnet test --filter "FullyQualifiedName~DesempenhoPainelTests" --logger "console;verbosity=detailed"

# Rodar a API (Swagger UI em http://localhost:5080/swagger)
dotnet run --project src/Catalogo.Api --launch-profile Development
```

## Regras do back-end

1. **Regra de dependência**: `Application → Domain`; `Infrastructure → Application, Domain`;
   `Api → Application`, e `Api → Infrastructure` só no `Program.cs`. O Domain não depende de nada.
   Garantido por `tests/Catalogo.ArchitectureTests/DependenciasEntreCamadasTests.cs`.
2. **Pacotes proibidos**: MediatR, AutoMapper, MassTransit e FluentAssertions ≥ 8
   ([ADR 0004](../docs/adr/0004-sem-mediatr-automapper-fluentassertions.md)). Versões de pacote só em
   `Directory.Packages.props`. Garantido por `PacotesTests`. Pacote novo fora da lista do
   [README](README.md#pacotes) **precisa de aprovação antes**.
3. **Não há migrations**: o schema vem de `../database/scripts/V00x__*.sql`. Nunca use `dotnet ef
   migrations`, `EnsureCreated`, `EnsureDeleted` ou `Migrate`
   ([ADR 0003](../docs/adr/0003-schema-por-scripts-sql.md)). Garantido por `SemMigrationsTests`.
4. **Toda consulta ao banco é EF Core em LINQ**: proibido `SqlQueryRaw`, `FromSql*`, `ExecuteSql*` e
   Dapper ([ADR 0011](../docs/adr/0011-consultas-somente-linq.md)). Garantido por `SemSqlManualTests`.
5. **Segredos**: connection string (`ConnectionStrings:Catalogo`) e chave do JWT (`Jwt:ChaveAssinatura`)
   ficam vazias nos `appsettings*.json`. O valor vem do user-secrets (desenvolvimento) ou de variáveis de
   ambiente (servidores). Ver [`docs/seguranca.md`](../docs/seguranca.md).
6. **Permissão e histórico**: toda regra de "quem pode" está na `PoliticaPermissao` (Domain), inclusive as de
   aprovação (RN33 a RN38), de avaliação (RN40, RN44, RN47) e de áreas de gestão por equipe (quem vê a área e "as equipes do usuário
   nesta área", RN90 a RN93), e todo caso de uso que altera dados grava histórico (RN26, RN32). Detalhes no AGENTS.md da Application.
7. **Senha nunca** em log, resposta, histórico ou exceção. `SenhaHash` não sai da Infrastructure/Domain:
   nenhum DTO o expõe (o teste `RespostasEHistoricoDeUsuarios_NuncaTrazemSenhaNemHash` confere no JSON bruto).
8. **Nomes**: domínio em português (`CriarRegistro`, `Campo.Obrigatorio`); termos do framework em
   inglês (`Controller`, `DbContext`, `IExceptionHandler`).
9. Ao implementar ou testar uma regra, cite o `RNxx` de [`docs/regras-de-negocio.md`](../docs/regras-de-negocio.md).
10. **Contrato da API versionado**: toda rota fica sob `/api/v1` (constante `RotasApi.V1`). Mudança que **quebra o contrato**
    (remover ou renomear campo, mudar tipo, mudar status HTTP ou `codigo` de erro) **só numa v2**. Acrescentar campos ou endpoints
    pode ser feito na v1 ([ADR 0022](../docs/adr/0022-versionamento-por-prefixo-de-rota.md)). O contrato fica em `openapi/v1.json`,
    e o teste `ContratoOpenApiTests` falha se ele mudar sem querer: regenere com
    `ATUALIZAR_OPENAPI=1 dotnet test --filter "FullyQualifiedName~ContratoOpenApiTests"` e revise o diff.
11. **Todo erro tem `codigo`** (de `CodigosErro`) e `traceId`; a tabela para o front fica em
    [`docs/api/codigos-de-erro.md`](../docs/api/codigos-de-erro.md) ([ADR 0023](../docs/adr/0023-codigos-de-erro-estaveis-no-problemdetails.md)).
    O guia de quem consome a API é [`docs/api/README.md`](../docs/api/README.md): mantenha-o junto com o contrato.
12. **Áreas de gestão (no código, grupos) são criadas pela interface ou pela API** (Configurações → Áreas de gestão, sobre `/api/v1/admin/metadados`; só o ADMINISTRADOR, RN50): scripts `V00x` são só para schema.
    Toda alteração de metadados é validada contra os dados e bloqueada se deixaria registros inválidos (RN58,
    [ADR 0026](../docs/adr/0026-bloquear-alteracoes-incompativeis-com-os-dados.md)); passo a passo em
    [`docs/api/administracao-de-metadados.md`](../docs/api/administracao-de-metadados.md).
13. **Termos**: o código mantém `Grupo`, `Entidade` e `Registro`; mensagens que chegam à tela usam os termos do
    [glossário](../docs/glossario.md) ("área de gestão", "tipo de item", "item"). Regras recentes: ícone do tipo de item (RN95) e
    "Continuar de onde parou" (RN96, `GET /grupos/{grupo}/eu/recentes`, só itens que o usuário ainda vê).
14. **Um caminho só para gravar registros**: criar e alterar passam SEMPRE pelo `GravadorRegistro` (Application/Comum), no cadastro
    manual (`CriarRegistro`, `AtualizarRegistro`) e na importação de planilhas (RN97 a RN104, `SimuladorImportacao`). Nada de atalho
    para "gravar rápido": o que a importação precisa a mais (ler em lote) entra pelo `LotePreparacao`, com as mesmas regras.

## Definição de pronto

- [ ] `dotnet build` sem nenhum warning (`TreatWarningsAsErrors` está ligado).
- [ ] `dotnet test` todo verde, inclusive os testes de integração.
- [ ] Código novo coberto por teste no projeto certo (ver [`tests/AGENTS.md`](tests/AGENTS.md)).
- [ ] Se uma regra mudou: `docs/regras-de-negocio.md` (ou o doc de `docs/` correspondente) atualizado no mesmo PR.
- [ ] Se uma decisão de arquitetura mudou: ADR novo em `docs/adr/`.
- [ ] Se o schema mudou: script `V00x` novo em `database/scripts/` **e** mapeamento do EF atualizado.
- [ ] Se o contrato da API mudou: `openapi/v1.json` regenerado e revisado (só acréscimos na v1), e `docs/api/` atualizado.
- [ ] Erro novo: código em `CodigosErro` e linha em `docs/api/codigos-de-erro.md`.
