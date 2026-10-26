# 0026. Bloquear alterações de metadados incompatíveis com os dados, sem migração automática

- Status: Aceito
- Data: 2026-10-21

## Contexto

Com a administração pela API (ADR 0025), um administrador pode, por exemplo, diminuir o tamanho de um texto que já tem valores
maiores, tornar obrigatório um campo vazio em metade dos registros ou criar uma restrição única com duplicatas. Se isso fosse
aceito, ficariam no banco registros que a própria aplicação considera inválidos: o próximo PUT deles falharia sem o usuário entender por quê.

## Decisão

- **Toda mudança é validada contra os dados existentes. Se algum registro ficaria inválido, ela é bloqueada** (409
  `ALTERACAO_INCOMPATIVEL_COM_DADOS`) com um **relatório de impacto**: quantidade de registros e até 10 exemplos com id e rótulo.
- O Domain (`PlanejadorAlteracoes`) diz **o que** verificar para cada mudança; a Infrastructure responde em **LINQ**
  (`VerificadorDadosMetadados`). Cada regra de alteração tem uma verificação e um teste.
- A verificação e a gravação rodam numa **transação Serializable**: um registro inserido no meio não escapa.
- **Nenhuma migração automática.** A única escrita em massa pedida pelo administrador é o **`valorPadrao`** (campo obrigatório novo,
  ou campo que vira obrigatório: só os vazios). A outra escrita em massa é técnica e não muda valores: gerar e recalcular as chaves
  únicas. As duas são feitas em lotes de 500, mantendo `Dados`, `RegistroValor` e `RegistroChaveUnica` juntos.
- O tipo de um campo não muda (salvo TEXTO → TEXTO_LONGO): o caminho é criar um campo novo e desativar o antigo.

### Alternativas descartadas

- **Migrar automaticamente** (truncar textos, arredondar números, converter tipos): perde dados sem o usuário ver, e cada
  conversão teria casos de borda (o que fazer com "abc" num campo que vira INTEIRO?).
- **Aceitar e marcar registros como inválidos**: espalharia o problema pelas telas, e a listagem, a exportação e a unicidade
  teriam de conviver com registros fora das regras.

## Consequências

- ✅ Os dados nunca ficam fora das regras dos metadados.
- ✅ O administrador vê exatamente quais registros impedem a mudança e pode corrigi-los antes.
- ⚠️ Algumas mudanças exigem trabalho manual (corrigir os registros, ou criar campo novo e desativar o antigo).
- ⚠️ A transação Serializable trava o que foi lido até o fim: numa entidade grande, a alteração bloqueia gravações nela por alguns
  segundos. Deadlock vira 409 (tente de novo).
