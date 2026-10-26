#!/usr/bin/env bash
# Compara dois bancos do Catálogo no SQL Server local: a ESTRUTURA do schema catalogo (descrever_estrutura.sql) e, se for
# informada uma área, a CONFIGURAÇÃO dela (impressao_digital_area.sql, todas as linhas canônicas). Só lê.
# Sai com 0 se não houver nenhuma diferença; com 1 se houver (e mostra o diff).
#
# Uso: database/ferramentas/comparar_bancos.sh <banco_a> <banco_b> [chave_da_area]
#   ex.: database/ferramentas/comparar_bancos.sh ListagemGenerica Simulacao_Caixa testes_desempenho
# Variáveis de ambiente opcionais: CONTAINER (padrão listagem-generica-db), ENV_FILE (padrão database/.env) e SAIDA (pasta
# onde ficam as descrições; padrão: uma pasta temporária).
set -euo pipefail

if [ $# -lt 2 ]; then
    echo "Uso: $0 <banco_a> <banco_b> [chave_da_area]" >&2
    exit 2
fi
A="$1"; B="$2"; GRUPO="${3:-}"
PASTA="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONTAINER="${CONTAINER:-listagem-generica-db}"
ENV_FILE="${ENV_FILE:-$PASTA/../.env}"
SAIDA="${SAIDA:-$(mktemp -d)}"
MSSQL_SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)"

sql() {
    local banco="$1"; shift
    docker exec -i -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" "$CONTAINER" \
        /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -d "$banco" -b -I -y 0 -f o:65001 "$@" | tr -d '\r'
}

descrever_area() {
    { echo "SET NOCOUNT ON; DECLARE @GrupoChave varchar(100) = '$GRUPO';"; cat "$PASTA/impressao_digital_area.sql";
      echo "SELECT Linha FROM @Canonico ORDER BY Secao, Chave1, Chave2;";
      echo "SELECT CONCAT(N'IMPRESSAO_DIGITAL|', CONVERT(varchar(66), @Impressao, 1));"; } | sql "$1" -i /dev/stdin
}

resultado=0
for banco in "$A" "$B"; do
    sql "$banco" -i /dev/stdin < "$PASTA/descrever_estrutura.sql" > "$SAIDA/estrutura-$banco.txt"
done
echo "== Estrutura do schema catalogo: $A ($(wc -l < "$SAIDA/estrutura-$A.txt") linhas) x $B ($(wc -l < "$SAIDA/estrutura-$B.txt") linhas)"
for tipo in TABELA COLUNA INDICE CHECK FK DEFAULT; do
    printf '   %-8s %5s x %-5s\n' "$tipo" "$(grep -c "^$tipo|" "$SAIDA/estrutura-$A.txt" || true)" "$(grep -c "^$tipo|" "$SAIDA/estrutura-$B.txt" || true)"
done
if diff -u "$SAIDA/estrutura-$A.txt" "$SAIDA/estrutura-$B.txt"; then
    echo "   diferenças: 0"
else
    resultado=1
fi

if [ -n "$GRUPO" ]; then
    for banco in "$A" "$B"; do
        descrever_area "$banco" > "$SAIDA/area-$GRUPO-$banco.txt"
    done
    echo "== Configuração da área $GRUPO: $A ($(wc -l < "$SAIDA/area-$GRUPO-$A.txt") linhas) x $B ($(wc -l < "$SAIDA/area-$GRUPO-$B.txt") linhas)"
    for tipo in Grupo Entidade Campo RestricaoUnica RestricaoUnicaCampo CampoDependencia; do
        printf '   %-20s %4s x %-4s\n' "$tipo" "$(grep -c "^$tipo|" "$SAIDA/area-$GRUPO-$A.txt" || true)" "$(grep -c "^$tipo|" "$SAIDA/area-$GRUPO-$B.txt" || true)"
    done
    grep '^IMPRESSAO_DIGITAL|' "$SAIDA/area-$GRUPO-$A.txt" | sed "s/^/   $A: /"
    grep '^IMPRESSAO_DIGITAL|' "$SAIDA/area-$GRUPO-$B.txt" | sed "s/^/   $B: /"
    if diff -u "$SAIDA/area-$GRUPO-$A.txt" "$SAIDA/area-$GRUPO-$B.txt"; then
        echo "   diferenças: 0"
    else
        resultado=1
    fi
fi

echo "== Descrições em $SAIDA"
exit $resultado
