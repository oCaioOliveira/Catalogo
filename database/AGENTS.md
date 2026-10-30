# AGENTS.md — database

Complementa o [`AGENTS.md` da raiz](../AGENTS.md). O passo a passo do Docker local (subir, resetar,
conectar) está em [`database/README.md`](README.md). A decisão está em
[`docs/adr/0003`](../docs/adr/0003-schema-por-scripts-sql.md).

## Padrão de nomes da CAIXA: schema `cat` (V013, [ADR 0052](../docs/adr/0052-padrao-de-nomes-caixa-e-sql-server-2014.md))

- **Desde o V013, todos os objetos ficam no schema `cat`, com os nomes do padrão da CAIXA**: tabelas `CATTB001_GRUPO` a
  `CATTB023_VERSAO_SCHEMA` e colunas com prefixo de tipo (`NU_`, `CO_`, `NO_`, `DE_`, `DH_`/`DT_`, `IC_`, `VR_`). O de-para completo
  está no `V013__padrao_caixa.sql` e, no código, em `NomesBanco` (Infrastructure); o teste `NomesBancoTests` exige que sejam iguais.
  Os nomes de constraints e índices (`PK_Grupo`, `UQ_RegistroChaveUnica_Valor`...) não mudaram.
- **Scripts novos (V014 em diante) usam os nomes físicos**: `cat.CATTB011_REGISTRO`, `NU_NSU_REGISTRO`. Tabela ou coluna nova entra
  também no `NomesBanco` (senão o modelo do EF falha ao subir).
- **Regra de nomes para tabela ou coluna NOVA** (o padrão da casa, igual ao das 23 existentes):
  - tabela `cat.CATTB<NNN>_<NOME>`: próximo número livre com 3 dígitos (hoje `CATTB026`; `CATTB025_CHAVE_JWT` é do V018), nome em maiúsculas com `_`, sem acento;
  - chave primária `NU_NSU_<TABELA>` (`uniqueidentifier`); chave estrangeira `NU_NSU_CE_<TABELA_REFERENCIADA>_<SIGLA>` (ex.:
    `NU_NSU_CE_ENTIDADE_IMP`); outras referências a usuário/registro sem FK, `NU_<PAPEL>` (ex.: `NU_USUARIO_DECISAO`);
  - demais colunas pelo tipo: `NU_` número, `CO_` código/chave, `NO_` nome, `DE_` descrição/texto/JSON, `DH_` data e hora, `DT_` data,
    `IC_` indicador (`bit`), `VR_` valor; nome genérico leva a sigla da tabela no fim (`IC_ATIVO_USR`, `DH_CRIADO_IMP`, `NU_VERSAO_REG`);
  - textos `nvarchar ... COLLATE Latin1_General_CI_AI`; constraints e índices com nome lógico (`PK_Tabela`, `FK_Tabela_Outra`...);
  - toda tabela e coluna nova entra no `NomesBanco` (e no de-para do `docs/modelo-de-dados.md`) no mesmo PR.
- **No Docker local, a estrutura do schema cat nasce do `V014`** (vazia, ao lado das tabelas antigas): o `aplicar_scripts.sh` usa só
  `cat.CATTB023_VERSAO_SCHEMA` e, se ela não existir, roda o `V014` primeiro (que registra 1 a 13 como consolidadas). O `V001` a
  `V013` não rodam mais aqui; as tabelas antigas do schema `catalogo` e os dados delas ficam intactos.
- **SQL Server 2014 (nível 120)** no banco da CAIXA (`CATDB001`): nada de `ISJSON`, `OPENJSON`, `STRING_AGG`, `CREATE OR ALTER`,
  `DROP ... IF EXISTS`, `TRIM`. O `sp_rename` não renomeia coluna usada em CHECK: remova e recrie o CHECK (como o V013).
- **Testes de integração no banco da CAIXA**: o login não cria outro banco, então os testes usam o schema `cat_testes` no `CATDB001`,
  montado por `./ferramentas/estrutura_padrao_caixa.ps1 -Modo SchemaTestes` (com a carga da área testes_desempenho). Rode de novo depois
  de cada `V0xx` novo. **No Docker local**, os testes usam o banco `ListagemGenerica_Testes`, schema `cat` (sem `SchemaTestes`), criado
  pelo `V014`, com a área testes_desempenho carregada pelo `V015` (os testes de integração contam com ela: `TesteDeApi.InitializeAsync`
  habilita o grupo do seed para a equipe padrão).
