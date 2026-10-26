# 0043. Navegação entre várias listagens, com a tela inicial calculada pelos metadados

- Status: Aceito
- Data: 2026-10-22

## Contexto

Até aqui o front tinha UMA listagem: o grupo (`grupoPadrao`) e a entidade da tela inicial (`entidadeInicial`, depois a primeira do
menu) vinham de configuração. Com a administração de listagens pela interface, o administrador cria listagens novas a qualquer
momento (ex.: "Inventário"), e o front precisa navegar entre elas sem configuração nem deploy. Também passa a ser comum a estrutura
mudar enquanto alguém usa o sistema, e os metadados ficavam em memória até o fim da sessão.

## Decisão

1. **A listagem atual vem da rota** (`/:grupo/...` e `/painel/:grupo`): o `listagemAtualGuard` a registra no `ListagemAtualService`,
   que o menu observa. O menu mostra as entidades dessa listagem e o "Painel gerencial" dela.
2. **Seletor de listagem** no topo do menu, com os grupos **ativos** de `GET /api/v1/grupos`. Com uma listagem ativa só, ele não
   aparece. Escolher outra listagem abre a tela inicial dela.
3. **Ao entrar** (`/`), abre a **última listagem usada** (guardada no `localStorage`, com `try/catch`), se ainda estiver ativa; senão,
   a primeira ativa, na ordem da API (por nome). Nenhuma ativa: o administrador vai para a administração de listagens.
4. **Tela inicial de cada listagem** (`/:grupo`, `telaInicialDaListagemGuard`): a primeira entidade **por equipe** e **não filha**,
   pela `Ordem` dos metadados (a primeira das "principais" do menu). Listagem só com cadastros globais: o primeiro deles. Listagem
   ainda sem tipos de item: o administrador vai para a estrutura dela; os demais, "não encontrado". Sai a configuração fixa
   (`grupoPadrao`, `entidadeInicial`).
5. **Metadados com validade**: o `MetadadosService` guarda entidades e grupos por **5 minutos** (`VALIDADE_METADADOS_MS`). A
   administração descarta o cache **na hora** depois de cada gravação (`invalidar`), e o menu recarrega (`versao`). O prazo cobre as
   mudanças feitas em outra aba ou por outra pessoa.
6. **Formulário com metadados desatualizados**: quando um POST/PUT de registro volta 400 por campo desconhecido ou inativo
   (`detalhes.camposDesatualizados`, RN01/RN02, um acréscimo na v1) ou por um campo que o formulário não tem, o formulário recarrega os
   metadados, remonta os campos preservando o que já foi digitado e pede para revisar e salvar de novo.

### Alternativas descartadas

- **Uma listagem por instalação** (configuração por ambiente): exigiria deploy para cada listagem nova, o contrário do objetivo.
- **Listagem atual só no `localStorage`**: um link compartilhado abriria a listagem de quem recebe, não a do link. Com a rota como
  fonte da verdade, o link é sempre o mesmo para todos (como o estado da listagem, ADR 0037).
- **Recarregar os metadados a cada tela**: correto, mas uma chamada a mais em toda navegação; 5 minutos de validade mais a
  invalidação imediata cobrem os casos reais.
- **Detectar campos desatualizados pelo texto da mensagem**: frágil (o texto pode mudar, ADR 0023); daí o `camposDesatualizados`.

## Consequências

- ✅ Uma listagem criada na administração aparece no seletor na hora, sem configuração nem deploy.
- ✅ Links continuam estáveis: a rota diz a listagem e a entidade.
- ✅ Mudanças de estrutura chegam ao usuário em até 5 minutos, ou na hora para quem altera e para quem tenta salvar um formulário
  desatualizado.
- ⚠️ Testes E2E que criam listagens precisam desativá-las no fim: uma listagem ativa a mais muda a tela inicial (a primeira por nome).
