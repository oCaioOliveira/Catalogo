# 0027. Chaves de metadados imutáveis

- Status: Aceito
- Data: 2026-10-21

## Contexto

As chaves de grupo, entidade, campo e restrição aparecem em muitos lugares: nas URLs (`/grupos/{grupo}/entidades/{entidade}`), no
JSON de `Registro.Dados`, nos filtros e na ordenação (`valor.de=100`), nos critérios de exportação, no histórico e no código do front.
Renomear uma chave exigiria reescrever o JSON de todos os registros e quebraria o front e qualquer integração.

## Decisão

- **Chaves são imutáveis** depois de criadas (RN51). No PUT, a chave pode vir igual ou não vir; diferente, 400.
- O **nome** e a **descrição** (o que o usuário vê) podem mudar à vontade.
- Num campo REFERENCIA, a entidade referenciada também é imutável (mudar de destino invalidaria as referências existentes).
- Para mudar de verdade: criar o campo novo e desativar o antigo (ADR 0028).

### Alternativas descartadas

- **Renomear com migração** (reescrever `Dados` e o histórico): caro, arriscado e ainda quebraria o front e os filtros salvos.
- **Usar o id nas URLs e no JSON**: ilegível para o time e para o front, e mudaria o contrato inteiro da v1.

## Consequências

- ✅ URLs, JSON e contrato com o front estáveis.
- ⚠️ Uma chave mal escolhida fica para sempre: revise antes de criar (o nome pode ser corrigido).