- `./ferramentas/executar_script_sql.ps1` executa um `.sql` como o SSMS (lotes por `GO`, mesma sessão), para quem não tem SSMS.

## Schema `catalogo` (V012, substituído pelo V013)

- **Todos os objetos do Catálogo ficam no schema `catalogo`**, em todos os ambientes: o banco de produção é compartilhado com outros
  sistemas, que têm tabelas de nomes genéricos em `dbo` (`Usuario`, `Equipe`, `Registro`). O `V012` moveu as tabelas de `dbo` para
  `catalogo` (`ALTER SCHEMA ... TRANSFER`, sem cópia de dados). No código, o nome do schema fica só em `SchemaBanco.Nome`
  (Infrastructure), usado pelo `HasDefaultSchema`.
- **Scripts novos (V013 em diante) escrevem `catalogo.` em TODOS os objetos**: `CREATE TABLE catalogo.X`, `REFERENCES catalogo.Y`,
  `OBJECT_ID(N'catalogo.X')`, `COL_LENGTH(N'catalogo.X', ...)`. Nunca `dbo.` e nunca nome sem schema. Os `V001` a `V011` continuam
  com `dbo.` porque são imutáveis (rodam antes do `V012`).
- A aplicação acessa pelo papel `catalogo_aplicacao`: `SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::catalogo`, e só isso.
  Procedure ou view nova no schema já fica coberta pelo papel; nada de `GRANT` a usuário.

## Scripts versionados

> **Listagens novas (grupos, entidades, campos, restrições, dependências) agora são criadas PELA INTERFACE ou PELA API**, pelo administrador
> ([`docs/api/administracao-de-metadados.md`](../docs/api/administracao-de-metadados.md), RN50). Scripts `V00x` são só para
> mudanças de **schema**. A listagem de Testes de Desempenho continua sendo a carga do `V001`. Não altere metadados por script:
> a API valida cada mudança contra os dados (RN58), grava o histórico e invalida o cache; um script não faz nada disso.

- Exceções conscientes à regra acima, pedidas e documentadas: o `V007` converte dois campos do seed para o tipo `URL` (validando os
  valores antes, [ADR 0042](../docs/adr/0042-tipo-de-dado-url.md)) e o `V008` preenche os plurais e a ordem das entidades do seed. As
  duas mudam só a carga do `V001`, para um ambiente novo nascer igual; listagens criadas pela API continuam só pela API.
- O `V009` cria `dbo.GrupoEquipe` (equipes habilitadas por listagem, RN90 a RN93) com uma **carga inicial** que mantém tudo como era
  (`testes_desempenho` para todas as equipes existentes; as outras listagens, para as equipes que já têm itens nelas). A carga só roda
  com a tabela vazia. Depois disso, quem muda as equipes de uma listagem é o administrador, pela interface ou pela API, com histórico.
- O schema é definido **só** pelos scripts `scripts/V<numero>__<descricao>.sql`, aplicados em ordem de número.
- A tabela `catalogo.__VersaoSchema` registra cada script aplicado (até o V012 ela ficava em `dbo`; o `aplicar_scripts.sh` acha
  as duas). Um script com número já registrado **não roda de novo**. A coluna `Observacao` diz quando a versão veio de um pacote
  consolidado (ver "Produção").
- **Script aplicado em produção é imutável.** Nunca edite nem renomeie um `V00x` existente,
  inclusive o `V001`. Precisa corrigir algo? Crie o próximo script.

## Como criar um `V00x`

1. Crie `scripts/V013__descricao_curta.sql` com o **próximo** número, 3 dígitos e **dois** sublinhados.
   O `aplicar_scripts.sh` recusa nome fora do padrão e número repetido.
