# 0033. Front-end em Angular 20 com PrimeNG

- Status: Aceito
- Data: 2026-10-22

## Contexto

O Catalogo precisa de uma interface web para o time cadastrar e consultar os itens. O time é júnior: o framework precisa ter uma
estrutura clara, padrões documentados e componentes prontos para tabela com paginação no servidor, formulários, datas, selects com
busca e diálogos. Não há identidade visual definida ainda.

## Decisão

- **Angular 20** (standalone, signals, nova sintaxe de controle, `inject()`, OnPush), criado com `npx @angular/cli@20 new`, com as
  dependências do Angular fixadas em `~20` no `package.json`. TypeScript `strict`, SCSS, ESLint (angular-eslint) e Prettier.
- **PrimeNG 20** (a major compatível com o Angular 20) com o tema **Aura** e os ícones `primeicons`. Cores e ajustes do tema ficam
  num arquivo só (`frontend/src/app/tema/tema.ts`), para aplicar a identidade visual da empresa depois.
- Nenhuma outra biblioteca de UI (sem Bootstrap, Tailwind etc.).
- pt-BR na tela (`LOCALE_ID`, traduções do PrimeNG em `primeng-pt-br.ts`); ISO 8601 e ponto decimal para a API.
- Testes unitários com o executor padrão do `ng new` na v20 (Karma + Jasmine), em Chromium headless (o do Playwright); E2E com
  Playwright.

### Alternativas descartadas

- **React ou Vue**: exigiriam escolher e combinar várias bibliotecas (roteamento, formulários, estado, componentes); o Angular traz
  isso integrado, o que reduz decisões para um time júnior.
- **Angular Material**: menos componentes de dados prontos (tabela lazy com filtros, autocomplete com dropdown, datepicker com hora).

## Consequências

- ✅ Componentes prontos para tudo o que a tela genérica precisa, com acessibilidade documentada pelo PrimeNG.
- ⚠️ **O suporte do Angular 20 termina em 28/11/2026.** A atualização para uma versão suportada (Angular 21 ou superior, com o
  PrimeNG da mesma major) fica **pendente** e deve ser planejada antes dessa data. Na v21 o executor padrão de testes muda para
  Vitest: reavaliar o Karma na mesma atualização.
- ⚠️ PrimeNG e Angular sobem de major juntos: toda atualização de um exige a do outro.
