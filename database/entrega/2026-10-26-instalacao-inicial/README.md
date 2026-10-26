# Catálogo — Pacote de instalação inicial do banco de dados

**Pacote:** `2026-10-26-instalacao-inicial` · **Versão da estrutura:** 12 · **Destinatários:** equipe de administração de banco de dados (DBAs)

## 1. Sobre o sistema

O Catálogo é um sistema web de cadastro genérico: os tipos de item e os seus campos são definidos por metadados guardados no
banco, e um motor único valida, grava, lista e exclui os itens. O primeiro uso é a área de gestão **Testes de Desempenho**.
A aplicação (API .NET 8 e interface web) acessa um banco SQL Server existente, compartilhado com outros sistemas.

## 2. Conteúdo do pacote

| Arquivo | Finalidade |
|---|---|
| `01_criar_schema_e_permissoes.sql` | Cria o schema `catalogo` e o papel `catalogo_aplicacao`, com as permissões |
| `02_estrutura.sql` | Cria a estrutura completa (23 tabelas, com chaves, índices, CHECKs e valores padrão) no schema `catalogo` |
| `03_administrador_inicial.sql` | Cadastra o primeiro administrador do Catálogo, com a senha provisória já criptografada |
| `README.md` | Este documento |

Todos os scripts foram escritos para **execução manual no SQL Server Management Studio (SSMS)**:

- lotes separados por `GO`, **sem variáveis de SQLCMD** (`:setvar`, `$(...)`) e sem `USE`: conecte-se ao banco do Catálogo antes de abrir cada script;
- `SET NOCOUNT ON`, `SET XACT_ABORT ON`, transação única e mensagens `PRINT` em cada etapa (aba *Messages*);
- **proteção contra execução duplicada**: se encontrarem o trabalho já feito, param com uma mensagem clara e não alteram nada
  (os lotes seguintes não são executados, por meio de `SET NOEXEC ON`);
- nenhuma senha em texto, nenhum login e nenhum nome de banco ou servidor está nos scripts.

## 3. Perguntas a responder antes da execução

Solicitamos que as respostas sejam enviadas à equipe do Catálogo **antes** da janela de execução:

1. **Versão do SQL Server** do servidor de destino (`SELECT @@VERSION;`). É exigido o SQL Server 2016 ou superior.
2. **Collation do banco** (`SELECT DATABASEPROPERTYEX(DB_NAME(), 'Collation');`). Ver a seção 4.3.
3. **Nível de compatibilidade do banco** (`SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME();`). É exigido 130 ou superior.
4. **Padrão de logins** da casa para aplicações: login SQL ou conta de domínio, e o nome a ser usado (ver a seção 7).
5. **O schema `catalogo` já existe** nesse banco? (`SELECT SCHEMA_ID(N'catalogo');` — `NULL` significa que não existe.) Se existir e
   pertencer a outro sistema, a instalação não deve prosseguir (o script 01 se recusa a usar um schema `catalogo` com objetos).
6. **Janela de execução** disponível. A execução completa leva menos de um minuto, sem indisponibilidade para os demais sistemas.
7. **A política da casa permite guardar arquivos binários no banco?** A importação de planilhas guarda o arquivo enviado na tabela
   `catalogo.ImportacaoArquivo` (coluna `varbinary(max)`), com **no máximo 5 MB por arquivo**, e a própria aplicação **apaga o
   arquivo automaticamente depois de 7 dias** (o registro da importação permanece, sem o arquivo). Volume estimado na seção 4.4.
   Se a política não permitir, informe-nos antes da instalação.

## 4. Pré-requisitos

### 4.1 Versão do SQL Server

**SQL Server 2016 ou superior** (versão 13.x ou posterior), em qualquer edição. A estrutura usa recursos disponíveis a partir
dessa versão: `ISJSON` (CHECKs das colunas JSON), `OPENJSON` e `FOR JSON` (usados pela aplicação), `DROP ... IF EXISTS`,
`rowversion` (controle de concorrência otimista) e índice filtrado. A aplicação foi testada no SQL Server 2022.

