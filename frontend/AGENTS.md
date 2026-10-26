# AGENTS.md — frontend

Complementa o [`AGENTS.md` da raiz](../AGENTS.md). Aplicação web em **Angular 20 + PrimeNG 20** ([ADR 0033](../docs/adr/0033-angular-20-com-primeng.md)).
Como a tela é montada: [`docs/frontend.md`](../docs/frontend.md). Contrato da API: [`backend/openapi/v1.json`](../backend/openapi/v1.json)
e [`docs/api/README.md`](../docs/api/README.md). **Comandos e caminhos daqui são relativos a `frontend/`.**

**Telas novas seguem o [guia de interface](../docs/design/guia-de-interface.md)** (tokens, componentes padrão, cores, logo), e na
interface vale sempre a coluna "Na tela" do [glossário](../docs/glossario.md): "Área de gestão" (no código, grupo), "Tipo de item"
(entidade), "Item" (registro). O nome do sistema na tela é "Catálogo", com acento (títulos, cabeçalho, login); no código,
`Catalogo`.

## A regra principal: telas dirigidas por metadados

**Nenhuma tela é escrita à mão para um tipo de item.** Listagem, filtros, formulário e detalhe são genéricos e se montam a partir
de `GET /api/v1/grupos/{grupo}/entidades` ([ADR 0034](../docs/adr/0034-front-dirigido-por-metadados.md)). Nunca escreva
`if (entidade === 'relatorio_teste')`: se a tela precisa de uma informação que os metadados não trazem, ela entra no back-end
(mudança aditiva na v1) e o front a lê do `MetadadosService`.

O front **consome** a API como ela é: não reimplemente regra de negócio. Esconder botões (`PermissoesService`) e validar campos é só
conveniência; a API decide e valida de novo.

## Pastas

