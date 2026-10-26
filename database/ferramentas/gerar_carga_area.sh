#!/usr/bin/env bash
# Gera o script de carga de UMA área de gestão (metadados) a partir de um banco do Catálogo, para levar a outro ambiente
# (ex.: produção, pelo pacote de entrega dos DBAs). Só LÊ o banco de origem.
#
# Uso (da raiz do repositório, com o SQL Server local no ar):
#   database/ferramentas/gerar_carga_area.sh <chave_da_area> <arquivo_saida.sql> [banco_de_origem]
#   ex.: database/ferramentas/gerar_carga_area.sh testes_desempenho database/entrega/<pacote>/0N_carga_testes_desempenho.sql
#
# O script gerado: roda manualmente no SSMS (GO, sem variáveis de SQLCMD); para se a estrutura não estiver na mesma versão
# do banco de origem ou se a área já existir; grava tudo numa transação; e, antes do
# COMMIT, confere as quantidades e a impressão digital (impressao_digital_area.sql) contra as do banco de origem.
# Entram: área, tipos de item, campos, campo de exibição, unicidades e seus campos, dependências, com os MESMOS GUIDs.
# Não entram: itens, equipes, GrupoEquipe (a área nasce sem equipes), usuários, histórico, aprovação e avaliação.
#
# Variáveis de ambiente opcionais: CONTAINER (padrão listagem-generica-db) e ENV_FILE (padrão database/.env, para a senha do sa).
set -euo pipefail

if [ $# -lt 2 ]; then
    echo "Uso: $0 <chave_da_area> <arquivo_saida.sql> [banco_de_origem]" >&2
    exit 2
fi

GRUPO="$1"
SAIDA="$2"
BANCO="${3:-ListagemGenerica}"
PASTA="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONTAINER="${CONTAINER:-listagem-generica-db}"
ENV_FILE="${ENV_FILE:-$PASTA/../.env}"

if [[ ! "$GRUPO" =~ ^[a-z0-9_]+$ ]]; then
    echo "ERRO: chave de área inválida: $GRUPO" >&2
    exit 2
fi

MSSQL_SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)"

sql() {
    docker exec -i -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" "$CONTAINER" \
        /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -d "$BANCO" -b -I -y 0 -f o:65001 "$@"
}

corpo="$(sql -v GRUPO="$GRUPO" -i /dev/stdin < "$PASTA/extrair_carga_area.sql")"

trecho="$(cat "$PASTA/impressao_digital_area.sql")"
resumo="$( { echo "SET NOCOUNT ON; DECLARE @GrupoChave varchar(100) = '$GRUPO';"; echo "$trecho";
  echo "SELECT CONCAT((SELECT COUNT(*) FROM @Canonico WHERE Secao = 2), '|', (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3), '|',
       (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4), '|', (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5), '|',
       (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6), '|', CONVERT(varchar(66), @Impressao, 1), '|',
       (SELECT MAX(Versao) FROM catalogo.__VersaoSchema), '|', (SELECT Nome FROM catalogo.Grupo WHERE Chave = @GrupoChave));"; } \
  | sql -i /dev/stdin | tr -d '\r' | sed '/^$/d')"
IFS='|' read -r ENTIDADES CAMPOS RESTRICOES RESTRICAO_CAMPOS DEPENDENCIAS IMPRESSAO VERSAO NOME_AREA <<< "$resumo"
if [[ ! "$IMPRESSAO" =~ ^0x[0-9A-F]{64}$ ]]; then
    echo "ERRO: não consegui calcular a impressão digital da área $GRUPO: $resumo" >&2
    exit 1
fi

{
cat <<EOF
/* =====================================================================
   CATÁLOGO — Carga da área de gestão "$NOME_AREA" ($GRUPO)
   ---------------------------------------------------------------------
   GERADO por database/ferramentas/gerar_carga_area.sh em $(date '+%Y-%m-%d %H:%M'), a partir do banco $BANCO
   (estrutura na versão $VERSAO). Não edite à mão: gere de novo.

   Cria a configuração da área (metadados), com os mesmos identificadores (GUIDs) do banco de origem:
     - 1 área de gestão, $ENTIDADES tipos de item, $CAMPOS campos (tipos, tamanhos, obrigatoriedade, opções, ordem, referências
       e ações de exclusão), o campo de exibição de cada tipo de item, $RESTRICOES unicidades ($RESTRICAO_CAMPOS campos) e $DEPENDENCIAS dependência(s).
   NÃO cria itens, equipes, usuários, histórico, aprovação nem avaliação. A área nasce SEM equipes habilitadas: só o
   administrador a vê, até habilitar as equipes pela interface (Configurações → Áreas de gestão).

   Pré-requisito: a estrutura do Catálogo (schema catalogo) na versão $VERSAO.
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo. Não usa variáveis de SQLCMD. Tempo estimado: menos de 1 segundo.
   Proteção: para, sem alterar nada, se a estrutura não estiver na versão $VERSAO ou se a área já existir. Tudo roda numa
   transação (um identificador repetido também desfaz tudo): antes do COMMIT, o script confere as quantidades e a impressão
   digital da configuração ($IMPRESSAO); se algo divergir, desfaz tudo.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '03_carga: verificando pré-requisitos...';
IF OBJECT_ID(N'catalogo.__VersaoSchema', N'U') IS NULL OR OBJECT_ID(N'catalogo.Grupo', N'U') IS NULL
BEGIN
    RAISERROR (N'03_carga: a estrutura do Catálogo não está instalada (schema catalogo). Execute antes o 01 e o 02. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF (SELECT MAX(Versao) FROM catalogo.__VersaoSchema) <> $VERSAO
BEGIN
    RAISERROR (N'03_carga: esta carga foi gerada para a estrutura na versão $VERSAO, e o banco está em outra versão (consulte catalogo.__VersaoSchema). Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM catalogo.Grupo WHERE Chave = '$GRUPO')
BEGIN
    RAISERROR (N'03_carga: a área de gestão $GRUPO já está carregada. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;

EOF
echo "$corpo" | tr -d '\r' | sed -e 's/[[:space:]]*$//' -e '/./,$!d'
cat <<EOF

PRINT '03_carga: conferindo a carga antes de gravar...';
DECLARE @GrupoChave varchar(100) = '$GRUPO';
$trecho
IF (SELECT COUNT(*) FROM @Canonico WHERE Secao = 2) <> $ENTIDADES
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3) <> $CAMPOS
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4) <> $RESTRICOES
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5) <> $RESTRICAO_CAMPOS
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6) <> $DEPENDENCIAS
   OR @Impressao <> $IMPRESSAO
    THROW 50003, N'03_carga: a configuração gravada difere da esperada (quantidades ou impressão digital). Nada foi gravado.', 1;

COMMIT TRANSACTION;
PRINT '03_carga: área $GRUPO carregada com sucesso ($ENTIDADES tipos de item, $CAMPOS campos, $RESTRICOES unicidades, $DEPENDENCIAS dependência(s); sem equipes habilitadas).';
GO

SET NOEXEC OFF;
GO
EOF
} > "$SAIDA"

echo "Gerado: $SAIDA"
echo "  área: $NOME_AREA ($GRUPO), estrutura versão $VERSAO"
echo "  tipos de item: $ENTIDADES | campos: $CAMPOS | unicidades: $RESTRICOES ($RESTRICAO_CAMPOS campos) | dependências: $DEPENDENCIAS"
echo "  impressão digital: $IMPRESSAO"
