# 0052. Padrão de nomes da CAIXA no banco (schema `cat`, tabelas `CATTB0xx`) e SQL Server 2014

- Status: Aceito
- Data: 2026-10-27
- Substitui, em parte: [0049](0049-schema-catalogo-e-pacotes-de-entrega.md) (o schema passa de `catalogo` para `cat`; os pacotes de
  entrega continuam)

## Contexto

O banco do Catálogo no ambiente da CAIXA (`CATDB001`, servidor `scthmdadnt0017`) foi criado pelo DBA no **padrão de nomes da casa**:
schema `cat`, tabelas `CATTB001_GRUPO` a `CATTB023_VERSAO_SCHEMA` e colunas com prefixo de tipo (`NU_`, `CO_`, `NO_`, `DE_`, `DH_`/`DT_`,
`IC_`, `VR_`). A tradução do pacote de instalação trouxe erros que impediam a aplicação de funcionar: identificadores `int IDENTITY` no
lugar de `uniqueidentifier`, versão em `float` no lugar de `rowversion`, `varchar` no lugar de `nvarchar`, chave primária errada em
`CATTB013`, chaves estrangeiras sobre colunas trocadas em `CATTB003` e `CATTB012`, colunas com nomes de constraint em `CATTB006`, nenhuma
exclusão em cascata e várias unicidades ausentes. O servidor é **SQL Server 2014** (nível 120): não tem `ISJSON`, `OPENJSON` nem
`STRING_AGG`. O DBA liberou o login da aplicação para ajustar a estrutura e aceitou identificadores `uniqueidentifier`.

## Decisão

- **Os nomes físicos seguem o padrão da CAIXA em todos os ambientes**; os **tipos e as restrições seguem o modelo da aplicação**
  (`uniqueidentifier`, `nvarchar`, `rowversion`, cascatas e unicidades de [`docs/modelo-de-dados.md`](../modelo-de-dados.md)). O
  `V013__padrao_caixa.sql` move as 23 tabelas de `catalogo` para `cat` e renomeia tabelas e colunas (`sp_rename`, sem cópia de dados).
  Os nomes de constraints e índices (`PK_Grupo`, `UQ_RegistroChaveUnica_Valor`...) **não mudam**: o tradutor de erros depende deles.
  Onde o nome do DBA tinha prefixo incoerente com o tipo ou erro de digitação, ficou o nome coerente (ex.: `NU_USUARIO_DECISAO`, não
  `DECICAO`; `IC_ESCOPO_EQUIPE_ENT` para um `bit`; `NU_ESTRELA` para um número).
- **No código, as configurações do EF continuam com os nomes lógicos** (Grupo, `Registro.Dados`); o `CatalogoDbContext` aplica os
  nomes físicos de `NomesBanco` depois delas. Uma coluna sem nome físico faz o modelo falhar ao ser montado. O de-para existe no V013
  e no `NomesBanco`, e o teste `NomesBancoTests` exige que sejam iguais.
- **Compatível com SQL Server 2014**: o EF usa `UseCompatibilityLevel(120)` (um `Contains` vira `IN`, sem `OPENJSON`), e os CHECKs com
  `ISJSON` saem (o JSON é sempre gerado pela aplicação). O `CK_Campo_Lista` fica sem o `ISJSON`.
- **O schema é configurável** (`Banco:Schema`, padrão `cat`): os testes de integração usam o schema `cat_testes` no próprio `CATDB001`,
  porque o login não pode criar outro banco. O schema de testes é montado por `database/ferramentas/estrutura_padrao_caixa.ps1`, com as
  mesmas peças do pacote de produção, e os testes só apagam dados num banco terminado em `_Testes` ou num schema terminado em `_testes`.
- **Collation explícita nas colunas de texto**: o `CATDB001` foi criado com `SQL_Latin1_General_CP1_CI_AS` (diferencia acentos), e a
  aplicação conta com `Latin1_General_CI_AI` nas buscas e nas unicidades de texto (RN09, filtros `contem`). A estrutura do pacote e do
  schema de testes declara `COLLATE Latin1_General_CI_AI` em todas as 46 colunas de texto; a collation do banco não importa mais.
- **Produção recebe o pacote `database/entrega/2026-10-27-padrao-caixa/`**, gerado pela mesma ferramenta: guarda os usuários já
  cadastrados, recria a estrutura (consolidada + V013) e devolve os usuários, numa transação só. Pode ser reaplicado enquanto o banco
  só tiver usuários (e o histórico da criação deles); com qualquer outro dado, para sem alterar nada.

## Consequências

- A aplicação funciona no banco da CAIXA sem mudar o contrato da API (os ids continuam `uuid`) nem o front-end.
- Todo script novo (V014 em diante) usa os nomes físicos (`cat.CATTB011_REGISTRO`, `NU_NSU_REGISTRO`); tabela ou coluna nova entra
  também no `NomesBanco`.
- O teste da carga inicial do V009 saiu: os nomes que o V009 (imutável) usa não existem mais. O `OutroSistemaNoMesmoBancoTests` só roda
  com um login de administrador de testes (Docker local); no banco da CAIXA, o login tem `db_owner` e o teste aparece como ignorado.
- No Docker local o banco já é `Latin1_General_CI_AI` (o V013 não muda collation): o comportamento é o mesmo nos dois ambientes.
- As ferramentas bash de comparação e de carga de área (`comparar_bancos.sh`, `gerar_carga_area.sh` e os `.sql` delas) ainda usam os
  nomes antigos e precisam ser atualizadas antes do próximo uso.