| Pasta | O que tem |
|---|---|
| `src/app/api/` | **GERADO** a partir do `openapi/v1.json` (`npm run gerar-api`). **Nunca edite à mão** ([ADR 0036](../docs/adr/0036-cliente-gerado-do-openapi.md)) |
| `src/app/core/` | importação de planilhas (`importacao/`: serviço e tipos), autenticação (`auth/`: serviço, interceptor, guards), erros (`erros/`: interceptor e o mapa `codigo → mensagem`), metadados (`metadados/`: serviço, tipos e regras puras), permissões (e os guards `painelGuard` e `administracaoGuard`), `RegistrosService` (acesso aos registros), `PainelService` (`painel/`), `AdministracaoService` (`administracao/`), aprovação (`aprovacao/`: serviço, contador de pendentes e regras) e avaliação (`avaliacao/`: serviço, modelo e regras) e o layout |
| `src/app/shared/` | o que é usado por várias telas: um componente de campo **por tipo** (`campos/`), o formulário genérico (`formulario/`), a exibição de um valor em leitura, igual na tabela e no detalhe (`exibicao/`), a trilha sobre o `p-breadcrumb` (`navegacao/`), a densidade das tabelas (`tabelas/`), o histórico genérico (`historico/`), formatação pt-BR e datas, acessibilidade |
| `src/app/features/` | as telas, carregadas sob demanda: `auth/` (login, troca de senha), `registros/` (listagem, filtros, detalhe, páginas do formulário), `painel/` (painel gerencial e análise personalizada), `administracao/` (áreas de gestão, usuários e equipes), `configuracoes-equipe/` (aprovação e modelos de avaliação por equipe), `aprovacao/` (a fila), `conta/` (Minha conta), `nao-encontrado/`, `sem-permissao/`, `inicio/` (página Início da área), `sem-listagens/` (nenhuma área de gestão para as equipes do usuário), `importacao/` (assistente de importação de planilhas e importações anteriores) |
| `src/app/tema/` | `tema.ts` (paleta da CAIXA, neutros, `PALETA_GRAFICOS`: **único lugar de valores de cor**), `fundamentos.scss` (tokens `--cor-*`, `--espaco-*`...), `marca.ts` (**caminho da logo**, num lugar só), `logo-marca.component.ts`, `botao-tema.component.ts`, `tema.service.ts` e as traduções do PrimeNG |
| `src/assets/marca/` | a logo da CAIXA em SVG: `caixa-logo.svg` (positiva, tema claro) e `caixa-logo-negativa.svg` (negativa, tema escuro). Ver [guia → Logo](../docs/design/guia-de-interface.md#logo) |
| `src/environments/environment.ts` | configurações num lugar só (`grupoPadrao`, tamanhos de página). A tela inicial é a primeira entidade do menu, pela `ordem` dos metadados (`telaInicialGuard`) |
| `scripts/` | `gerar-api.mjs` |
| `tools/dados-dev/` | script que popula dados de exemplo **pela API** |
| `e2e/` | testes Playwright |

## Padrão dos componentes

- **Standalone**, `ChangeDetectionStrategy.OnPush` (o lint exige), `inject()` em vez de construtor, **signals** para estado
  (`signal`, `computed`, `input()`, `output()`), nova sintaxe de controle (`@if`, `@for`, `@switch`; o lint exige).
- Formulários: **Reactive Forms tipados** (`FormControl<T>`, `FormRecord` para os campos dinâmicos). Nada de APIs experimentais.
- Componentes **não usam `HttpClient`**: chamam serviços de `core/` (ou de `features/`), que usam o cliente gerado.
- Registros são dinâmicos: `Record<string, unknown>` (`core/registros/modelo-registro.ts`). Modelos gerados têm tudo opcional:
  converta para os tipos de `core/` (ex.: `entidadeDeDto`).
- Regra que dá para testar sem Angular vira **função pura** num `.ts` com `.spec.ts` ao lado (ex.: `regras-metadados.ts`,
  `filtros-url.ts`, `montador-formulario.ts`, `permissoes.ts`).
- Erros da API: decida pelo `codigo` (`core/erros/problema.ts` → `lerProblema`), nunca pelo texto. Mensagens só em
  `core/erros/mensagens-erro.ts`, alinhadas com [`docs/api/codigos-de-erro.md`](../docs/api/codigos-de-erro.md). Para tratar um
  status no próprio componente (ex.: 400 no formulário), passe `tratarNoComponente(400)` como contexto da chamada; os demais viram
  toast no interceptor.
- Acessibilidade: **todo controle de formulário tem nome acessível, verificado em teste**. `<input>`: `<label for>`; `p-select` e
  `p-multiselect` (o combobox deles não é um `<input>`): `id` no rótulo e `ariaLabelledBy` apontando para ele. Mensagens de erro
  ligadas por `aria-describedby` (componente sem a propriedade: diretiva `appDescritoPor`); foco visível; nada só por cor; botões só
  com ícone têm `ariaLabel` **e** dica (`pTooltip`). Nos E2E, ache os controles por `getByRole` com o nome (não por `getByLabel`
  com expressão regular, que compara o texto bruto do rótulo).
- Nomes do domínio em português; termos do framework em inglês (`Component`, `Guard`, `Interceptor`).
- Datas: na tela `dd/MM/yyyy` (e `HH:mm`), para a API ISO 8601 (`shared/formatacao/datas.ts`). Números: vírgula na tela, ponto na API.
- Telas de lista e de item usam a trilha `app-trilha` (nunca `<a>` soltos com "/"), as tabelas recebem `[size]="densidade.tamanhoTabela()"`
  (`DensidadeService`), e nomes de entidade em títulos de lista vêm de `nomePlural(...)`; "Novo/Editar" usam o `nome` (singular).

## Painel gerencial

Como funciona: [`docs/frontend.md` → "Painel gerencial"](../docs/frontend.md#painel-gerencial). Em resumo:

- **Widgets são configuração, não componentes novos** ([ADR 0039](../docs/adr/0039-widgets-por-configuracao-em-arquivo.md)). Um widget
  novo é um objeto em `features/painel/configuracao/<grupo>.painel.ts`, de preferência copiado da análise personalizada ("Copiar
  configuração"). Não crie componente por gráfico, e nada de `if (entidade === ...)` no painel: o que é de uma área de gestão fica no arquivo.
- **Gráficos só com `p-chart`, cores do tema** ([ADR 0040](../docs/adr/0040-graficos-com-primeng-chartjs.md)). Cor nova ou ordem nova
  da paleta só em `src/app/tema/tema.ts` (`PALETA_GRAFICOS`), e conferida com um validador de paletas (daltonismo, claro e escuro).
- A transformação dos dados (`graficos/transformar-dados.ts`) e as opções do Chart.js (`graficos/opcoes-grafico.ts`) são **funções
  puras** com teste. Regra visual nova entra lá, não no template.
- Todo gráfico tem "Ver dados" (tabela) e título/descrição ligados ao canvas. Cada widget carrega e falha sozinho.
- O estado (período, equipe, análise) mora na URL (`modelo/estado-url.ts`, testado nos dois sentidos).
- Quem vê: `PermissoesService.podeVerPainel` (RN78) e o `painelGuard`.

## Administração de áreas de gestão (metadados)

Como funciona: [`docs/frontend.md` → "Administração de listagens"](../docs/frontend.md#administração-de-listagens). **Áreas de gestão
novas são criadas pela interface** (Configurações → Áreas de gestão), não por script nem por código: nada de tela, rota ou `if` por área.

- **Toda gravação de estrutura passa pelo `AlteracaoEstruturaService`** (`features/administracao/listagens/comum/`): ele simula
  (`?simular=true`) e decide — grava, confirma, pede o valor padrão ou mostra o relatório de bloqueio ([ADR 0044](../docs/adr/0044-simulacao-obrigatoria-antes-de-gravar-estrutura.md)).
  Tela nova da administração: monte um `Alteracao` (`grupo`, `entidade`, `titulo`, `chamar(simular, valorPadrao)`) e trate o
  `Desfecho` (`gravado` → estrutura nova; `conflito` → recarregar; `invalido` → erros nos controles). Nunca chame a API direto.
- O acesso à API é o `MetadadosAdminService` (`core/administracao/`), que descarta os metadados em memória depois de cada gravação.
  Corpo de PUT de entidade e de campo **sempre completo** (`corpoDaEntidade`, `corpoDoCampo`): na API, ausente = apagado.
- Regras da tela são funções puras com teste em `listagens/modelo/` (chave, tipos e conversões, dependências, simulação, usos).
- Formulário preenchido num `effect`: o preenchimento vai em `untracked` (o estado dos controles também é signal; sem isso, mexer num
  campo refaz o reset). Formulário sem `[formGroup]`: use `(submit)="$event.preventDefault(); salvar()"`, não `(ngSubmit)`.
- Arrastar e soltar com `@angular/cdk/drag-drop`, sempre com os botões de mover como alternativa pelo teclado.

## Aprovação e avaliação

Como funciona: [`docs/frontend.md` → "Aprovação e avaliação"](../docs/frontend.md#aprovação-e-avaliação).

- **Os elementos de aprovação e avaliação só aparecem quando o recurso está ativo para o tipo de item e a equipe**
  ([ADR 0045](../docs/adr/0045-telas-condicionais-por-recurso-ativo.md)). A informação vem dos metadados (`equipesComAprovacao`,
  `criteriosAvaliacao`; o formulário de avaliação, de `?equipeId=`): nada de chamada por equipe, nada de `if` por tipo de item.
  Onde nada está ligado, a tela tem de ficar igual a antes (o cenário 12 do E2E confere nas equipes de desenvolvimento).
- Regras de tela em funções puras com teste: `core/permissoes/permissoes.ts` (quem cria, edita, decide, avalia),
  `core/aprovacao/regras-aprovacao.ts` (situação, faixa, justificativa, "aguardando há"), `core/avaliacao/regras-avaliacao.ts`
  (resumo, respostas, validação com as chaves da API) e `features/configuracoes-equipe/modelo/regras-modelo.ts` (editor de modelo).
- O contador de pendentes é por intervalo (2 minutos) e a cada navegação (`PendentesService`), sem tempo real.
- E2E: crie a própria equipe e usuários (prefixo único), habilite a equipe na área de gestão que o teste usa (`habilitarNaListagem`, RN90),
  ligue os recursos **só nela** e deixe a limpeza com o helper compartilhado (abaixo). Nunca configure as equipes de desenvolvimento.
  Alternador (`p-toggleswitch`): clique no `getByRole('switch', { name })`, não no `<label>`.

## Navegação e padrões de tela

Como funciona: [guia de interface](../docs/design/guia-de-interface.md). Regras que valem para toda tela:

- **Moldura** (`core/layout/`): no cabeçalho, a logo (leva ao Início da área atual), o seletor de área de gestão, Aprovações,
  Configurações, o botão de tema e o menu do usuário. Ao lado, o **menu da área** (Início, Painel, tipos de item, Cadastros de apoio)
  ou, em `/configuracoes/...`, a **área de configurações**. Rota antiga vira redirecionamento (`rotas-antigas.ts`), sem perder `?parâmetros`.
- **Área atual: só o `AreaAtualService`** (`core/metadados/area-atual.service.ts`). Nas rotas da área ela vem da URL; fora delas,
  é a última usada. Nenhuma tela guarda a área por conta própria.
- **Tabelas**: a linha é clicável, a primeira coluna (o campo de exibição) fica fixa e é link, as ações ficam no menu "⋯"
  (`app-menu-acoes-linha`), há sombra de rolagem (`appSombraRolagem`) e a densidade vem do `DensidadeService`. Nos E2E, use
  `acaoDaLinha(...)`.
- **"Criar novo" em diálogo de um nível**: "+ Criar <Tipo>" no select de referência (`shared/formulario/criar-relacionado.ts`). O
  diálogo não abre outro diálogo; o resto é página.
- **Salvar sempre visível**: em listas ou formulários longos, a ação de salvar fica sempre visível.
  - lista editável: cabeçalho fixo `.barra-lista-fixa` com busca, ações rápidas, Descartar e Salvar, habilitados só com alterações;
  - formulário: `.acoes-formulario.acoes-fixas` no rodapé;
  - alterações pendentes: `alteracoesPendentesGuard` e `confirmarSaidaSemSalvar` (`shared/formulario/`).
- **Cards em grade**: altura igual na linha e rodapé com `margin-top: auto`, nunca altura fixa (guia → "Cards em grade").
- **Cabeçalho de página padrão** (`app-cabecalho-pagina`, uma ação primária por tela) e **estados padronizados** (`app-estado-vazio`,
  `app-estado-erro`, `p-skeleton` no carregamento).
- **Cores**:
  - valores só em `tema/tema.ts`, e nos componentes só os tokens `--cor-*`;
  - o laranja CAIXA é destaque pontual (no máximo 4 usos, hoje filete, contador e indicador principal), nunca texto sobre fundo claro
    nem botão;
  - alerta é âmbar, não laranja.
  O contraste dos dois temas é conferido no E2E `marca.spec.ts` (axe).
- Nada de grupo ou entidade fixos: a área atual vem da rota (`listagemAtualGuard`), a tela inicial é calculada
  (`telaInicialGuard`, `telaInicialDaListagemGuard`, [ADR 0043](../docs/adr/0043-navegacao-entre-listagens-e-tela-inicial-pelos-metadados.md)).
- Em guard assíncrono, todo `inject()` vai **antes** do primeiro `await` (depois dele o guard sai do contexto de injeção: tela em branco).
- E2E que cria área de gestão: prefixo único, chave com `_e2e_` (ex.: `inventario_e2e_...`), habilitar as equipes que o teste usa (a
  área nasce sem equipes, RN93) e desativar no fim (a preparação global e o helper de limpeza desativam sobras).

## Áreas de gestão habilitadas por equipe (RN90 a RN93)

Como funciona: [`docs/frontend.md` → "Equipe"](../docs/frontend.md#equipe) e [ADR 0046](../docs/adr/0046-listagens-habilitadas-por-equipe.md).

- O que é de equipe **dentro de uma área de gestão** usa `Listagem.minhasEquipes` (as equipes do usuário nesta área, de `GET /grupos`,
  via `MetadadosService.listagem(grupo)` / `equipesDaListagem(grupo)`), **nunca** `usuario.equipes` direto: select do formulário,
  coluna e filtro de Equipe (`mostraEquipe`), painel, configurações da equipe e fila. Nada de chamada por equipe.
- `GET /grupos` já devolve só as áreas visíveis: não filtre de novo. Área fora dela = "não encontrado" (`listagemAtualGuard`);
  nenhuma = `/sem-listagens`.
- A aba Equipes da administração grava pelo `AlteracaoEstruturaService`, como toda estrutura (o impacto `ITENS_OCULTOS` vai para o
  diálogo de confirmação).

## Itens relacionados no detalhe (RN94)

Como funciona: [`docs/frontend.md` → "Itens relacionados"](../docs/frontend.md#itens-relacionados-e-entidades-filhas) e
[ADR 0047](../docs/adr/0047-itens-relacionados-descobertos-pelos-metadados.md).

- As seções vêm de **uma** chamada (`RegistrosService.relacionados`) e das regras puras de `features/registros/detalhe/relacionados.ts`
  (seções, títulos, colunas, links "Ver todos" e "Novo …", estado aberto/fechado). Nada de `if` por tipo de item nem lista fixa de
  relações: uma referência nova nos metadados já gera a seção.
- Os itens de cada seção vêm da **listagem filtrada** (`?<campo>=<id>`), nunca de um endpoint próprio. A seção só busca a tabela
  quando está aberta.
- Pré-preenchimento na criação: `?preencher=<campo>&com=<id>&equipe=&voltar=` na rota `novo` (`preenchimentoDaUrl`, que só aceita um
  campo REFERENCIA e um `voltar` interno). O formulário recebe `valorInicial` (editável) — diferente do `campoFixo` (travado) das filhas.

## Importação de planilhas (RN97 a RN104)

Como funciona: [`docs/frontend.md` → "Importação de planilhas"](../docs/frontend.md#importação-de-planilhas) e
[ADR 0048](../docs/adr/0048-importacao-pelo-mesmo-caminho-do-cadastro.md).

- **A importação não tem regra de negócio no front**: o assistente envia o arquivo, o mapeamento e as opções, e mostra o que a API
  devolveu. No back-end, cada linha passa pelo **mesmo** `GravadorRegistro` do cadastro manual, sem atalhos: nada de validar ou gravar
  itens "em lote" pelo front. As regras da tela (mapeamento, textos, situações) ficam em `features/importacao/modelo/regras-importacao.ts`,
  com teste.
- Genérica para qualquer tipo de item: nada de colunas, rótulos ou `if` por tipo de item. Os destinos de cada coluna vêm da API
  (`destinos` do envio), na ordem do formulário.
- Downloads (modelo e relatório) pelo `ImportacaoService` + `baixarArquivo`, como a exportação.
- E2E (`e2e/importacao.spec.ts`): as planilhas do modelo e do relatório são lidas e preenchidas pelo XML (`e2e/apoio/planilhas.ts`,
  JSZip), porque o ClosedXML grava com prefixo de namespace (`<x:row>`), que o exceljs não lê; planilhas novas são geradas com o exceljs.
  Arquivo pelo `getByLabel('Escolher arquivo').setInputFiles(...)`.
- Serviços carregados na partida (`AuthService`, `MetadadosService`, `AprovacaoService`) importam as operações **direto do arquivo**
  (`api/fn/...`), não do barril `api/functions`: o barril leva o cliente inteiro para o bundle inicial (as funções geradas têm o efeito
  colateral `.PATH`, que impede o tree-shaking). Serviço novo carregado na partida: faça igual.

## Limpeza dos E2E

**Toda suíte E2E que cria equipes ou usuários usa o helper de limpeza no início e no fim**: `limparE2E(tokenDe('admin'))`
(`e2e/apoio/limpeza.ts`) no `beforeAll` e no `afterAll`. Ele desativa, nesta ordem, o que tem nome de teste (equipes "E2E-…" ou
"Equipe E2E-…", usuários `e2e-…`, áreas de gestão com `_e2e_` na chave): pendentes aprovados, aprovação e modelo de avaliação desligados,
usuários desativados, equipe desativada e tirada das áreas. Nunca toca nas equipes e usuários de desenvolvimento.

- Nomes: use o `prefixoUnico()` (equipes `${prefixo} Squad`, `Equipe ${prefixo}`; matrículas `${prefixo.toLowerCase()}-…`).
- A preparação global também chama o helper, e a **verificação final** (`e2e/apoio/verificacao-final.ts`) falha a execução se sobrar
  equipe ou usuário de teste ativo.
- Usuários de teste com troca de senha e página logada: `e2e/apoio/usuarios-teste.ts` (`criarUsuarioDeTeste`, `abrirComo`).

## Administração e Minha conta

Como funciona: [`docs/frontend.md` → "Administração de usuários e equipes"](../docs/frontend.md#administração-de-usuários-e-equipes).

- **A senha provisória nunca sai do diálogo** (`SenhaProvisoriaDialogComponent`, [ADR 0041](../docs/adr/0041-senha-provisoria-exibida-uma-vez-sem-armazenamento.md)):
  da resposta da API direto para ele; nada de guardar em serviço, signal de página, storage, URL, log ou toast. Fechar = descartar.
- Quem administra: `PermissoesService.podeAdministrar` (só ADMINISTRADOR, RN26) e o `administracaoGuard`. "Minha conta" é para todos.
- Regras puras com teste em `features/administracao/modelo/` (equipes por perfil, situação, filtros na URL, aviso ao editar o próprio
  perfil, membros sem outra equipe ativa). Os erros da API são mostrados pelo `codigo` (`mensagens-erro.ts`), também nos formulários.
- Histórico de usuários, equipes e registros: o mesmo `shared/historico/historico.component.ts`; quem usa diz como carregar e formatar.

## Como adicionar um tipo de dado novo

1. Back-end primeiro (ver `backend/src/Catalogo.Domain/AGENTS.md`): o tipo aparece em `tipoDado` nos metadados.
2. `core/metadados/modelo.ts`: inclua o tipo em `TipoDado`.
3. `shared/campos/`: crie `campo-<tipo>.component.ts` (inputs `campo`, `controle`, `inputId`, `descricaoId`, `invalido`) e um `@case`
   em `campo-formulario.component.ts`.
4. `shared/formulario/valores.ts`: conversão API ↔ formulário; `validadores.ts`: validação do tipo (igual à RN04).
5. `shared/formatacao/formatar-valor.ts` (o texto) e, se o tipo tiver apresentação própria (link, ícone), `shared/exibicao/valor-campo.component.ts`.
   Tipos hoje: TEXTO, TEXTO_LONGO, INTEIRO, DECIMAL, BOOLEANO, DATA, DATA_HORA, LISTA, REFERENCIA e URL.
6. `features/registros/filtros/filtros-url.ts` e `filtros-registros.component.html`: o filtro do tipo.
7. Testes em `montador-formulario.spec.ts` e `filtros-url.spec.ts`; tabela de `docs/frontend.md` atualizada.

## Cliente da API (gerado)

- Mudou o `backend/openapi/v1.json`? Rode `npm run gerar-api`, compile (`npm run build`) e corrija o que o compilador apontar.
- `src/app/api/` é apagado e recriado pelo gerador: qualquer edição manual se perde. O lint e o Prettier ignoram a pasta.

## Comandos

```bash
npm install                 # dependências
npx playwright install chromium   # navegador dos testes (unitários e E2E), uma vez
npm start                   # http://localhost:4200, com proxy de /api para a API local (proxy.conf.json)
npm run gerar-api           # regenera src/app/api a partir de ../backend/openapi/v1.json
npm run lint                # ESLint (angular-eslint)
npm run format              # Prettier
npm test                    # testes unitários (Karma + Jasmine, Chromium headless)
npm run dados-dev           # dados de exemplo pela API (variáveis de ambiente: ver README)
npm run e2e                 # Playwright sobre um BUILD novo servido na 4300 (API, banco e dados-dev no ar)
npm run e2e:ng-serve        # Playwright contra o `npm start` (4200), no desenvolvimento
```

**E2E sempre sobre um build** (`playwright.config.ts` → `scripts/servir-build.mjs`): o watcher do `ng serve` no WSL perde
alterações e serve código antigo. Ao depurar com o `npm start`, se a tela não refletir o código, suba de novo ou use `--poll 2000`.
`npx playwright test e2e/<arquivo>.spec.ts` também faz o build antes. Para repetir um arquivo sem mudar o front, use
`E2E_SEM_BUILD=1`, que reaproveita o `dist/`. As senhas vêm de `CATALOGO_ADMIN_SENHA` e `CATALOGO_SENHA_DEV`.

## Definição de pronto

- [ ] `npm run lint` sem erros e `npm run build` sem warnings.
- [ ] `npm test` todo verde, com teste para regra nova (funções puras primeiro).
- [ ] `npm run e2e` todo verde, com a API e o banco locais e os dados do `npm run dados-dev`.
- [ ] Nenhuma tela ou `if` específico de um tipo de item.
- [ ] Todo controle novo tem nome acessível (rótulo associado; `ariaLabelledBy` em `p-select`/`p-multiselect`).
- [ ] Mudou como a tela se monta a partir dos metadados? `docs/frontend.md` atualizado. Decisão de arquitetura? ADR em `docs/adr/`.
- [ ] Precisou de algo novo da API? Mudança no back-end (com testes e `openapi/v1.json` revisado) e `npm run gerar-api`.
