# 0047. Itens relacionados descobertos pelos metadados, com a contagem num único endpoint e os itens pela listagem filtrada

- Status: Aceito
- Data: 2026-10-24

## Contexto

O detalhe de um item mostrava só as entidades **filhas** (REFERENCIA obrigatória com `CASCATA`, como Cenários e Links dentro do
Relatório). Os demais itens que apontam para ele não apareciam: ao abrir um Projeto, o usuário não via os Relatórios de Teste nem os
Endpoints dele. As listagens são criadas pela interface (ADR 0025) e as telas são genéricas (ADR 0034): não dá para escrever, por tipo
de item, "o projeto mostra os relatórios". E a visibilidade (equipes na listagem, aprovação, globais) precisa ser a mesma da listagem
(RN26, RN37, RN91), sem uma segunda implementação.

## Decisão

- **As relações são descobertas pelos metadados** (`Relacoes.QueApontamPara`, Domain): toda entidade ativa do grupo com um campo
  REFERENCIA ativo que aponta para a entidade do item. Nenhuma configuração por tipo de item; uma referência criada na administração
  já gera a seção.
- **Um endpoint só para as quantidades**: `GET .../registros/{id}/relacionados` devolve uma entrada por campo, com a quantidade que o
  usuário vê, inclusive zero. O detalhe faz **uma** chamada para montar todas as seções.
- **A contagem é um SQL só, no banco** (`ConsultaRelacionadosLinq`): `RegistroReferencia` filtrado pelo item, restrito às origens
  visíveis (o `ConsultaRegistrosLinq.Filtrar` de cada entidade de origem, com o escopo da listagem, juntado com `UNION ALL`) e agrupado
  por campo. O escopo é o mesmo `PreparadorConsultaRegistros.Escopo` da listagem e do painel.
- **Os itens de cada seção vêm da listagem filtrada** que já existia (`?<campo>=<id>`): paginação, ordenação, situação de aprovação e
  médias de avaliação de graça, e "Ver todos na listagem" abre a mesma consulta na tela de listagem.
- **No front**: as filhas continuam editáveis no detalhe (sempre visíveis, com a quantidade no título); as demais relações viram seções
  **somente leitura**, só quando têm itens, com "Ver todos" e "Novo …" (o formulário de criação com a referência já preenchida). As
  seções são recolhíveis e cada uma carrega a tabela só quando está aberta.

### Alternativas descartadas

- **Configurar quais relações aparecem em cada tipo de item**: trabalho manual a cada listagem nova, e fora do escopo.
- **Um endpoint que devolve os itens de todas as relações**: uma resposta grande, sem paginação por seção, e uma segunda forma de
  listar registros (com o risco de divergir da visibilidade da listagem).
- **Contar com uma consulta por relação**: N idas ao banco por detalhe; o agrupamento por campo resolve numa só.
- **Contar no front** (`total` da listagem de cada relação): N chamadas por detalhe, inclusive para relações vazias.

## Consequências

- ✅ Toda listagem, inclusive as criadas pela interface, ganha a navegação "de volta" pelas referências, sem código novo.
- ✅ A quantidade e os itens obedecem à mesma regra de visibilidade, porque partem do mesmo escopo e da mesma consulta.
- ✅ Um detalhe faz uma chamada de contagem; as tabelas só são buscadas para as seções abertas.
- ⚠️ Só um nível de relação (os cenários de um projeto através dos relatórios não aparecem).
- ⚠️ Um item global muito usado (um Ambiente) pode ter muitas linhas na seção: a tabela é paginada (10 por página) e "Ver todos"
  leva à listagem com todos os filtros.
