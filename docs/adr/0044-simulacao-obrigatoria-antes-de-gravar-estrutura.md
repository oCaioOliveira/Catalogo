# 0044. Simulação obrigatória antes de gravar alterações de estrutura (na interface)

- Status: Aceito
- Data: 2026-10-22

## Contexto

A API já simula qualquer alteração de metadados com `?simular=true` (RN56, ADR 0029), com o mesmo relatório que o bloqueio traria. Com
as telas de administração de listagens, quem altera a estrutura é um administrador, não necessariamente técnico, e as alterações
mexem em dados reais: tornar um campo obrigatório pode preencher centenas de itens com um valor padrão; diminuir um tamanho pode ser
bloqueado por itens que nem estão na tela. Descobrir isso só no 409 da gravação, ou depois dela, é tarde.

## Decisão

- **Toda** alteração de estrutura feita pela interface (listagem, tipo de item, campo, ordem, unicidade, dependência, ativação,
  exclusão) passa por um único executor no front, o `AlteracaoEstruturaService`, que **primeiro chama o mesmo endpoint com
  `?simular=true`** e decide pelo resultado (regras puras em `modelo/simulacao.ts`):
  - **permitido sem impacto** → grava direto;
  - **permitido com impacto** (itens que recebem o valor padrão, chaves únicas geradas) → **diálogo de confirmação** com o resumo;
  - **campo que passa a ser obrigatório com itens vazios** (RN60, RN61) → pede o **valor padrão** com o mesmo componente de campo do
    tipo (e a validação dele), simula de novo com o valor e confirma;
  - **bloqueado** → **relatório de impacto**: quantidade, exemplos com link para cada item, grupos de duplicados, usos e a orientação
    do que fazer. Sem botão de confirmar;
  - **409 de versão** → avisa que outra pessoa alterou a estrutura e a tela recarrega;
  - **400** → os erros vão para junto de cada controle.
- Impacto que não atinge nenhum item (quantidade 0, sem usos) não pede confirmação. O relatório de bloqueio mostra só o que bloqueia.
- Só depois da confirmação vem a gravação (o mesmo endpoint sem `simular`). Se a gravação ainda assim for bloqueada (alguém gravou
  itens entre a simulação e a confirmação), o 409 mostra o mesmo relatório.

### Alternativas descartadas

- **Gravar direto e tratar o 409**: o bloqueio aparece igual, mas os efeitos permitidos (valor padrão em 120 itens) acontecem sem a
  pessoa ver antes.
- **Simular só as alterações "perigosas"**: a tela teria de saber quais são, duplicando as regras da API (RN58 a RN77); simular
  sempre custa uma chamada a mais e não tem exceção para esquecer.
- **Confirmação genérica ("tem certeza?") em tudo**: treina a pessoa a clicar sem ler; o diálogo aparece só quando há efeito real.

## Consequências

- ✅ O administrador vê o efeito antes de cada alteração, com links para os itens afetados.
- ✅ Um caminho só no front: telas novas de administração usam o mesmo executor e o mesmo diálogo.
- ⚠️ Toda alteração faz duas chamadas (simulação e gravação). Em entidades grandes, as duas abrem a transação Serializable (ADR 0029).
