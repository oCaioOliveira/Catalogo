# Catálogo — Pacote da chave de assinatura dos tokens (JWT)

**Pacote:** `2026-10-30-chave-jwt` · **Versão da estrutura:** 18 · **Banco:** `CATDB001`, SQL Server 2014, schema `cat` ·
**Destinatários:** equipe de administração de banco de dados (DBAs)

## 1. Objetivo

A partir desta versão, a aplicação sobe apenas com os seis valores que o deploy substitui no `appsettings.json` (`ENVIRONMENT`,
`CORS_ALLOW`, `DB_DATABASE`, `DB_SERVER`, `DB_USER` e `DB_PASSWORD`). A chave que assina os tokens de acesso deixa de ser uma
variável de ambiente: a própria aplicação a gera na primeira inicialização e a guarda no banco, na tabela criada por este pacote.

## 2. Conteúdo

| Arquivo | Finalidade |
|---|---|
| `01_chave_jwt.sql` | Cria `cat.CATTB025_CHAVE_JWT` (vazia) e registra a versão 18 em `cat.CATTB023_VERSAO_SCHEMA` |
| `README.md` | Este documento |

## 3. A tabela

| Coluna | Tipo | Conteúdo |
|---|---|---|
| `NU_NSU_CHAVE_JWT` | `uniqueidentifier`, chave primária (`PK_ChaveJwt`) | Identificador fixo, gravado pela aplicação (a tabela tem no máximo uma linha) |
| `CO_CHAVE_JWT` | `varchar(200)`, `CK_ChaveJwt_Tamanho` (32 caracteres ou mais) | A chave, 64 bytes aleatórios em Base64 |
| `DH_CRIADO_CJ` | `datetime2`, padrão `SYSUTCDATETIME()` | Data e hora (UTC) da geração |

A coluna `CO_CHAVE_JWT` é um **segredo**: quem a lê consegue emitir tokens válidos para a aplicação. Restrinja a leitura da tabela
ao login da aplicação (papel `catalogo_aplicacao`, que já tem acesso ao schema `cat`) e aos DBAs.

## 4. Como executar

No SSMS, conectado ao `CATDB001`: abrir `01_chave_jwt.sql` e executar inteiro (F5). Ou:

```bat
sqlcmd -S <servidor> -d CATDB001 -E -b -f 65001 -i 01_chave_jwt.sql
```

- Pré-requisito: estrutura do Catálogo na versão 13 ou maior. Sem ela, o script para **sem alterar nada**.
- Tudo roda numa transação: em caso de erro, nada fica gravado.
- Executar de novo não faz nada (`a versão 18 já está registrada. Nada a fazer.`).
- A última mensagem esperada é `01_chave_jwt: concluído (cat.CATTB025_CHAVE_JWT criada e versão 18 registrada).`

**Ordem com o deploy:** execute este pacote **antes** de publicar a versão da aplicação que o exige. Sem a tabela, a aplicação não
sobe e informa no log que falta `cat.CATTB025_CHAVE_JWT`.

## 5. Conferência

```sql
SELECT NU_NSU_VERSAO, DE_ARQUIVO_VS, DE_OBSERVACAO_VS FROM cat.CATTB023_VERSAO_SCHEMA WHERE NU_NSU_VERSAO = 18;
SELECT COUNT(*) AS Linhas FROM cat.CATTB025_CHAVE_JWT;  -- 0 antes da primeira inicialização; 1 depois
```

## 6. Troca da chave (se necessário)

Para invalidar todos os tokens emitidos (por exemplo, suspeita de vazamento), apague a linha e reinicie a aplicação; ela gera uma
chave nova. Os usuários conectados precisarão entrar de novo.

```sql
DELETE FROM cat.CATTB025_CHAVE_JWT;
```

Com mais de uma instância da aplicação, reinicie **todas** depois da exclusão: cada instância lê a chave só na inicialização.
