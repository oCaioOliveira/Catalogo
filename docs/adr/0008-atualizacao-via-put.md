# 0008. Atualização via PUT com o registro inteiro

- Status: Aceito
- Data: 2026-10-21

## Contexto

Atualizar só alguns campos (PATCH) exigiria distinguir "campo não enviado" de "campo apagado" e
validar dependências e unicidade com parte dos valores vindo do banco e parte do corpo. O formulário
do front sempre tem o registro inteiro em mãos.

## Decisão

- `PUT .../registros/{id}` recebe o registro **inteiro**, no mesmo formato plano do POST, mais a `versao`.
- Campo opcional ausente no corpo = campo **removido** (RN15).
- A validação é a mesma do criar, com o mesmo código (`PreparadorRegistro` e `SincronizadorRegistro`).

## Consequências

- ✅ Uma regra só para criar e atualizar, sem casos especiais.
- ⚠️ O cliente precisa mandar todos os campos. Esquecer um campo opcional o apaga.
