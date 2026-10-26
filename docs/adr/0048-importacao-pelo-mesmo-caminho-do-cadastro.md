# 0048. Importação de planilhas pelo mesmo caminho de gravação do cadastro manual, com prévia por simulação e arquivo no banco

- Status: Aceito
- Data: 2026-10-26

## Contexto

As equipes precisam cadastrar centenas de itens de uma vez (relatórios, cenários), inclusive com referências a outros tipos de item.
As regras do cadastro (tipos, obrigatórios, referências, dependências, unicidade, equipes, aprovação, histórico) estão espalhadas por
peças do Domain e da Application e já são complexas; reescrevê-las "em lote" para uma importação criaria uma segunda implementação, que
divergiria com o tempo. Ao mesmo tempo, validar 5.000 linhas com uma consulta por linha seria lento, e o deploy é on premise, com mais de
um servidor possível e sem disco compartilhado garantido.

## Decisão

- **Um caminho só para gravar**: `GravadorRegistro` (Application) cria e altera registros (equipe, permissão, validação, dados,
  aprovação e histórico). `CriarRegistro` e `AtualizarRegistro` passaram a usá-lo, e a importação também, linha a linha. O que a
  importação precisa a mais é **ler em lote**: o `PreparadorRegistro` aceita um `LotePreparacao` (itens referenciados e donos das chaves
  únicas, carregados de uma vez) e aplica exatamente as mesmas regras sem consultar o banco por linha.
- **A prévia é uma simulação real**: cada linha passa pelo gravador e tudo o que foi marcado para gravar é descartado
  (`IUnidadeDeTrabalho.DescartarAlteracoes`). O resultado de cada linha é guardado (`Importacao.Resumo`).
- **A execução repete a simulação** e só grava (num único SaveChanges, uma transação) se o resultado for o mesmo da prévia; senão,
  nada é gravado, a prévia nova é guardada e a resposta é 409. Assim o usuário confirma exatamente o que viu.
- **Referências pelo nome** do item (o campo de exibição), resolvidas no escopo de visibilidade da listagem, com a coluna "(ID)" para
  nomes repetidos.
- **O arquivo fica no banco** (`ImportacaoArquivo`, `varbinary(max)`) por 7 dias, apagado por um serviço em segundo plano: funciona igual
  em qualquer servidor, sem configurar pasta.
- **Desfazer** usa o mesmo plano de exclusão (com vários alvos) e é recusado se algum item foi alterado depois ou é referenciado por
  itens de fora.

### Alternativas descartadas

- **Gravação em massa (bulk insert)** direto nas tabelas: rápida, mas pularia o histórico, a aprovação, o `RegistroValor` e as chaves
  únicas, ou os reimplementaria.
- **Validação própria da importação** (sem passar pelo gravador): duas implementações das mesmas regras.
- **Prévia só com validação, sem simular a gravação**: a prévia poderia dizer "ok" e a gravação falhar por uma regra esquecida.
- **Arquivo em disco ou em armazenamento externo**: exigiria pasta compartilhada entre servidores ou um serviço novo.
- **Importar vários tipos de item num arquivo**: ordem de gravação, referências entre linhas ainda não gravadas e desfazer muito mais
  complexos; duas etapas (pai, depois filho) resolvem com o mesmo fluxo.

## Consequências

- ✅ Toda regra nova do cadastro vale na hora para a importação, sem código a mais.
- ✅ A prévia é fiel: o que ela mostra é o que a gravação faria.
- ✅ Uma consulta por tipo de item referenciado e uma para as chaves únicas, em LINQ (ADR 0011): 5.000 linhas validam em segundos.
- ⚠️ A simulação roda duas vezes (prévia e execução) e mantém os itens rastreados na memória até o SaveChanges: com o limite de 5.000
  linhas, a execução leva alguns segundos (medido em `DesempenhoImportacaoTests`).
- ⚠️ O banco guarda arquivos por até 7 dias (até 5 MB cada).
