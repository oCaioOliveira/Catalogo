#!/usr/bin/env bash
# Aplica os scripts database/scripts/V*.sql que ainda não foram aplicados, em ordem de versão.
# O controle fica na tabela catalogo.__VersaoSchema. Para no primeiro erro (sqlcmd -b).
# Bancos anteriores ao V012 ainda têm a tabela em dbo: ela é usada até o V012 movê-la para catalogo.
#
# Variáveis de ambiente: DB_HOST, DB_NAME, MSSQL_SA_PASSWORD, SCRIPTS_DIR
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
DB_HOST="${DB_HOST:-db}"
DB_NAME="${DB_NAME:-ListagemGenerica}"
SCRIPTS_DIR="${SCRIPTS_DIR:-/scripts}"

sql() {
    "$SQLCMD" -C -b -S "$DB_HOST" -U sa -P "$MSSQL_SA_PASSWORD" -d "$DB_NAME" "$@"
}

# Onde está a tabela de controle: catalogo (o normal), dbo (banco anterior ao V012) ou em lugar nenhum (banco novo).
tabela_versao() {
    sql -h -1 -W -Q "SET NOCOUNT ON;
SELECT CASE WHEN OBJECT_ID(N'catalogo.__VersaoSchema', N'U') IS NOT NULL THEN 'catalogo.__VersaoSchema'
            WHEN OBJECT_ID(N'dbo.__VersaoSchema', N'U') IS NOT NULL THEN 'dbo.__VersaoSchema'
            ELSE '' END;" | tr -d '[:space:]'
}

if [ -z "$(tabela_versao)" ]; then
    echo "==> Criando o schema catalogo e a tabela catalogo.__VersaoSchema"
    sql -Q "IF SCHEMA_ID(N'catalogo') IS NULL EXEC (N'CREATE SCHEMA catalogo AUTHORIZATION dbo;');
CREATE TABLE catalogo.__VersaoSchema (
    Versao      INT           NOT NULL CONSTRAINT PK___VersaoSchema PRIMARY KEY,
    ArquivoNome NVARCHAR(260) NOT NULL,
    AplicadoEm  DATETIME2(0)  NOT NULL CONSTRAINT DF___VersaoSchema_AplicadoEm DEFAULT SYSUTCDATETIME(),
    Observacao  NVARCHAR(500) NULL
);"
fi
echo "==> Controle de versões em $(tabela_versao)"

shopt -s nullglob
arquivos=("$SCRIPTS_DIR"/V*.sql)
if [ ${#arquivos[@]} -eq 0 ]; then
    echo "Nenhum script V*.sql encontrado em $SCRIPTS_DIR."
    exit 0
fi

aplicados=0
declare -A versoes_vistas=()
# sort -V ordena V2 antes de V10 mesmo sem zeros à esquerda.
while IFS= read -r caminho; do
    arquivo="$(basename "$caminho")"
    if [[ ! "$arquivo" =~ ^V([0-9]+)__.+\.sql$ ]]; then
        echo "ERRO: nome fora do padrão V<numero>__<descricao>.sql: $arquivo" >&2
        exit 1
    fi
    versao=$((10#${BASH_REMATCH[1]}))
    if [ -n "${versoes_vistas[$versao]:-}" ]; then
        echo "ERRO: versão $versao duplicada: ${versoes_vistas[$versao]} e $arquivo" >&2
        exit 1
    fi
    versoes_vistas[$versao]="$arquivo"

    ja_aplicado="$(sql -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM $(tabela_versao) WHERE Versao = $versao;" | tr -d '[:space:]')"
    if [ "$ja_aplicado" != "0" ]; then
        echo "--  $arquivo já aplicado, pulando."
        continue
    fi

    echo "==> Aplicando $arquivo"
    # -x desliga a substituição de variáveis $(...), igual a rodar o script no SSMS em produção.
    sql -x -i "$caminho"
    # O V012 move a tabela de controle: o registro vai para onde ela estiver depois do script.
    sql -Q "INSERT INTO $(tabela_versao) (Versao, ArquivoNome) VALUES ($versao, N'${arquivo//\'/\'\'}');"
    echo "OK  $arquivo registrado na versão $versao."
    aplicados=$((aplicados + 1))
done < <(printf '%s\n' "${arquivos[@]}" | sort -V)

echo "==> Concluído. Scripts aplicados nesta execução: $aplicados"
