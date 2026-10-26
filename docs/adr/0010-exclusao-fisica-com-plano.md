# 0010. Exclusão física com plano de exclusão

- Status: Aceito
- Data: 2026-10-21

## Contexto

Os registros se relacionam por `RegistroReferencia`, e cada campo REFERENCIA tem uma `AcaoExclusao`
(RESTRINGIR ou CASCATA). O banco só garante o RESTRINGIR (FK sem CASCADE no destino). Excluir em
cascata exige percorrer o grafo de referências, que pode ter vários níveis e até ciclos. Exclusão
lógica e auditoria estão fora do escopo por enquanto.

## Decisão

- Exclusão **física**.
- Antes de excluir, monta-se um **plano** (RN16): percorre quem aponta para o registro, aplica
  CASCATA (entra no plano, recursivo) e RESTRINGIR (bloqueia, a menos que quem aponta também esteja
  no plano), sem revisitar registros.
- **Tudo ou nada**: qualquer bloqueio impede a exclusão inteira (409 com as dependências).
- O plano é **código puro no Domain** (`PlanejadorExclusao`), testado sem banco. A Infrastructure
  só busca as referências e executa a exclusão, dos filhos para o pai, numa única transação.

## Consequências

- ✅ Regra testável e explicável ao usuário (o 409 diz o que impede).
- ✅ O RESTRINGIR continua garantido pelo banco, mesmo se a aplicação errar.
- ⚠️ Não há como desfazer uma exclusão. Exclusão lógica, se vier, precisa de um ADR novo.
- ⚠️ Uma cascata muito grande exclui muitos registros numa transação só.
