# Catálogo — Pacote da área de gestão Testes de Desempenho

**Pacote:** `2026-10-28-area-testes-desempenho` · **Pré-requisito:** estrutura do Catálogo no schema `cat` (versão 13 ou maior) ·
**Destinatários:** equipe de administração de banco de dados (DBAs)

## 1. Objetivo

Cadastrar a **configuração** da área de gestão **Testes de Desempenho** (chave `testes_desempenho`) no banco `CATDB001`: a área, os
10 tipos de item, os 40 campos, as 8 regras de unicidade (10 campos) e 1 dependência entre campos, com os mesmos identificadores
usados pela aplicação.

## 2. Conteúdo

| Arquivo | Finalidade |
|---|---|
| `01_area_testes_desempenho.sql` | Insere a configuração da área nas tabelas `cat.CATTB001` a `cat.CATTB006` |
| `README.md` | Este documento |

O script é **gerado** por `database/ferramentas/gerar_carga_area.sh` (não edite à mão).

## 3. O que o script faz e o que não faz

- Roda **depois** da instalação inicial e da reestruturação no padrão da casa (pacote `2026-10-27-padrao-caixa`).
- **Não cria dados**: nenhum item, equipe, usuário, histórico, aprovação ou avaliação. A área nasce **sem equipes habilitadas**: o
  administrador do Catálogo as habilita pela interface.
- Para **sem alterar nada** se a estrutura do schema `cat` não estiver instalada ou se a área `testes_desempenho` já existir.
- Uma única transação: antes do `COMMIT`, confere as quantidades e a impressão digital da configuração; se algo divergir, desfaz tudo.
- Compatível com SQL Server 2014 (nível 120). Sem variáveis de SQLCMD e sem `USE`. Tempo: menos de 1 segundo.

## 4. Execução

Conecte-se ao `CATDB001` no SSMS com um usuário que possa inserir no schema `cat`, abra o script e execute-o inteiro (F5). A última
mensagem deve ser `carga_testes_desempenho: área testes_desempenho carregada com sucesso (10 tipos de item, 40 campos, 8 unicidades,
1 dependência(s); sem equipes habilitadas).`
