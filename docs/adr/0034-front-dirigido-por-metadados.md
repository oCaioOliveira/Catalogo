# 0034. Front-end dirigido por metadados

- Status: Aceito
- Data: 2026-10-22

## Contexto

O motor do Catalogo é genérico (ADR 0001): os tipos de item e os campos vêm de metadados, e a administração cria listagens novas
pela API (ADR 0025), sem deploy. Se o front tivesse uma tela escrita à mão para cada tipo de item, toda listagem nova (ou campo novo)
exigiria código e deploy do front, e o motor genérico perderia o sentido.

## Decisão

- **Nenhuma tela fixa por tipo de item.** Listagem, filtros, formulário e detalhe são **genéricos**, montados a partir de
  `GET /api/v1/grupos/{grupo}/entidades` (rotas `/:grupo/:entidade`, `/novo`, `/:id`, `/:id/editar`).
- Um componente de campo **por tipo de dado** (`shared/campos/`), escolhido pelo `tipoDado`; validações do front montadas pelos
  metadados (obrigatório, tamanho, faixa, escala, opções), espelhando RN03 e RN04. O back-end valida de novo.
- Regras tiradas dos metadados, em funções puras testadas (`core/metadados/regras-metadados.ts`):
  - **entidade filha** = tem um campo REFERENCIA **obrigatório** com ação de exclusão **CASCATA** para outra entidade (ex.:
    `cenario_teste` → `relatorio_teste`). Filhas não aparecem no menu e são editadas num diálogo, no detalhe do pai;
  - **menu**: entidades por equipe ("principais") e globais ("cadastros de apoio"), na `ordem` dos metadados;
  - **campos dependentes** (`dependeDe`, RN21): desabilitados até a origem ter valor.
- Os metadados são carregados uma vez por sessão (`MetadadosService`). Se faltar informação no endpoint, ela é **acrescentada no
  back-end** (mudança aditiva na v1), não deduzida no front: assim entraram `ordem` (entidade) e `ativo` (campo).

## Consequências

- ✅ Listagens criadas pela administração de metadados aparecem no front sem código novo.
- ✅ Tipo de dado novo = um componente novo e um `@case` em `campo-formulario.component.ts` (passo a passo em `frontend/AGENTS.md`).
- ⚠️ Telas muito específicas de um tipo de item (painéis, gráficos) não cabem neste modelo: quando forem necessárias, entram como
  telas próprias **além** das genéricas, com um ADR.
- ⚠️ A regra de "filha" depende de a modelagem usar CASCATA só para composição. Uma CASCATA obrigatória usada com outro sentido
  esconderia a entidade do menu.
