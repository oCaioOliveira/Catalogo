# 0030. Indicadores genéricos, montados pelos metadados

- Status: Aceito. O limite de "um nível de referência, só em dimensões" foi substituído pelo [0038](0038-caminhos-por-referencia.md)
- Data: 2026-10-21

## Contexto

O painel gerencial precisa de números sobre os cadastros: quantos relatórios por status e por mês, a vazão média por ambiente, a taxa
de erro por projeto... O Catalogo é genérico (ADR 0001): os tipos de item vêm de metadados, e amanhã haverá listagens que ainda não
existem. Um endpoint por indicador ("vazão por ambiente") teria de ser escrito, testado e publicado para cada pergunta nova, e só
serviria a Testes de Desempenho.

## Decisão

- Dois tipos de endpoint, ambos sem nada específico de uma listagem:
  - **visão geral** do grupo (`.../painel/grupos/{grupo}/visao-geral`): blocos fixos que valem para qualquer grupo (volume, aprovação,
    avaliação, atividade), calculados a partir das colunas de sistema, do histórico e das avaliações (RN81 a RN84);
  - **agregação configurável** (`.../entidades/{entidade}/indicadores`): o cliente escolhe até **2 dimensões** e **5 métricas**
    (`agruparPor`, `metricas`), conferidas contra os metadados da entidade, e usa os **mesmos filtros** da listagem (RN85 a RN87).
- As regras de cada tipo (o que agrupa, o que soma) ficam num interpretador puro no Domain (`InterpretadorIndicadores`), como os filtros
  da listagem (`InterpretadorConsulta`); a tradução, na Infrastructure.
- Dimensões podem atravessar **um** campo REFERENCIA (`relatorio_teste.ambiente`): as métricas de cenário_teste dependem do ambiente e do
  projeto do relatório, que estão em outra entidade. Um nível só, para a consulta continuar previsível.
- Limites fixos: 2 dimensões, 5 métricas, 500 linhas, 50 valores de TEXTO (o resto vira "Outros").

### Alternativas descartadas

- **Indicadores específicos por listagem**: cada pergunta nova seria código novo; vai contra o motor genérico.
- **Consulta livre (SQL, OData, GraphQL)**: o cliente poderia montar consultas caras ou sem o escopo de segurança; e SQL na URL é
  proibido (ADR 0011).
- **Ferramenta de BI externa lendo o banco**: duplicaria as regras de visibilidade (equipes, pendentes) fora do sistema.

## Consequências

- ✅ Qualquer listagem, inclusive as criadas pela administração de metadados, ganha painel sem código novo.
- ✅ O front monta os gráficos a partir de `dimensoes`/`metricas` (nome e tipo de cada coluna) sem conhecer a listagem.
- ⚠️ Perguntas que precisam de mais de 2 dimensões, de percentis ou de mais de um nível de referência não cabem; ficam para quando
  houver pedido real (um ADR novo).
- ⚠️ Os parâmetros do painel viram chaves reservadas (RN14): um campo não pode se chamar `limite`, `de`, `ate`...
