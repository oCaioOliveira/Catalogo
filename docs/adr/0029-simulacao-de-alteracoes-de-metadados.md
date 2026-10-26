# 0029. Simulação de alterações de metadados com ?simular=true

- Status: Aceito
- Data: 2026-10-21

## Contexto

As alterações de metadados são feitas sobre dados reais e podem ser bloqueadas (ADR 0026) ou reescrever muitos registros (valor
padrão). O administrador precisa ver o efeito **antes** de confirmar, e o front precisa de um jeito de mostrar isso numa tela de confirmação.

## Decisão

- Toda escrita da administração aceita **`?simular=true`** (RN56). Ela executa **o mesmo caminho** da gravação (validações de
  configuração, versão, verificações contra os dados, na mesma transação Serializable) e, no fim, **desfaz a transação**.
- A resposta é `{ permitido, simulado: true, impactos, grupo: null }`. Os `impactos` são o **mesmo** relatório que o 409 traria; nos
  casos permitidos, mostram o efeito (ex.: quantos registros vão receber o valor padrão).
- Erros de configuração continuam sendo 400 também na simulação.
- A resposta da gravação tem o **mesmo formato** (`permitido: true`, `simulado: false`, os impactos e o grupo como ficou), para o front
  tratar as duas do mesmo jeito.

### Alternativas descartadas

- **Endpoints separados de "pré-visualização"**: duplicariam cada operação e poderiam divergir das regras da gravação.
- **Simular fora da transação**: o resultado poderia mudar entre a simulação e a gravação de um jeito diferente do que a gravação
  verificaria; com o mesmo caminho, a única diferença é o commit.

## Consequências

- ✅ Um caminho só para validar: a simulação nunca diverge da gravação.
- ✅ O front monta a confirmação ("isto vai preencher 1.240 registros") sem lógica própria.
- ⚠️ A simulação também trava, por um instante, o que lê (Serializable). Não deve ser usada em laço.
