# Catálogo — Pacote de reestruturação do banco no padrão da CAIXA

**Pacote:** `2026-10-27-padrao-caixa` · **Versão da estrutura:** 13 · **Destinatários:** equipe de administração de banco de dados (DBAs)

## 1. Objetivo

O banco `CATDB001` recebeu as tabelas do Catálogo no padrão de nomes da casa (schema `cat`, tabelas `CATTB001` a `CATTB023`), mas a
tradução do pacote `2026-10-26-instalacao-inicial` trouxe diferenças que impediam a aplicação de funcionar:

| # | Problema | Efeito |
|---|---|---|
| 1 | Identificadores `int IDENTITY` (a aplicação usa `uniqueidentifier`) | A aplicação gera o identificador antes de gravar; nada podia ser gravado |
| 2 | Versão em `float` (a aplicação usa `rowversion`) | O controle de alterações simultâneas não funcionava |
| 3 | `varchar` no lugar de `nvarchar` | Perda de caracteres fora do Latin-1 |
| 4 | `CATTB013`: chave primária (restrição, **entidade**) | Só cabia um item por regra de unicidade |
| 5 | `CATTB012`: chave estrangeira do campo de origem sobre `NU_REGISTRO_ORIGEM` | Nenhuma referência entre itens podia ser gravada |
| 6 | `CATTB003`: chave estrangeira da entidade referenciada sobre a coluna errada | O banco não conferia a entidade referenciada |
| 7 | `CATTB006`: colunas com nomes de constraint, sem a entidade e sem chaves estrangeiras | Regras de unicidade sem integridade |
| 8 | Nenhuma exclusão em cascata e unicidades ausentes (`CATTB004`, `009`, `010`, `017`, `022`) | Exclusões falhavam; duplicidades possíveis |
| 9 | Datas padrão em hora local (`GETDATE()`) | A aplicação trabalha em UTC |

Este pacote substitui essa estrutura pela da aplicação, **mantendo o padrão de nomes da casa** e compatível com o SQL Server 2014.

## 2. Conteúdo

| Arquivo | Finalidade |
|---|---|
| `01_reestruturar_padrao_caixa.sql` | Recria as 23 tabelas no schema `cat`, preservando os usuários já cadastrados |
| `README.md` | Este documento |

O script é **gerado** por `database/ferramentas/estrutura_padrao_caixa.ps1 -Modo Pacote` (não edite à mão) a partir da estrutura do
pacote de instalação inicial e do script versionado `V013__padrao_caixa.sql`.

## 3. O que o script faz

1. Confere os pré-requisitos e **para sem alterar nada** se: o schema `catalogo` tiver tabelas; ou houver dados do Catálogo (itens,
   áreas, equipes, histórico além da criação dos usuários) nas tabelas atuais.
2. Guarda os usuários de `CATTB008_USUARIO` (matrícula, nome, hash da senha, perfil, situação) e exclui as 23 tabelas do schema `cat`.
3. Cria a estrutura da aplicação (versões 1 a 12) num schema provisório `catalogo` e a converte para o padrão (versão 13): move para
   `cat`, renomeia tabelas e colunas e exclui o schema provisório.
4. Registra a versão 13, devolve os usuários (novo identificador, **mesma senha**, linha de criação no histórico) e confere o resultado.

Tudo numa **única transação**: em caso de erro, nada fica alterado. Compatível com SQL Server 2014 (nível 120), sem variáveis de
SQLCMD e sem `USE`. Tempo: menos de 10 segundos.

## 4. Execução

Conecte-se ao `CATDB001` no SSMS com um usuário `db_owner`, abra o script e execute-o inteiro (F5). A última mensagem deve ser
`01_reestruturar: concluído com sucesso (23 tabelas no schema cat, versão 13, N usuário(s) preservado(s)).`

Para conferir: `SELECT NU_NSU_VERSAO, DE_ARQUIVO_VS, DH_APLICADO_VS FROM cat.CATTB023_VERSAO_SCHEMA ORDER BY NU_NSU_VERSAO;`

> Este pacote já foi aplicado no `CATDB001` em 2026-10-27 pela equipe do Catálogo, com o login `usr_gic` (autorizado pelo DBA).

## 5. Pontos de atenção

- **Acesso da aplicação:** o papel `catalogo_aplicacao` recebe `SELECT, INSERT, UPDATE, DELETE, EXECUTE` no schema `cat`. O login
  `usr_gic` hoje é `db_owner`; recomendamos, depois da estabilização, associá-lo só ao papel e trocar a senha (hoje igual ao nome).
- **Collation:** o banco usa `SQL_Latin1_General_CP1_CI_AS`; as colunas de texto do Catálogo declaram `Latin1_General_CI_AI`
  (buscas e unicidades sem diferenciar maiúsculas nem acentos), então a collation do banco não precisa mudar.
- **Reaplicação:** enquanto o banco só tiver usuários (e o histórico da criação deles), o script pode ser executado de novo;
  com qualquer outro dado (áreas, equipes, itens, histórico de alterações), ele para sem alterar nada.
- **Schema de testes:** a equipe do Catálogo mantém, no mesmo banco, o schema `cat_testes` (mesma estrutura, dados descartáveis) para
  os testes automatizados, já que o login não pode criar outro banco.
- **Versão do SQL Server:** o suporte estendido do SQL Server 2014 terminou em julho de 2024; a aplicação também funciona no 2016 ou superior.
