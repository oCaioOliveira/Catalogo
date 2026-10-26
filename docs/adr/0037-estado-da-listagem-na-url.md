# 0037. Estado da listagem na URL

- Status: Aceito
- Data: 2026-10-22

## Contexto

Na listagem, o usuário filtra, ordena e pagina. Se esse estado ficar só na memória do componente, recarregar a página, voltar do
detalhe ou mandar o link para um colega perde a consulta.

## Decisão

- Filtros, página, tamanho e ordenação ficam nos **query params** da URL, e a URL é a **fonte da verdade**: qualquer mudança na
  tabela vira uma navegação, e a listagem consulta a API sempre que os query params mudam.
- Os query params são **exatamente os parâmetros da API** (RN17 a RN19, `docs/api/README.md`), ex.:
  `?bateria_testes.contem=carga&data_execucao.de=2026-10-01&projeto=<id>&pagina=2&ordenacao=-vazao_media_tps`. Valores padrão
  (página 1, tamanho padrão, `-criadoEm`) ficam fora da URL.
- A conversão tela ↔ URL fica em funções puras testadas (`features/registros/filtros/filtros-url.ts`): datas `aaaa-mm-dd`, ponto
  decimal e referência pelo id (o rótulo é buscado ao abrir a página).
- Paginação, ordenação e filtros são feitos **pela API** (`p-table` em modo lazy), nunca no navegador.
- Preferências do usuário que não são consulta (colunas visíveis) ficam no `localStorage`, por entidade, com `try/catch`.

## Consequências

- ✅ F5, voltar e compartilhar o link mantêm a consulta.
- ✅ A mesma URL monta a exportação (CSV/XLSX) com os filtros atuais.
- ⚠️ Os nomes dos parâmetros são as chaves dos campos: renomear um campo quebra links antigos (por isso as chaves são imutáveis,
  ADR 0027).
