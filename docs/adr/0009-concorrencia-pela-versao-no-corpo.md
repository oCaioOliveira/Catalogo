# 0009. Concorrência otimista pela versao no corpo do PUT

- Status: Aceito
- Data: 2026-10-21

## Contexto

Dois usuários podem abrir o mesmo registro e salvar em sequência. Sem controle, o segundo apaga
a alteração do primeiro sem saber. `Registro.Versao` (`rowversion`) já existe e o GET já a devolve
em Base64. A alternativa seria o par ETag/If-Match nos headers HTTP.

## Decisão

- O PUT recebe a `versao` **no corpo**, na mesma representação (Base64) que o GET devolve. Para o
  front é o mesmo objeto que ele leu, sem lidar com headers.
- A aplicação compara a versão recebida com a atual (409 se diferente) **e** a configura como
  valor original no EF, para o UPDATE ser `... WHERE Versao = @v`. Isso protege a janela entre a
  leitura e a gravação. `DbUpdateConcurrencyException` também vira 409.
- `versao` passou a ser uma chave reservada (RN14).

## Consequências

- ✅ Nenhuma alteração é perdida em silêncio.
- ⚠️ Não segue a convenção HTTP de ETag/If-Match. Se um cliente externo precisar dela, dá para adicionar depois sem quebrar o corpo.