### 4.2 Nível de compatibilidade

**130 ou superior** (`ISJSON` e `OPENJSON` exigem o nível 130). Para conferir: `SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME();`.

### 4.3 Collation e o seu impacto

A aplicação foi desenvolvida e testada com a collation **`Latin1_General_CI_AI`** (sem diferenciar maiúsculas/minúsculas nem
acentos). As tabelas do Catálogo **herdam a collation do banco**: os scripts não definem collation nas colunas, com uma única
exceção deliberada (o CHECK do formato da chave dos critérios de avaliação usa `Latin1_General_BIN`).

O que depende da collation:

- **Buscas e filtros de texto** (igualdade e "contém"): com `CI_AI`, buscar "agil" encontra "Ágil"; com uma collation `CS`
  (sensível a maiúsculas) ou `AS` (sensível a acentos), a busca passa a diferenciar.
- **Unicidade garantida pelo banco** em colunas de texto: chave da área de gestão, nome da equipe (`UQ_Equipe_Nome`), matrícula do
  usuário (`UQ_Usuario_Matricula`), chave do tipo de item e do campo. Com `CI_AI`, "Equipe Alfa" e "equipe alfa" são o mesmo nome e
  o banco recusa a duplicata; com uma collation sensível, os dois seriam aceitos.
- **Ordenação** das listagens por texto (posição de letras acentuadas e de maiúsculas).

