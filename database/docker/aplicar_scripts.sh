#!/usr/bin/env bash
# Aplica os scripts database/scripts/V*.sql que ainda não foram aplicados, em ordem de versão.
# O controle fica na tabela cat.CATTB023_VERSAO_SCHEMA (padrão da CAIXA, docs/adr/0052). Para no primeiro erro (sqlcmd -b).
#
# A estrutura do schema cat nasce do V014 (vazia, ao lado das tabelas antigas): se a tabela de controle não existe, o V014
# roda primeiro e registra as versões 1 a 13 como consolidadas. Assim o V001 a V013 NUNCA rodam aqui: as tabelas antigas do
# schema catalogo (e os dados delas) ficam intactas, e o V013 (que as moveria para cat) fica só para o histórico da CAIXA.
#
# Variáveis de ambiente: DB_HOST, DB_NAME, MSSQL_SA_PASSWORD, SCRIPTS_DIR
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
DB_HOST="${DB_HOST:-db}"
DB_NAME="${DB_NAME:-ListagemGenerica}"
SCRIPTS_DIR="${SCRIPTS_DIR:-/scripts}"
TABELA_VERSAO="cat.CATTB023_VERSAO_SCHEMA"
VERSAO_ESTRUTURA=14

sql() {
    "$SQLCMD" -C -b -S "$DB_HOST" -U sa -P "$MSSQL_SA_PASSWORD" -d "$DB_NAME" "$@"
}

registrar() {
    sql -Q "INSERT INTO $TABELA_VERSAO (NU_NSU_VERSAO, DE_ARQUIVO_VS) VALUES ($1, N'${2//\'/\'\'}');"
}

tem_tabela_versao="$(sql -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'$TABELA_VERSAO', N'U') IS NULL THEN 0 ELSE 1 END;" | tr -d '[:space:]')"
if [ "$tem_tabela_versao" = "0" ]; then
    shopt -s nullglob
    base=("$SCRIPTS_DIR"/V0"$VERSAO_ESTRUTURA"__*.sql)
    shopt -u nullglob
    if [ ${#base[@]} -ne 1 ]; then
        echo "ERRO: não achei um único script da versão $VERSAO_ESTRUTURA (estrutura do schema cat) em $SCRIPTS_DIR." >&2
        exit 1
    fi
    echo "==> $TABELA_VERSAO não existe: criando a estrutura do schema cat com $(basename "${base[0]}")"
    sql -x -i "${base[0]}"
    registrar "$VERSAO_ESTRUTURA" "$(basename "${base[0]}")"
    echo "OK  $(basename "${base[0]}") registrado na versão $VERSAO_ESTRUTURA."
fi
echo "==> Controle de versões em $TABELA_VERSAO"

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

    ja_aplicado="$(sql -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM $TABELA_VERSAO WHERE NU_NSU_VERSAO = $versao;" | tr -d '[:space:]')"
    if [ "$ja_aplicado" != "0" ]; then
        echo "--  $arquivo já aplicado, pulando."
        continue
    fi

    echo "==> Aplicando $arquivo"
    # -x desliga a substituição de variáveis $(...), igual a rodar o script no SSMS em produção.
    sql -x -i "$caminho"
    registrar "$versao" "$arquivo"
    echo "OK  $arquivo registrado na versão $versao."
    aplicados=$((aplicados + 1))
done < <(printf '%s\n' "${arquivos[@]}" | sort -V)

echo "==> Concluído. Scripts aplicados nesta execução: $aplicados"
