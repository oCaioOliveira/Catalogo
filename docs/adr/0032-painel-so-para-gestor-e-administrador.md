# 0032. Painel só para GESTOR e ADMINISTRADOR, com a visibilidade da listagem

- Status: Aceito
- Data: 2026-10-21

## Contexto

O painel mostra números consolidados das equipes: taxa de rejeição, tempo de decisão, quem mais cadastra. É informação de gestão. Ao
mesmo tempo, os números não podem revelar registros que o usuário não veria na listagem (outra equipe, pendente de outra pessoa, RN26 e
RN37).

## Decisão

- Acesso (RN78): **GESTOR** e **ADMINISTRADOR**. INTERNO e EXTERNO recebem 403 `SEM_PERMISSAO`. A regra fica na `PoliticaPermissao`
  (`PodeVerPainel`), como todas as de "quem pode".
- Visibilidade (RN79): **a mesma da listagem**, montada pelo mesmo `PreparadorConsultaRegistros` e aplicada pela mesma
  `ConsultaRegistrosLinq.Filtrar`. O painel não tem regra de visibilidade própria: o GESTOR vê as equipes dele e as entidades globais;
  o ADMINISTRADOR, tudo; o filtro `equipeId` segue a regra da listagem (403 para equipe alheia).
- A atividade (RN84) usa a equipe gravada no histórico para a mesma regra.

### Alternativas descartadas

- **Painel para todos os perfis**: INTERNO e EXTERNO veriam indicadores de gestão (rejeições, ranking de pessoas) sem necessidade.
- **Visibilidade própria do painel** (ex.: o GESTOR ver todas as equipes de forma agregada): duas regras para manter e risco de vazar
  números de outras equipes.

## Consequências

- ✅ Um teste garante que um registro da Equipe B não muda nenhum número do GESTOR da Equipe A.
- ✅ **Abrir para o INTERNO no futuro** é mudar só `PodeVerPainel` (e a RN78): como os números já respeitam a visibilidade do usuário,
  o INTERNO veria só o que já vê na listagem. Antes de abrir, decidir se a atividade por pessoa (RN84) deve aparecer para ele — isso
  pede um ADR novo.
- ⚠️ O EXTERNO só vê na listagem os pendentes que ele criou (RN37); se um dia tiver painel, os números dele serão diferentes dos do
  gestor da mesma equipe, por construção.
