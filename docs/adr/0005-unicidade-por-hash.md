# 0005. Unicidade por hash em RegistroChaveUnica

- Status: Aceito
- Data: 2026-10-21

## Contexto

Com os campos dentro de um JSON (e as referências em outra tabela), não dá para criar um UNIQUE
do SQL Server sobre "nome da comunidade" ou "relatório + número do cenário". Conferir só na
aplicação (`SELECT` antes do `INSERT`) falha com duas gravações simultâneas.

## Decisão

- Para cada `RestricaoUnica`, a aplicação calcula o **SHA-256 dos valores normalizados** dos campos
  e grava em `RegistroChaveUnica`, na mesma transação do registro.
- O banco tem UNIQUE (`RestricaoUnicaId`, `ValorHash`): a regra vale mesmo com concorrência.
- A normalização segue a collation `Latin1_General_CI_AI` (sem acento e sem diferença de
  maiúsculas) e está descrita em [RN09](../regras-de-negocio.md#rn09--unicidade) e no comentário da `CalculadoraChaveUnica`.
- Valor nulo não gera chave (RN10), igual ao UNIQUE do SQL.
- A aplicação também confere antes de gravar, para dar 409 com mensagem amigável. Se o UNIQUE do
  banco barrar (2601/2627), a resposta também é 409.

## Consequências

- ✅ Unicidade garantida pelo banco, simples ou composta, com qualquer tipo de campo.
- ⚠️ Mudar a normalização (ou a ordem dos campos de uma restrição) muda os hashes. Os existentes
  precisam ser recalculados por script.
- ⚠️ Criar uma restrição nova numa entidade que já tem dados exige calcular os hashes dos registros existentes.
