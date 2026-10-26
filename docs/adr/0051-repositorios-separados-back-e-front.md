# 0051. Repositórios separados para o back-end e o front-end, com cópia do contrato no front

- Status: Aceito
- Data: 2026-10-26

## Contexto

O ADR 0007 organizou um repositório único com uma pasta por aplicação (`backend/`, `frontend/`) e, na raiz, o que era compartilhado
(`docs/`, `database/`, o `AGENTS.md` como mapa). O front-end passa a ter um repositório próprio, com ciclo de revisão e de entrega
separado. No repositório único, o front dependia de arquivos de fora da sua pasta: o contrato (`../backend/openapi/v1.json`, lido pelo
`npm run gerar-api`), o `AGENTS.md` da raiz e os docs de `docs/` (guia de interface, guias do usuário, ADRs do front).

## Decisão

- **Repositório do back-end** (este): `backend/`, `database/` e `docs/` com o que vale para os dois lados ou só para a API: regras de
  negócio (`RNxx`), modelo de dados, arquitetura, segurança, áreas de gestão, guia da API (`docs/api/`), a **fonte** do glossário e os
  ADRs que mexem na API ou no banco. O `AGENTS.md` da raiz descreve só este repositório.
- **Repositório do front-end**: o conteúdo de `frontend/` vira a raiz. O `AGENTS.md` dele passa a ser o da raiz (com as regras gerais
  que valiam para o front), e ele ganha um `docs/` próprio: `frontend.md`, `design/` (guia de interface e revisão de usabilidade),
  `guias/` (guias do usuário, com capturas de tela), os ADRs só do front (0033 a 0037, 0039 a 0041 e 0044) e uma **cópia** do
  glossário.
- **Contrato**: o front guarda uma **cópia fiel** do `backend/openapi/v1.json` em `openapi/v1.json`, de onde o `npm run gerar-api`
  gera o cliente. A fonte continua no back-end, conferida pelo `ContratoOpenApiTests` (ADR 0024). Mudou o contrato? Depois do merge
  no back-end, a cópia é atualizada no front com `npm run gerar-api` e o build no mesmo commit.
- **Numeração de ADR única** para os dois repositórios: antes de criar um ADR, veja o último número nos dois. Decisão que mexe nos dois
  lados fica no back-end.
- Referências de um repositório ao outro são textuais (caminho e "back-end" ou "front-end"), sem links relativos que quebrariam.

## Consequências

- ✅ O front é compilado, testado e entregue sem precisar de um clone do back-end (só os E2E precisam da API no ar).
- ✅ Quem trabalha no front (pessoa ou agente) encontra no próprio repositório as regras, o guia de interface e os ADRs dele.
- ⚠️ A cópia do contrato e a do glossário podem ficar desatualizadas: a definição de pronto dos dois lados pede a atualização no mesmo
  ciclo da mudança.
- ⚠️ Regras de negócio e guia da API ficam só no back-end: o front os cita pelo id (`RNxx`) e pelo caminho.
- Substitui o ADR 0007 e altera a origem do contrato descrita no ADR 0036.