2. Escreva SQL de SQL Server puro, que rode **igual no SSMS**:
   - nada de `USE`, nome de banco, caminho de arquivo ou senha no script;
   - separe lotes com `GO` (ex.: depois de `CREATE TABLE` e antes de usar a tabela, e em `CREATE VIEW`/`PROCEDURE`);
   - variáveis `$(...)` do sqlcmd não são substituídas;
   - dê nome a toda constraint (`PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_`), seguindo o V001. O EF e o tradutor de erros dependem desses nomes;
   - **todo objeto com `catalogo.`** (seção acima).
3. Prefira scripts que possam rodar de novo sem erro (`IF OBJECT_ID(...) IS NULL`, `IF NOT EXISTS`).
   Se um script falhar no meio, os lotes anteriores ao erro **ficam** no banco.
4. Aplique localmente nos dois bancos (desenvolvimento e testes):
   `cd database/docker && docker compose --env-file ../.env run --rm db-init`.
   O comando precisa terminar com código 0.
5. Atualize o mapeamento do EF (ver [`backend/src/Catalogo.Infrastructure/AGENTS.md`](../backend/src/Catalogo.Infrastructure/AGENTS.md))
   e [`docs/modelo-de-dados.md`](../docs/modelo-de-dados.md). Rode `dotnet test` de dentro de `backend/`.
6. Metadados (uma listagem nova, um campo novo) não são mais alterados por script: use a API. Documente a listagem em `docs/listagens/`.
7. O script altera `Registro.Dados`? Atualize `RegistroValor` no **mesmo** script: as duas tabelas
   andam sempre juntas ([ADR 0013](../docs/adr/0013-registro-valor.md)). O `V002` mostra como carregar
   `RegistroValor` a partir do JSON com `OPENJSON`.
