# 0046. Listagens habilitadas por equipe, geridas pelo ADMINISTRADOR, com itens ocultos (não apagados) ao tirar uma equipe

- Status: Aceito
- Data: 2026-10-24

## Contexto

Até a etapa 6, toda listagem (grupo) aparecia para todos os usuários; o isolamento por equipe valia só para os **itens** (RN27,
ADR 0017). Com várias listagens criadas pela interface (ADR 0025), isso desorganiza o uso: quem é da equipe de testes vê no menu uma
listagem "Tasks de Desenvolvimento" e pode criar itens nela; um gestor de duas equipes não separa, nas tabelas, os itens de cada uma.

Restrições: a regra de "quem vê o quê" precisa continuar num lugar só (`PoliticaPermissao`, RN26), aplicada **dentro da consulta**
(LINQ, ADR 0011); a mudança é aditiva na v1 (ADR 0022); nada pode mudar para quem já usa o sistema.

## Decisão

- **Tabela `dbo.GrupoEquipe`** (V009): as equipes habilitadas em cada listagem. A carga inicial mantém tudo como era:
  `testes_desempenho` para todas as equipes existentes; cada outra listagem para as equipes que já têm itens nela.
- **Visibilidade da listagem** (RN90): o não-administrador vê uma listagem ativa se pelo menos uma das suas equipes ativas está
  habilitada nela; o ADMINISTRADOR vê todas. Listagem não visível é **404** em tudo o que está sob ela (como a RN28: a API não revela
  que existe), e `GET /grupos` só devolve as visíveis.
- **"As equipes do usuário nesta listagem"** (RN91) = equipes ativas do usuário ∩ equipes habilitadas (para o ADMINISTRADOR, as
  habilitadas e ativas). Em vez de espalhar a interseção, a política produz o **usuário como ele age dentro da listagem**
  (`PoliticaPermissao.NaListagem`): todas as regras que já existiam (itens, criação, opções, aprovação, avaliação, configurações,
  painel, fila) passam a usá-lo sem mudar a lógica delas.
- **Quem gerencia: só o ADMINISTRADOR** (RN50), como o resto dos metadados, em `GET`/`PUT /admin/metadados/grupos/{grupo}/equipes`, com
  a lista completa, a `versao` do grupo, `?simular=true` e histórico `METADADO` — o mesmo executor das outras alterações de estrutura.
- **Tirar uma equipe com itens é permitido, com aviso** (RN92): os itens **não são apagados**, ficam ocultos para os membros e
  visíveis só para o ADMINISTRADOR; ao habilitar de novo, tudo volta. A simulação informa a quantidade (`ITENS_OCULTOS`).
- **Listagem nova nasce sem equipes** e **equipe nova não é habilitada em nenhuma** (RN93): liberar uma listagem é um ato explícito.
- `GET /grupos` traz, por listagem, `minhasEquipes` e `possuiEquipes`: o front monta selects, a coluna e o filtro de Equipe e a faixa de
  aviso sem chamadas extras.

### Alternativas descartadas

- **Gestor habilita listagens para a sua equipe**: a listagem é compartilhada (RN50); um gestor mudaria o que outras equipes veem.
  Fica fora do escopo.
- **Bloquear a remoção de uma equipe com itens** (como a RN58 faz com dados inválidos): obrigaria a apagar ou mover itens para
  reorganizar listagens. Ocultar é reversível e não perde nada.
- **Apagar ou mover os itens da equipe removida**: irreversível e contra a RN31 (nada de equipe é excluído).
- **Listagem nova visível para todos até alguém restringir**: uma listagem pela metade apareceria para todo mundo durante a montagem.
- **Perfil diferente por equipe** ou **"equipe ativa" na sessão**: mudanças maiores no modelo de acesso (ADR 0014), fora do escopo.

## Consequências

- ✅ Cada equipe vê só as listagens dela; o seletor, os selects de equipe, o painel, as configurações e a fila ficam coerentes.
- ✅ A regra continua num lugar só; as consultas continuam em LINQ, com a interseção aplicada no SQL.
- ✅ Reorganizar listagens é reversível: tirar e devolver uma equipe não perde item, aprovação nem avaliação.
- ⚠️ Itens ocultos continuam no banco com a equipe deles: entram na unicidade (RN09) e nas referências (RN29) da equipe, mas como os
  membros não usam a listagem enquanto ela está fora, não há conflito; ao reabilitar, tudo volta como estava. O ADMINISTRADOR os vê
  como de "Equipe não habilitada".
- ⚠️ Os metadados ficam em memória no front por até 5 minutos: quem altera as equipes de uma listagem vê na hora; as outras sessões,
  no próximo recarregamento (a API decide sempre na hora).
- ⚠️ Toda listagem nova exige um passo a mais (habilitar equipes) antes de ser usada, documentado no guia do administrador.
