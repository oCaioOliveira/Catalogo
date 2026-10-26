# 0017. Escopo global ou por equipe definido na Entidade

- Status: Aceito
- Data: 2026-10-21

## Contexto

Equipes diferentes não podem ver os registros umas das outras. Mas algumas tabelas são de apoio e
compartilhadas por todas (comunidades, siglas, ambientes, status). Decidir isso registro a registro
seria confuso; por perfil, não faria sentido.

## Decisão

- Nova coluna `Entidade.EscopoEquipe` (padrão 1):
  - **1, por equipe**: cada registro tem `Registro.EquipeId`;
  - **0, global**: registros sem equipe, vistos por todos e alterados só pelo administrador (RN27).
- A equipe de um registro é escolhida na criação (`equipeId`) e não muda.
- Referências só dentro da mesma equipe ou para globais (RN29). A unicidade das entidades por equipe vale dentro da equipe (RN09).
- No grupo `testes_desempenho`: globais `comunidade`, `sigla`, `ambiente` e `status_resultado`; as demais
  por equipe. **Classificação a confirmar com o negócio** ([listagem](../listagens/testes-desempenho.md#escopo-das-entidades-a-confirmar)).

## Consequências

- ✅ O isolamento é uma propriedade dos metadados, sem código por listagem.
- ✅ Exclusões em cascata nunca atravessam equipes.
- ⚠️ Mudar o escopo de uma entidade com dados exige script de migração dos registros (preencher ou limpar
  `EquipeId`, conferir referências e recalcular os hashes de unicidade).