8. Nenhum campo pode usar chave reservada (`id`, `versao`, `criadoEm`, `atualizadoEm`, `equipeId`,
   `criadoPor`, `atualizadoPor`, `situacaoAprovacao`, `ultimaDecisao`, `avaliacao`, `pagina`, `tamanho`, `ordenacao`, `busca`, `dependeDe`, `formato`): a aplicação recusa os metadados ([RN14](../docs/regras-de-negocio.md#rn14--chaves-reservadas)).

9. Entidade nova: defina `EscopoEquipe` (1 = por equipe, o padrão; 0 = global) no mesmo script e
   registre a decisão no documento da listagem (RN27).
10. Nunca exclua linhas de `Usuario`, `Equipe` ou `HistoricoAlteracao` num script: usuários e equipes são
   desativados, e o histórico é permanente (RN31, RN32).
11. Valor novo num CHECK do histórico (`Operacao`, `AlvoTipo`)? Recrie o CHECK num script novo, como o `V004`,
   e atualize os conversores em `Domain/Historico` e o texto do CHECK em `HistoricoAlteracaoConfiguracao`.
12. Registros inseridos por script nascem **APROVADO** (padrão da coluna `SituacaoAprovacao`, V004).
13. Índice **filtrado** (`WHERE ...`), coluna computada ou view indexada exigem `SET QUOTED_IDENTIFIER ON;` no início do
   script (o `sqlcmd` usa OFF). Veja o `V005`.
14. O seed não cria modelos de avaliação: avaliação é configuração de cada equipe, feita pela API (RN40).

## Permissões

- A aplicação usa **só** o login `listagem_app`, membro do papel `catalogo_aplicacao` (`SELECT, INSERT, UPDATE, DELETE, EXECUTE` no
  schema `catalogo`). Ele **não** consegue `CREATE`/`ALTER`/`DROP` nem ler nada fora de `catalogo`: se a aplicação precisar mudar o
  schema, o caminho certo é um script.
- O `sa` só administra o banco local. Nunca o use em connection string da aplicação. O único teste que usa o `sa` é o
  `OutroSistemaNoMesmoBancoTests` (user-secret `ConnectionStrings:CatalogoTestesAdmin`, só no banco `_Testes`), que precisa criar
  `dbo.Usuario` "de outro sistema".
- Senhas ficam em `database/.env` (fora do git), nunca nos arquivos versionados.

## Produção: pacotes de entrega (`entrega/`)

- Em produção não há Docker nem as nossas ferramentas: os DBAs da CAIXA rodam scripts **à mão, no SSMS**, num banco compartilhado.
  Eles **não** recebem os `V00x` para rodar em sequência (os antigos criam objetos em `dbo`). Recebem um **pacote** em
  `entrega/<aaaa-mm-dd>-<descricao>/`, com `README.md` (português formal, para os DBAs; é o nome padrão em todas as entregas) e scripts numerados.
- A instalação inicial é `entrega/2026-10-26-instalacao-inicial/`: `01` schema e papel, `02` estrutura consolidada (o resultado de
  V001 a V012, direto em `catalogo`, registrando as versões 1 a 12 com a observação de instalação consolidada) e `03` administrador
  inicial, gerado por `criar-admin --gerar-sql` (só o hash da senha; `DeveTrocarSenha = 1`; para se a matrícula já existir). Não leva
  área de gestão: as áreas são criadas pela interface ou, no futuro, por uma carga gerada (`ferramentas/gerar_carga_area.sh`).
- O `03` é **gerado, nunca editado à mão**. A senha provisória entra só pela variável `CATALOGO_ADMIN_SENHA`, nunca em arquivo.
  `--aceitar-senha-fora-da-politica` é a única exceção à política de senha, só nesse modo e só para o administrador inicial (a troca
  obrigatória exige uma senha dentro da política).
- **Toda mudança futura de estrutura vira um novo pacote de entrega**, além do `V0xx` de sempre:
  1. o `V0xx` em `scripts/` (desenvolvimento e testes), já com `catalogo.`;
  2. a pasta `entrega/<data>-<descricao>/` com o script incremental (o mesmo conteúdo do `V0xx`, mais as proteções do pacote:
     `SET NOCOUNT/XACT_ABORT ON`, transação, `PRINT` por etapa, `SET NOEXEC ON` se já aplicado, e o `INSERT` em
     `catalogo.__VersaoSchema`) e o `README.md`;
  3. a validação: rodar o pacote num banco que tenha a instalação anterior e comparar com o desenvolvimento
     (`ferramentas/comparar_bancos.sh`, diferença zero). Pacote entregue é imutável, como um `V0xx` aplicado.
- Regras dos scripts de pacote: compatíveis com SQL Server 2016 (nível 130: nada de `STRING_AGG`, `CREATE OR ALTER`, `TRIM`), sem
  `:setvar`/`$(...)`, sem `USE`, sem senha em texto (no máximo um hash gerado pela aplicação); o login da aplicação é um marcador (`<LOGIN_DA_APLICACAO>`).
- A collation de produção precisa ser `Latin1_General_CI_AI` (a de `MSSQL_COLLATION`); o `README.md` do pacote explica o impacto de outra.

## Ferramentas (`ferramentas/`)

- `gerar_carga_area.sh <area> <saida.sql> [banco]`: lê a configuração de UMA área de gestão (só metadados, mesmos GUIDs) e escreve
  um script de carga **no padrão da CAIXA** (schema `cat`, nomes físicos, SQL Server 2014) para o SSMS, que confere a estrutura
  (versão 13 ou maior), para se a área já existir e se autoconfere (quantidades e impressão digital) antes do `COMMIT`. A origem é o
  schema `cat` (`ler_area_cat.sql`) ou, com `ORIGEM=catalogo`, as tabelas antigas do banco local (`ler_area_catalogo.sql`). É o
  caminho para levar áreas para produção (ex.: `V015` e `entrega/2026-10-28-area-testes-desempenho/`). Nunca edite a carga gerada:
  gere de novo. A área nasce sem equipes.
- `impressao_digital_area.sql`: trecho que descreve a configuração de uma área (lida para as tabelas `#Area*`) em linhas canônicas e
  calcula o SHA-256 em partes (o `HASHBYTES` do 2014 aceita até 8000 bytes). É copiado para a carga gerada (autoconferência); mudou
  o trecho, gere a carga de novo.
- `descrever_estrutura.sql` e `comparar_bancos.sh <banco_a> <banco_b> [area]`: comparam a estrutura do schema (pelos catálogos do
  sistema, por nome) e a configuração de uma área entre dois bancos do container local. Saem com 0 só sem nenhuma diferença.
