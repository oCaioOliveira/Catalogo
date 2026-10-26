# Banco de dados – Listagem Genérica

Esta pasta guarda **tudo o que é do banco**:

```
database/
  scripts/                 Scripts do schema: V001, V002... (desenvolvimento, testes e fonte da verdade)
  entrega/                 Pacotes para os DBAs da CAIXA (rodam à mão no SSMS): instalação inicial e próximas mudanças
  ferramentas/             Gerador da carga de uma área de gestão e comparador de bancos
  docker/                  SQL Server local, só para desenvolvimento
    docker-compose.yml
    init/                  Cria o banco e o login da aplicação
    aplicar_scripts.sh     Aplica os scripts de scripts/ que ainda não foram aplicados
  .env.example             Modelo das configurações locais
  .env                     Suas configurações locais (NÃO vai para o git)
```

> **Importante:** em produção **não existe Docker**. O banco fica num SQL Server já
> existente e **compartilhado com outros sistemas**, e os DBAs aplicam à mão um **pacote de
> entrega** (`database/entrega/`, seção 7). O Docker serve só para você ter na sua máquina
> um SQL Server parecido com o de produção.
>
> **Todos os objetos do Catálogo ficam no schema `catalogo`** (desde o `V012`), nunca em `dbo`:
> os outros sistemas do banco de produção têm tabelas como `dbo.Usuario` e `dbo.Registro`.

---

## 1. Pré-requisitos

- **Docker Desktop** (Windows/Mac) ou Docker Engine + plugin Compose (Linux/WSL).
  Confira com `docker compose version`.
- **Mac com Apple Silicon (M1/M2/M3...)**: a imagem do SQL Server só existe para
  `amd64`. Ative em *Docker Desktop → Settings → General* a opção
  **"Use Rosetta for x86_64/amd64 emulation on Apple Silicon"** e adicione
  `platform: linux/amd64` aos serviços `db` e `db-init` no `docker-compose.yml`.
- Um cliente SQL: **Azure Data Studio**, **SSMS** (Windows) ou **sqlcmd**.

## 2. Criar o arquivo `.env`

```bash
cd database
cp .env.example .env
```

Abra o `database/.env` e troque as senhas:

| Variável            | Para que serve                                                      |
|---------------------|---------------------------------------------------------------------|
| `MSSQL_SA_PASSWORD` | Senha do `sa` (administrador). Só para administrar o banco local.   |
| `APP_DB_PASSWORD`   | Senha do `listagem_app`, o login que **a aplicação** usa.           |
| `MSSQL_PORT`        | Porta no seu computador (padrão `1433`).                            |
| `MSSQL_COLLATION`   | Collation do servidor. Deve ser igual à de produção.                |

**Regras das senhas** (é a política do SQL Server; se não forem seguidas, o container
não sobe ou o `db-init` falha):

- 8 caracteres ou mais;
- com letras **maiúsculas**, **minúsculas**, **números** e **símbolos** (ex.: `#`, `!`, `@`, `%`);
- **não use** `$`, `'`, `"` nem espaços: eles quebram o `.env` ou os scripts SQL.

Exemplo válido: `Dev#Listagem2026`

> A collation só é aplicada quando o volume é criado. Se você mudar `MSSQL_COLLATION`
> depois, precisa fazer o **reset** (seção 3).

## 3. Comandos do dia a dia

Todos os comandos rodam **dentro de `database/docker`** e precisam de `--env-file ../.env`
(é assim que o Compose encontra o `.env`, que fica uma pasta acima):

```bash
cd database/docker
```

| O que fazer                     | Comando                                                       |
|---------------------------------|---------------------------------------------------------------|
| **Subir** o banco               | `docker compose --env-file ../.env up -d`                     |
| Ver se está tudo ok             | `docker compose --env-file ../.env ps -a`                     |
| **Ver logs** do SQL Server      | `docker compose --env-file ../.env logs -f db`                |
| Ver logs da inicialização       | `docker compose --env-file ../.env logs db-init`              |
| **Parar** (mantém os dados)     | `docker compose --env-file ../.env down`                      |
| **Aplicar scripts novos**       | `docker compose --env-file ../.env run --rm db-init`          |
| **Resetar do zero** (apaga tudo)| `docker compose --env-file ../.env down -v` e depois `up -d`  |

