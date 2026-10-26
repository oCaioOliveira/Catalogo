# 0049. Schema próprio `catalogo` em todos os ambientes e pacotes de entrega consolidados para os DBAs

- Status: Aceito
- Data: 2026-10-26
- Complementa: [0003](0003-schema-por-scripts-sql.md) (o schema continua definido só por scripts SQL versionados)

## Contexto

O Catálogo vai para o ambiente da CAIXA num banco de produção **que já existe e é compartilhado** com outros sistemas, que têm tabelas
de nomes genéricos em `dbo` (`Usuario`, `Equipe`, `Registro`). Os DBAs criam os objetos rodando scripts **à mão, pelo SSMS**, sem
Docker e sem as nossas ferramentas. Os scripts `V001` a `V011` criavam tudo em `dbo` e espalhavam a configuração da área Testes de
Desempenho (carga no V001, conversões no V007, plurais e ordem no V008, ícones no V010), sem os ajustes feitos depois pela interface;
alguns passos só fazem sentido no desenvolvimento (converter dados existentes no V007, habilitar a área para as equipes existentes no V009).

## Decisão

- **Todos os objetos do Catálogo ficam no schema `catalogo`, em todos os ambientes**, para que não divirjam. O `V012` move as tabelas de
  `dbo` para `catalogo` com `ALTER SCHEMA ... TRANSFER` (preserva os dados) e cria o papel `catalogo_aplicacao`, com `SELECT, INSERT,
  UPDATE, DELETE, EXECUTE ON SCHEMA::catalogo` e nada mais. No código, o nome fica numa constante (`SchemaBanco.Nome`) aplicada por
  `HasDefaultSchema`. Scripts novos escrevem `catalogo.` em todos os objetos.
- **Os `V0xx` continuam a fonte da verdade** da estrutura (desenvolvimento, testes e geração dos pacotes), com a numeração normal.
- **Produção recebe pacotes** em `database/entrega/<data>-<descricao>/`: na instalação inicial, um script de estrutura **consolidado**
  (o resultado de todos os `V`, direto em `catalogo`, registrando as versões como aplicadas), uma **carga gerada a partir do banco de
  desenvolvimento** (só metadados da área, com os mesmos GUIDs), uma verificação só de consultas e um rollback que se recusa a rodar
  com dados de usuários. Cada mudança futura de estrutura vira um novo pacote com script incremental, verificação e rollback.
- A equivalência é **provada**, não presumida: `database/ferramentas/comparar_bancos.sh` compara a estrutura pelos catálogos do sistema
  e a configuração da área (impressão digital SHA-256), e a carga gerada se autoconfere antes do `COMMIT`.
- O primeiro administrador é criado pelo `criar-admin`, no servidor da aplicação: nenhuma senha passa pelos DBAs nem fica em script.

## Consequências

- Nenhum conflito de nomes com os outros sistemas, e o login da aplicação nem enxerga o que está fora de `catalogo` (coberto pelo teste
  `OutroSistemaNoMesmoBancoTests`, que precisa de um user-secret com DDL no banco de testes).
- Há dois caminhos para a mesma estrutura (os `V` e o script consolidado): por isso toda entrega passa pela comparação com diferença zero
  antes de ir para os DBAs.
- Levar outra área de gestão para produção é gerar a carga com `gerar_carga_area.sh`, sem escrever SQL à mão.
- Os `V001` a `V011` mantêm `dbo.` (são imutáveis); um banco novo de desenvolvimento passa por `dbo` e chega a `catalogo` no `V012`.
