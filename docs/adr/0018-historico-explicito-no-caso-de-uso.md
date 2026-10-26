# 0018. Histórico gravado explicitamente pelo caso de uso, sem interceptor

- Status: Aceito
- Data: 2026-10-21

## Contexto

Toda alteração de registro, usuário e equipe precisa de histórico (RN32). Uma alternativa comum é
interceptar o `SaveChanges` do EF e gerar o histórico automaticamente a partir do change tracker. Isso
esconde a regra (quem lê o caso de uso não vê o histórico), grava o formato das tabelas, e não o formato
da API, e torna difícil excluir campos sensíveis como o hash da senha.

## Decisão

- Os casos de uso chamam **explicitamente** o `ServicoHistorico` (Application/Comum), passando o objeto
  plano de antes e de depois, no formato da API.
- A linha entra no **mesmo SaveChanges** da operação (na exclusão, na mesma transação dos DELETEs).
- O `ComparadorAlteracoes` (Domain) calcula as mudanças por campo na hora da consulta.

## Consequências

- ✅ Quem lê `CriarRegistro`, `AtualizarUsuario`... vê exatamente o que vai para o histórico.
- ✅ O histórico de usuário nunca tem senha: o objeto é montado por `ParaJsonHistorico`, que não a inclui.
- ⚠️ Esquecer de chamar o serviço num caso de uso novo deixa a operação sem histórico. Os AGENTS.md e os
  testes de integração de histórico existem para pegar isso.