Como funciona:

- O serviço `db` é o SQL Server. Os dados ficam no volume `listagem-generica_mssql-data`,
  então **sobrevivem** a `down` / `up`.
- O serviço `db-init` espera o banco ficar saudável, roda uma vez e **encerra**. Ele:
  1. cria os bancos `ListagemGenerica` (desenvolvimento) e `ListagemGenerica_Testes`
     (testes de integração), se não existirem;
  2. cria o login `listagem_app` (se não existir), sincroniza a senha com o `.env` e dá
     acesso a ele nos dois bancos;
  3. aplica nos dois bancos os scripts de `database/scripts/` que ainda não foram aplicados
     (controle em `catalogo.__VersaoSchema`; num banco novo, ele cria o schema e a tabela antes do `V001`).

  A lista de bancos fica na variável `DB_NAMES` do serviço `db-init`, no `docker-compose.yml`.
  O `ListagemGenerica_Testes` é **apagado a cada teste** (só as tabelas de dados): não guarde nada nele.

  Ele pode rodar quantas vezes você quiser, porque não refaz o que já foi feito.
  No `ps -a`, o esperado é `db-init` como **`Exited (0)`**. Qualquer outro código
  significa erro: veja os logs dele.
- **Reset**: o `-v` apaga o volume, ou seja, **todos os dados**. No próximo `up -d`
  tudo é recriado do zero (banco, login e scripts).

### V006: administração de metadados

O `V006__administracao_metadados.sql` acrescenta `Ativo`, `Ordem`, `AtualizadoEm` e `Versao` (rowversion) em `Grupo` e `Entidade`
e o alvo `METADADO` no histórico. Não precisa de reset: tudo fica ativo, e as entidades do seed recebem a ordem do `V001`.

### V005: avaliação de itens

O `V005__avaliacao_itens.sql` cria `ModeloAvaliacao`, `CriterioAvaliacao`, `Avaliacao` e `AvaliacaoResposta`.
Não precisa de reset e não cria nenhum modelo: cada equipe configura a avaliação pela API. O script começa com
`SET QUOTED_IDENTIFIER ON`, exigido pelo índice único filtrado (um modelo ativo por entidade e equipe). Ao aplicar
à mão em outro ambiente, mantenha essa linha.

### V004: aprovação de registros

O `V004__aprovacao_registros.sql` cria a `ConfiguracaoEntidadeEquipe` e acrescenta a situação de aprovação
em `Registro`. Não precisa de reset: os registros existentes ficam **APROVADO** e a aprovação começa
desligada em todas as entidades.

### Reset obrigatório depois do V003

O script `V003__usuarios_equipes_historico.sql` cria usuários, equipes e o histórico, e passa a exigir
uma equipe nos registros das entidades por equipe. Registros criados **antes** dele ficam com `EquipeId`
nulo: só o administrador os enxerga e as regras de equipe não se aplicam a eles. Como no banco local só
há dados de teste, **faça o reset** depois de aplicar o V003:

```bash
cd database/docker
docker compose --env-file ../.env down -v
docker compose --env-file ../.env up -d
```

