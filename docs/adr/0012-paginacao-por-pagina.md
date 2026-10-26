# 0012. Paginação por página (pagina e tamanho)

- Status: Aceito
- Data: 2026-10-21

## Contexto

O front precisa de listas paginadas com "página X de Y" e total de registros. A alternativa,
paginação por cursor (keyset), é mais eficiente em tabelas enormes, mas não permite pular para uma
página e complica a ordenação por qualquer campo.

## Decisão

- Paginação por **página**: `pagina` (a partir de 1) e `tamanho` (padrão 20, máximo 100).
- A resposta traz `itens`, `pagina`, `tamanho`, `total` e `totalPaginas` (RN17).
- `total` vem de um `COUNT` sobre a mesma consulta filtrada. A página vem de `Skip`/`Take`, com
  desempate pelo `id` para a ordem ser estável.

## Consequências

- ✅ Simples para o front e para o time.
- ⚠️ Páginas muito distantes ficam mais lentas (OFFSET), e registros incluídos durante a navegação
  podem deslocar itens entre páginas. Aceitável para os volumes esperados.
