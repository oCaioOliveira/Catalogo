# 0028. Desativar em vez de excluir metadados

- Status: Aceito
- Data: 2026-10-21

## Contexto

Excluir um campo com valores apagaria dados; excluir uma entidade com registros (ou que já teve registros) quebraria o histórico, que
é permanente (RN32) e aponta para ela. Mas o negócio precisa tirar de uso campos, entidades e listagens que não servem mais.

## Decisão

- **Desativar** é o caminho normal (RN57). Campo, entidade e grupo têm `Ativo` e podem ser reativados.
  - **Campo inativo**: some dos formulários, da resposta, dos filtros e da exportação, mas os valores ficam, e o PUT de registro os
    preserva (RN69). Reativar devolve tudo.
  - **Entidade ou grupo inativo**: some para os não-administradores (404) e fica somente leitura para o administrador; as referências
    existentes continuam, sem vínculos novos (RN75, RN77).
- **Excluir** só existe para o que nunca foi usado: campo sem nenhum valor e sem usos (RN70); entidade que nunca teve registros e não
  é referenciada (RN76); grupo sem entidades (RN77).

### Alternativas descartadas

- **Exclusão com os dados**: perde informação e quebra o histórico.
- **Só desativar, nunca excluir**: acumularia lixo de configuração criado por engano (ex.: um campo digitado errado e nunca usado).

## Consequências

- ✅ Nenhum dado se perde ao tirar algo de uso, e o histórico continua íntegro.
- ⚠️ Campos inativos continuam ocupando espaço no JSON e em `RegistroValor`.
- ⚠️ Um campo inativo que é obrigatório só pode ser reativado se todos os registros tiverem valor (os criados enquanto ele estava inativo não têm).