Depois do reset não existe nenhum usuário: crie o primeiro administrador (ver
[`backend/README.md`](../backend/README.md#primeiro-administrador)).

## 4. Conectar no banco

| Campo            | Valor                                                       |
|------------------|-------------------------------------------------------------|
| Servidor         | `localhost,1433` (ou a porta do seu `.env`)                 |
| Autenticação     | SQL Login                                                   |
| Usuário / senha  | `listagem_app` / `APP_DB_PASSWORD`, ou `sa` / `MSSQL_SA_PASSWORD` para administrar |
| Banco            | `ListagemGenerica`                                          |
| Criptografia     | Encrypt = **True** e **Trust server certificate = True**    |

- **Azure Data Studio**: *New Connection*, preencha os campos acima e marque
  *Trust server certificate*.
- **SSMS**: *Connect → Database Engine*, servidor `localhost,1433`, *SQL Server
  Authentication*. Na aba *Options*, marque *Trust server certificate*.
- **sqlcmd** (no seu computador): `sqlcmd -C -S localhost,1433 -U listagem_app -d ListagemGenerica`
  (o `-C` confia no certificado).
- **sqlcmd sem instalar nada** (usando o do container):
  ```bash
  docker exec -it listagem-generica-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U listagem_app -d ListagemGenerica
  ```

### Por que existem dois logins?

- `sa` é o administrador. Ele pode tudo, inclusive apagar o banco. **Nunca** use na aplicação.
- `listagem_app` é o login **da aplicação**. Ele é membro do papel `catalogo_aplicacao`
  (`V012`): `SELECT`, `INSERT`, `UPDATE`, `DELETE` e `EXECUTE` **só no schema `catalogo`**.
  Ele **não pode alterar o schema** (`CREATE`/`ALTER`/`DROP TABLE` dão erro de permissão) nem
  ler nada fora de `catalogo`. Quem muda o schema são os scripts de `database/scripts/`.

## 5. Connection string da aplicação

A connection string **não fica em arquivo versionado**. Em desenvolvimento ela fica no
**user-secrets** do .NET, um arquivo guardado no seu perfil de usuário, fora do repositório.
Rode **de dentro de `backend/`**, trocando `<senha>` pelo `APP_DB_PASSWORD` do seu `.env`
(e `1433` pela sua porta, se mudou):

```bash
# API (banco de desenvolvimento)
dotnet user-secrets set "ConnectionStrings:Catalogo" \
  "Server=localhost,1433;Database=ListagemGenerica;User Id=listagem_app;Password=<senha>;Encrypt=True;TrustServerCertificate=True" \
  --project src/Catalogo.Api

# Testes de integração (banco de testes)
dotnet user-secrets set "ConnectionStrings:CatalogoTestes" \
  "Server=localhost,1433;Database=ListagemGenerica_Testes;User Id=listagem_app;Password=<senha>;Encrypt=True;TrustServerCertificate=True" \
  --project tests/Catalogo.Api.IntegrationTests
# Só para o teste OutroSistemaNoMesmoBancoTests, que cria dbo.Usuario "de outro sistema" no banco de TESTES (o listagem_app
# não tem DDL nem enxerga dbo). <senha_sa> = MSSQL_SA_PASSWORD do database/.env. Nunca aponte para outro banco.
dotnet user-secrets set "ConnectionStrings:CatalogoTestesAdmin" \
  "Server=localhost,1433;Database=ListagemGenerica_Testes;User Id=sa;Password=<senha_sa>;Encrypt=True;TrustServerCertificate=True" \
  --project tests/Catalogo.Api.IntegrationTests
```

Para conferir (de dentro de `backend/`): `dotnet user-secrets list --project src/Catalogo.Api`.
Mais detalhes em [`backend/README.md`](../backend/README.md).

> ⚠️ **`TrustServerCertificate=True` é só para desenvolvimento.** Ele faz a aplicação aceitar
> qualquer certificado, e é necessário aqui porque o container usa um certificado
> autoassinado. Em produção, use um certificado válido no servidor e
> `TrustServerCertificate=False`. Nunca versione senhas reais em `appsettings`.

## 6. Criar um script novo (mudança de schema)

> **Listagens novas não precisam de script**: grupos, entidades, campos, restrições e dependências são criados pela API, pelo
> administrador ([`docs/api/administracao-de-metadados.md`](../docs/api/administracao-de-metadados.md)). Scripts são só para
> mudanças de **schema**. A listagem de Testes de Desempenho continua sendo a carga do `V001`.

O schema **não** é gerenciado por migrations do EF Core. Toda mudança vira um script SQL
numerado em `database/scripts/`.

1. Crie o arquivo com o **próximo número** e uma descrição curta:
   ```
   database/scripts/V002__adiciona_coluna_x_em_campo.sql
   ```
   - O formato é `V<numero>__<descricao>.sql`, com **dois** sublinhados.
   - Use 3 dígitos (`V002`, `V003`...) para manter a ordem fácil de ler.
   - Não repita número: o script de aplicação recusa versões duplicadas.
2. Escreva o SQL. Separe os lotes com `GO` quando precisar (ex.: depois de `CREATE TABLE`
   antes de usar a tabela, ou ao criar procedures/views).
3. Aplique localmente:
   ```bash
   cd database/docker
   docker compose --env-file ../.env run --rm db-init
   ```
4. Confira o que foi aplicado:
   ```sql
   SELECT * FROM catalogo.__VersaoSchema ORDER BY Versao;
   ```

**Regras de ouro:**

- **Escreva `catalogo.` em todos os objetos** (`CREATE TABLE catalogo.X`, `OBJECT_ID(N'catalogo.X')`).
  Os `V001` a `V011` usam `dbo.` só porque são anteriores ao `V012` e imutáveis.
- **Lembre que o conteúdo vai para produção num pacote de entrega** (seção 7). Não coloque nada que
  só funcione no Docker (nomes de banco, caminhos de arquivo, senhas, `USE` em outro banco).
- **Nunca altere um script que já foi aplicado** (nem o V001). Precisa corrigir? Crie um
  script novo. O controle é por número: um script já aplicado não roda de novo, mesmo
  que o conteúdo mude.
- O script de aplicação **para no primeiro erro**, e o script com erro **não** é registrado
  em `__VersaoSchema`. Mas os lotes (`GO`) que rodaram antes do erro **continuam no banco**.
  Localmente, o caminho mais simples é corrigir o script e fazer o **reset**. Se puder,
  escreva scripts que possam rodar de novo sem erro (ex.: `IF OBJECT_ID(...) IS NULL`).
- Variáveis `$(...)` do sqlcmd **não** são substituídas nos scripts, igual a quando o
  script é executado no SSMS em produção.

## 7. Produção: pacotes de entrega para os DBAs

Os DBAs da CAIXA instalam o Catálogo **à mão, pelo SSMS**, num banco que já existe e é compartilhado com outros sistemas. Eles não
recebem os `V00x` para rodar em sequência (os antigos criam objetos em `dbo`): recebem um pacote em `database/entrega/`.

| Pacote | Conteúdo |
|---|---|
| [`entrega/2026-10-26-instalacao-inicial/`](entrega/2026-10-26-instalacao-inicial/README.md) | `01` schema `catalogo` e papel `catalogo_aplicacao`; `02` estrutura consolidada (o resultado de V001 a V012, versões 1 a 12 registradas); `03` administrador inicial (só o hash da senha, troca obrigatória no primeiro acesso); `README.md` para os DBAs |

- O primeiro administrador vem no `03`, com **só o hash** da senha provisória e troca obrigatória no primeiro acesso. O script é
  gerado pela própria aplicação: `criar-admin --gerar-sql` ([`backend/README.md`](../backend/README.md#primeiro-administrador)).
- **Mudança de estrutura futura = `V0xx` + novo pacote** (`entrega/<data>-<descricao>/`) com o script incremental e o `README.md`
  (o nome padrão do documento das entregas). Regras em [`AGENTS.md`](AGENTS.md#produção-pacotes-de-entrega-entrega).

### Levar uma área de gestão para outro ambiente

```bash
# da raiz do repositório, com o banco local no ar: lê a área no banco de desenvolvimento e escreve a carga
database/ferramentas/gerar_carga_area.sh <chave_da_area> database/entrega/<pacote>/0N_carga_<chave_da_area>.sql

# compara estrutura (e, opcionalmente, a configuração de uma área) entre dois bancos do container; 0 = nenhuma diferença
database/ferramentas/comparar_bancos.sh ListagemGenerica <outro_banco> <chave_da_area>
```

A carga leva só os metadados (área, tipos de item, campos, campo de exibição, unicidades e dependências), com os mesmos GUIDs, e a
área nasce sem equipes habilitadas. Itens, equipes, usuários, histórico, aprovação e avaliação ficam de fora.
