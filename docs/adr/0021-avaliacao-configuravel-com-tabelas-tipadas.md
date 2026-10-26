# 0021. Avaliação configurável por entidade e equipe, com tabelas relacionais tipadas

- Status: Aceito
- Data: 2026-10-21

## Contexto

Os itens do catálogo podem ser avaliados pelos usuários, sempre o item inteiro (um registro). O que se avalia
muda por equipe: a equipe A avalia os Relatórios de Teste só pela "Qualidade", de 1 a 5 estrelas; a equipe B usa
cinco critérios, alguns com comentário obrigatório; a equipe C não avalia. Precisamos de médias, contagens e
ordenação pela média na listagem, e os formulários vão evoluir com avaliações já feitas.

O motor já guarda os dados dos registros em JSON (ADR 0001), e seria tentador guardar as respostas do mesmo jeito.

## Decisão

- O formulário é um **modelo de avaliação por entidade e por equipe** (`ModeloAvaliacao`), como a aprovação
  (ADR 0020), com no máximo **um ativo** (índice único filtrado). Configuram o GESTOR das suas equipes e o
  ADMINISTRADOR, pela mesma `PoliticaPermissao`.
- Tudo em **tabelas relacionais tipadas**, sem JSON: `CriterioAvaliacao` (perguntas), `Avaliacao` (uma por usuário,
  item e modelo) e `AvaliacaoResposta` (`Estrelas` tinyint e `Texto`). Média e contagem viram LINQ simples
  (`GroupBy`, `Average`, `Count`), traduzidas para `AVG`/`COUNT` no banco, inclusive numa subconsulta para ordenar e
  filtrar a listagem.
- Dois tipos de critério: `ESTRELAS` (1 a 5, com comentário NENHUM, OPCIONAL ou OBRIGATORIO) e `TEXTO`.
- **Evolução sem perder avaliações antigas** (RN43):
  - critérios são identificados pela **chave**;
  - critério **com respostas** não pode ser excluído nem mudar de tipo: é **desativado**, some do formulário e das
    médias, e as respostas ficam;
  - regras novas (critério novo, obrigatoriedade) valem só para avaliações criadas ou editadas depois;
  - o modelo nunca é excluído: desativado, as avaliações dele aparecem separadas, e a equipe cria outro.
- Avaliações são excluídas junto com o registro (CASCADE); o histórico (alvo `AVALIACAO`) guarda o que existia.

### Alternativas descartadas

- **Respostas em JSON** (uma coluna por avaliação, como `Registro.Dados`): média e distribuição exigiriam
  `OPENJSON`/`JSON_VALUE` em SQL escrito à mão (proibido pelo ADR 0011) ou trazer as avaliações para a memória.
  Também não daria para garantir 1 a 5 estrelas nem a FK para o critério no banco.
- **Critérios como metadados (`Campo`) de uma entidade "avaliação"**: reaproveitaria o motor, mas misturaria o
  formulário de avaliação (por equipe) com os metadados do grupo (iguais para todas as equipes) e deixaria as
  médias no EAV genérico.
- **Um modelo por entidade, igual para todas as equipes**: mais simples, mas não atende equipes com critérios diferentes.

## Consequências

- ✅ Médias, distribuição e ordenação pela média são consultas LINQ legíveis, no banco, sem SQL manual.
- ✅ O banco garante a integridade: estrelas de 1 a 5, uma avaliação por usuário, critério existente, chave válida.
- ✅ O formulário evolui sem invalidar notas antigas, e o histórico mostra cada versão.
- ⚠️ Outros tipos de critério (nota de 0 a 10, múltipla escolha) exigem script novo (CHECK de `Tipo`) e código.
- ⚠️ Critérios desativados ficam para sempre no modelo; com muitas mudanças, o melhor é criar um modelo novo.
- ⚠️ O índice único filtrado exige `SET QUOTED_IDENTIFIER ON` ao aplicar o script (o `sqlcmd` usa OFF por padrão).
