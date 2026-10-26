# 0036. Cliente da API gerado a partir do OpenAPI

- Status: Aceito
- Data: 2026-10-22

## Contexto

O contrato da API está em `backend/openapi/v1.json`, verificado por teste (ADR 0024). Escrever à mão as interfaces e as chamadas no
front duplicaria o contrato e deixaria o front desatualizado sem ninguém perceber.

## Decisão

- Models e funções de chamada são **gerados** com o **ng-openapi-gen** (MIT) a partir de `backend/openapi/v1.json`, pelo script
  `npm run gerar-api`, em `frontend/src/app/api/`. Essa pasta **nunca é editada à mão** (o lint e o Prettier a ignoram).
- Componentes não chamam `HttpClient` direto: usam serviços de `core/` (ex.: `RegistrosService`, `AuthService`,
  `MetadadosService`), que usam o cliente gerado (`Api.invoke(função, parâmetros)`).
- Os registros são dinâmicos (os campos vêm dos metadados): o front os trata como `Record<string, unknown>`. Na listagem e na
  exportação, os filtros dependem da entidade e não estão no contrato: o serviço monta a requisição com o `RequestBuilder` e o
  `PATH` da operação **gerados**, acrescentando os filtros.
- Os modelos gerados têm todas as propriedades opcionais (o OpenAPI não marca obrigatórios); `core/` converte para tipos do front
  com os campos obrigatórios (ex.: `Entidade`, `Campo`, `UsuarioLogado`).

## Consequências

- ✅ Mudou o contrato? `npm run gerar-api` e o compilador aponta o que quebrou no front.
- ⚠️ Os nomes das funções geradas são longos (o contrato não tem `operationId`), ex.: `apiV1GruposGrupoEntidadesGet`. Ficam
  escondidos atrás dos serviços de `core/`.
- ⚠️ Gerar o cliente exige o `backend/openapi/v1.json` atualizado no repositório.