O que **não** depende da collation: a unicidade dos **itens cadastrados** (ex.: "não pode haver duas comunidades com o mesmo
nome") é calculada pela própria aplicação, com o texto normalizado sem acentos e em maiúsculas, e gravada como hash; ela se
comporta da mesma forma em qualquer collation. Isso, porém, pode gerar diferenças entre o que a busca encontra e o que a
unicidade bloqueia, caso a collation do banco seja sensível a maiúsculas ou acentos.

**Recomendação:** banco com `Latin1_General_CI_AI`. Uma collation `Latin1_General_CI_AS` ou `SQL_Latin1_General_CP1_CI_AS` também
funciona, com a diferença de que as buscas e as unicidades de texto do banco passam a diferenciar acentos. Collations sensíveis a
maiúsculas (`_CS_`) ou binárias **não são recomendadas**. Em qualquer caso diferente de `Latin1_General_CI_AI`, pedimos que nos
avisem antes da instalação.

### 4.4 Espaço estimado

| Item | Estimativa |
|---|---|
| Estrutura vazia e administrador inicial (scripts 02 e 03) | menos de 2 MB |
| Crescimento por item cadastrado (item, valores, referências e histórico) | cerca de 2 KB por item |
| Uso esperado no primeiro ano (ordem de grandeza: dezenas de milhares de itens) | 50 MB a 300 MB |
| Arquivos de importação (`catalogo.ImportacaoArquivo`, retidos por 7 dias) | até 5 MB por arquivo; em uso típico, abaixo de 50 MB a qualquer momento |

Referência: o banco de desenvolvimento, com cerca de 7.700 itens e 24 mil linhas de histórico, ocupa 16 MB no schema `catalogo`;
os arquivos de importação do desenvolvimento têm em média 24 KB (o maior, 48 KB). O pior caso de arquivos seria
`5 MB × quantidade de importações em 7 dias` (por exemplo, 100 importações de 5 MB na mesma semana = 500 MB, liberados após 7 dias).

## 5. Ordem de execução

Conecte-se ao **banco de destino** com um usuário com permissão de `db_owner` (ou com `CREATE SCHEMA`, `CREATE ROLE`,
`CREATE TABLE` e `ALTER` no schema). Abra cada script no SSMS e execute-o inteiro (F5), **nesta ordem: 01 → 02 → 03**.

| # | Script | O que faz | Tempo estimado | Como confirmar o sucesso |
|---|---|---|---|---|
| 1 | `01_criar_schema_e_permissoes.sql` | Cria o schema `catalogo` (proprietário `dbo`) e o papel `catalogo_aplicacao`, com `SELECT, INSERT, UPDATE, DELETE, EXECUTE` no schema. Ao final há um trecho comentado para associar o login da aplicação ao papel (seção 7), que pode ser executado a qualquer momento depois do 01 | < 1 s | Última mensagem: `01_criar_schema_e_permissoes: concluído...` |
| 2 | `02_estrutura.sql` | Cria as 23 tabelas no schema `catalogo` e registra as versões 1 a 12 em `catalogo.__VersaoSchema` | < 5 s | Última mensagem: `02_estrutura: estrutura criada com sucesso (23 tabelas...)` |
| 3 | `03_administrador_inicial.sql` | Cadastra o administrador inicial (matrícula `C161587`) e a linha de criação no histórico (seção 8). Para, sem alterar nada, se a matrícula já existir | < 1 s | Última mensagem: `03_administrador_inicial: concluído...` |

Em caso de erro em qualquer script, **nada daquele script fica gravado** (transação única). Pedimos que nos enviem a aba
*Messages* completa; o script pode ser executado de novo depois da correção.

## 6. Objetos criados

**Nada é criado nem alterado fora do schema `catalogo`.** Nenhuma tabela, view, procedure, função, sequência, tipo ou gatilho é
criado em outro schema, e nenhum objeto existente (inclusive em `dbo`) é lido ou alterado. Os únicos elementos de nível de banco
são o **próprio schema `catalogo`** e o **papel de banco `catalogo_aplicacao`** (um principal de segurança, necessário para conceder
as permissões no schema). Logins e usuários de banco ficam a cargo dos DBAs.

O schema `catalogo` recebe **23 tabelas**, **173 colunas**, **48 índices** (23 chaves primárias, 14 chaves únicas e 11 índices),
**27 restrições CHECK**, **50 chaves estrangeiras** (todas internas ao schema) e **46 valores padrão**. Não há views, procedures,
funções nem gatilhos.

| Tabela | Conteúdo |
|---|---|
| `catalogo.Grupo` | Áreas de gestão |
| `catalogo.Entidade` | Tipos de item de cada área |
| `catalogo.Campo` | Campos de cada tipo de item |
| `catalogo.CampoDependencia` | Regras de dependência entre campos (filtro de opções) |
| `catalogo.RestricaoUnica` | Regras de unicidade de cada tipo de item |
| `catalogo.RestricaoUnicaCampo` | Campos que compõem cada regra de unicidade |
| `catalogo.GrupoEquipe` | Equipes habilitadas em cada área de gestão |
| `catalogo.Registro` | Itens cadastrados (campos simples em JSON) |
| `catalogo.RegistroValor` | Valores tipados dos itens, para busca e ordenação |
| `catalogo.RegistroReferencia` | Referências entre itens |
| `catalogo.RegistroChaveUnica` | Hash das regras de unicidade de cada item |
| `catalogo.Equipe` | Equipes |
| `catalogo.Usuario` | Usuários do Catálogo (senha apenas como hash PBKDF2) |
| `catalogo.UsuarioEquipe` | Vínculo entre usuários e equipes |
| `catalogo.HistoricoAlteracao` | Histórico permanente de todas as alterações |
| `catalogo.ConfiguracaoEntidadeEquipe` | Configuração de aprovação por tipo de item e equipe |
| `catalogo.ModeloAvaliacao` | Modelos de avaliação de itens, por equipe |
| `catalogo.CriterioAvaliacao` | Critérios (perguntas) dos modelos de avaliação |
| `catalogo.Avaliacao` | Avaliações feitas pelos usuários |
| `catalogo.AvaliacaoResposta` | Respostas de cada avaliação |
| `catalogo.Importacao` | Importações de planilhas |
| `catalogo.ImportacaoArquivo` | Arquivo enviado em cada importação (`varbinary(max)`, até 5 MB, apagado em 7 dias) |
| `catalogo.__VersaoSchema` | Controle das versões da estrutura aplicadas (1 a 12 nesta instalação) |

Índices além das chaves: `IX_Registro_Entidade_CriadoEm`, `IX_Registro_Entidade_Equipe`, `IX_Registro_Situacao_Equipe`,
`IX_RegistroReferencia_Destino`, `IX_HistoricoAlteracao_Alvo`, `IX_HistoricoAlteracao_Usuario`, `IX_GrupoEquipe_EquipeId`,
`UX_ModeloAvaliacao_Ativo` (único e filtrado: `WHERE Ativo = 1`), `IX_Avaliacao_Modelo`, `IX_AvaliacaoResposta_Criterio` e
`IX_Importacao_Entidade_CriadoEm`.

Os únicos dados gravados pelo pacote são o registro das versões em `catalogo.__VersaoSchema` (script 02) e o administrador
inicial, com a linha de criação no histórico (script 03). Áreas de gestão, equipes, demais usuários e itens são cadastrados
depois, pela interface da aplicação.

## 7. Acesso da aplicação

A aplicação precisa de **um único login**, membro do papel `catalogo_aplicacao`, que concede exclusivamente:

- `SELECT`, `INSERT`, `UPDATE`, `DELETE` e `EXECUTE` **no schema `catalogo`**.

O login da aplicação **não** precisa (e não deve ter) permissão de DDL (`CREATE`, `ALTER`, `DROP`), `db_owner`, `db_datareader`,
`db_datawriter` nem qualquer acesso a outros schemas. Mudanças futuras de estrutura chegarão sempre como novo pacote de scripts,
executado pelos DBAs.

O nome e o tipo do login (login SQL ou conta de domínio) seguem o padrão dos DBAs. O trecho comentado ao final do script 01 usa
o marcador `<LOGIN_DA_APLICACAO>`, a ser substituído:

```sql
CREATE USER [<LOGIN_DA_APLICACAO>] FOR LOGIN [<LOGIN_DA_APLICACAO>] WITH DEFAULT_SCHEMA = catalogo;
ALTER ROLE catalogo_aplicacao ADD MEMBER [<LOGIN_DA_APLICACAO>];
```

**Nenhuma senha em texto passa por este pacote** (o administrador inicial vem só com o hash, seção 8). Se o login da aplicação for SQL, a senha é definida pelos DBAs e entregue à equipe de implantação
pelo canal seguro da casa, para a variável de ambiente da connection string no servidor da aplicação (nunca em arquivo ou script).

## 8. Administrador inicial (script 03)

O script `03_administrador_inicial.sql` cadastra o primeiro administrador do Catálogo, responsável por configurar o restante pela
interface (áreas de gestão, equipes e usuários):

- matrícula `C161587`, nome "Caio", perfil `ADMINISTRADOR`, ativo e sem equipe;
- a **senha provisória já vem criptografada**: o script contém somente o hash (PBKDF2 com HMAC-SHA512 e sal aleatório, o mesmo
  algoritmo da aplicação), gerado pela própria aplicação. A senha em texto não está no script nem passa pelos DBAs; ela é conhecida
  apenas pelo próprio administrador;
- a **troca de senha é obrigatória no primeiro acesso** (`DeveTrocarSenha = 1`): até definir uma senha nova, dentro da política
  de senhas do sistema, o administrador não consegue usar nenhuma outra função;
- se já existir um usuário com a matrícula `C161587`, o script para com uma mensagem clara, sem alterar nada.

## 9. Próximos pacotes

Este pacote instala a estrutura na **versão 12**. Cada mudança futura de estrutura será entregue como um **novo pacote** nesta mesma
forma (`AAAA-MM-DD-<descrição>`), com um script incremental escrito diretamente no schema `catalogo` e este mesmo `README.md`, e registrará a nova versão em
`catalogo.__VersaoSchema`. Para consultar a versão instalada:

```sql
SELECT Versao, ArquivoNome, AplicadoEm, Observacao FROM catalogo.__VersaoSchema ORDER BY Versao;
```
