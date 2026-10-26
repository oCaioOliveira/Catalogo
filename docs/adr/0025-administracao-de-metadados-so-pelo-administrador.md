# 0025. Administração de metadados pela API, só pelo administrador

- Status: Aceito
- Data: 2026-10-21

## Contexto

Até aqui, criar uma listagem ou mudar um campo exigia um script SQL (`V00x`), escrito por um desenvolvedor e aplicado pelo DBA.
Isso deixava o negócio dependente do time técnico para algo que é configuração. Os metadados (grupos, entidades, campos,
restrições, dependências) são **compartilhados por todas as equipes**: a mesma entidade `relatorio_teste` serve a todas elas.

## Decisão

- Os metadados passam a ser criados e alterados **pela API**, em `/api/v1/admin/metadados`, separada da API de uso.
- **Só o ADMINISTRADOR** administra metadados (`PoliticaPermissao.PodeAdministrarMetadados`, RN50). GESTOR, INTERNO e EXTERNO
  recebem 403 em toda a rota, inclusive nas leituras.
- O GESTOR continua configurando o que é **da equipe dele**: aprovação (RN33) e modelo de avaliação (RN40).
- Scripts `V00x` ficam só para mudanças de **schema**. A listagem de Testes de Desempenho continua sendo a carga do V001.

### Alternativas descartadas

- **Gestores administrando metadados**: a mudança de um gestor (ex.: tornar um campo obrigatório) afetaria as outras equipes.
- **Continuar só por script**: mantém a dependência do time técnico e não dá ao administrador a simulação nem o relatório de impacto.

### Evolução possível

Listagens **próprias de uma equipe** (um grupo com dono), administradas pelo gestor dela. Exigiria um dono no `Grupo`, a regra na
`PoliticaPermissao` e decidir o que fazer com entidades referenciadas por outras equipes. Fica para um ADR novo, se o negócio pedir.

## Consequências

- ✅ O negócio cria e evolui listagens sem desenvolvimento nem deploy.
- ✅ Uma regra só de quem pode, na política central.
- ⚠️ Toda mudança passa a acontecer em produção, com dados reais: por isso ela é sempre validada contra os dados (ADR 0026) e simulável (ADR 0029).
