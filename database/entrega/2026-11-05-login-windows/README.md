# Catálogo — Pacote do login pela autenticação do Windows (remoção das colunas de senha)

**Pacote:** `2026-11-05-login-windows` · **Versão da estrutura:** 19 · **Banco:** `CATDB001`, SQL Server 2014, schema `cat` ·
**Destinatários:** equipe de administração de banco de dados (DBAs)

## 1. Objetivo

A partir desta versão, o acesso ao Catálogo é feito pela autenticação integrada do Windows (Active Directory): o usuário é
identificado pela matrícula do login do Windows, e a aplicação deixa de guardar senhas. Este pacote remove da tabela de usuários as
colunas que guardavam a senha, a obrigação de troca e o bloqueio por tentativas, que não são mais usadas.

## 2. Conteúdo

| Arquivo | Finalidade |
|---|---|
| `01_remover_senha.sql` | Remove as colunas de senha de `cat.CATTB008_USUARIO` e registra a versão 19 em `cat.CATTB023_VERSAO_SCHEMA` |
| `README.md` | Este documento |

## 3. O que muda na tabela `cat.CATTB008_USUARIO`

| Coluna removida | Conteúdo anterior | Objetos removidos junto |
|---|---|---|
| `CO_SENHA_HASH` | Hash da senha | — |
| `IC_TROCAR_SENHA` | Troca de senha obrigatória no próximo acesso | padrão `DF_Usuario_DeveTrocarSenha` |
| `NU_TENTATIVA_FALHA` | Tentativas de login erradas seguidas | padrão `DF_Usuario_TentativasFalhas` e verificação `CK_Usuario_TentativasFalhas` |
| `DH_BLOQUEADO` | Fim do bloqueio por tentativas | — |

Os padrões e a verificação são localizados pelo catálogo do sistema (`sys.default_constraints`, `sys.check_constraints`), e não
pelo nome: o script funciona mesmo que os nomes no `CATDB001` sejam outros. **Nenhuma linha é excluída**: matrícula, nome, perfil,
situação e data do último acesso de cada usuário permanecem.

## 4. Como executar

No SSMS, conectado ao `CATDB001`: abrir `01_remover_senha.sql` e executar inteiro (F5). Ou:

```bat
sqlcmd -S <servidor> -d CATDB001 -E -b -f 65001 -i 01_remover_senha.sql
```

- Pré-requisito: estrutura do Catálogo na versão 18 (pacote `2026-10-30-chave-jwt`) ou maior. Sem ela, o script para **sem
  alterar nada**.
- Permissão necessária: `ALTER` na tabela `cat.CATTB008_USUARIO`.
- Tudo roda numa transação: em caso de erro, nada fica gravado.
- Executar de novo não faz nada (`a versão 19 já está registrada ... Nada a fazer.`).
- A última mensagem esperada é `01_remover_senha: concluído (colunas de senha removidas e versão 19 registrada).`

**Ordem com o deploy:** publique a versão da aplicação com o login pelo Windows e execute este pacote **logo em seguida**.
A versão anterior da aplicação lê essas colunas e deixa de funcionar sem elas, por isso o pacote não deve ser executado antes. Já a
versão nova funciona com as colunas ainda presentes, exceto no cadastro de usuários novos (a coluna `CO_SENHA_HASH` é obrigatória
e a aplicação não a preenche mais), que volta a funcionar assim que o pacote é executado.

## 5. Conferência

```sql
SELECT NU_NSU_VERSAO, DE_ARQUIVO_VS, DE_OBSERVACAO_VS FROM cat.CATTB023_VERSAO_SCHEMA WHERE NU_NSU_VERSAO = 19;

-- Nenhuma linha: as quatro colunas não existem mais.
SELECT name FROM sys.columns
WHERE object_id = OBJECT_ID(N'cat.CATTB008_USUARIO')
  AND name IN (N'CO_SENHA_HASH', N'IC_TROCAR_SENHA', N'NU_TENTATIVA_FALHA', N'DH_BLOQUEADO');
```

## 6. Reversão

A remoção das colunas não tem volta automática: os hashes de senha são descartados. Se for preciso voltar à versão anterior da
aplicação, as colunas teriam de ser recriadas (com `CO_SENHA_HASH` obrigatória) e cada usuário receberia uma senha nova. Antes de
executar, faça a cópia de segurança de rotina do banco.
