# 0045. Telas de aprovação e avaliação condicionais ao recurso ativo, com contador por intervalo

- Status: Aceito
- Data: 2026-10-23

## Contexto

A aprovação (RN33 a RN38, ADR 0020) e a avaliação (RN39 a RN48, ADR 0021) são ligadas **por tipo de item e por equipe**. A maioria das
equipes não usa nenhuma das duas, e para elas o sistema tem de continuar igual: sem coluna de situação, sem faixa, sem aba, sem
contador. A tela é genérica (ADR 0034): ela não pode saber "quais tipos usam aprovação" por código, e também não deve fazer uma
chamada por equipe só para descobrir se mostra uma coluna.

A fila de aprovação precisa de um aviso de que há itens esperando. Notificação em tempo real (WebSocket, SignalR, e-mail) está fora do
escopo, e o deploy é manual, on premise.

## Decisão

- **Cada elemento só aparece onde o recurso está ativo para o tipo de item e a equipe**. Os metadados dizem isso numa chamada só, com
  dois campos aditivos em `GET /api/v1/grupos/{grupo}/entidades` (sem `equipeId`):
  - `equipesComAprovacao`: as equipes **que o usuário vê** com a aprovação ligada para a entidade (vazia = nada de aprovação para ele);
  - `criteriosAvaliacao`: os critérios de **estrelas** dos modelos ativos nessas equipes (colunas, ordenação e filtro da listagem).
  O formulário de avaliação de um item vem da chamada com `equipeId` (`modeloAvaliacao`), que já existia.
- O front decide com funções puras (`core/permissoes/permissoes.ts`, `core/aprovacao/regras-aprovacao.ts`,
  `features/registros/filtros/filtros-url.ts`): coluna e filtro Situação, faixa do detalhe, aviso no formulário, botão "Reenviar",
  aba Avaliações, colunas de média. Um item que **não** está APROVADO mostra a situação mesmo com a aprovação desligada depois, para
  ninguém tomar um rejeitado por aceito.
- As opções dos selects de referência trazem `situacaoAprovacao` (aditivo em `OpcaoDto`), e o front marca "(pendente)" (RN38).
- O **contador de pendentes** do menu "Aprovações" (GESTOR e ADMINISTRADOR) é atualizado **a cada navegação e a cada 2 minutos**
  (`PendentesService`, `GET /api/v1/aprovacoes/pendentes?tamanho=1`). Sem tempo real.

### Alternativas descartadas

- **Chamar a configuração de cada equipe** para montar a listagem: N chamadas por tela, e a mesma regra repetida em vários lugares.
- **Mostrar sempre a coluna Situação** (todo item é APROVADO sem aprovação): polui todas as equipes que não usam o recurso.
- **Tempo real (SignalR) ou e-mail** para o contador: infraestrutura nova e fora do escopo; 2 minutos bastam para uma fila de decisões.

## Consequências

- ✅ As equipes sem aprovação nem avaliação não veem nada de novo (o E2E confere nas equipes de desenvolvimento).
- ✅ Uma chamada de metadados resolve a listagem; a regra de "onde aparece" fica em funções puras testadas.
- ⚠️ O contador pode ficar até 2 minutos atrasado em relação a um item novo de outra pessoa (a fila em si é sempre atual).
- ⚠️ Os metadados ficam em memória por até 5 minutos (`MetadadosService`): quem liga a aprovação invalida o cache na própria sessão;
  as outras sessões passam a ver a mudança no próximo recarregamento dos metadados.
