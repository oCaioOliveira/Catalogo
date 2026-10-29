#!/usr/bin/env bash
# Simula no Docker local a migração do SharePoint como ela vai rodar no banco da CAIXA (docs/migracao-sharepoint.md, seção "Teste local").
#
#   ./database/migracao/simular_migracao.sh            recria Simulacao_Migracao (V014 + V015 + C161587), roda 01, 02 (todas as partes)
#                                                      e 03 pelo sqlcmd -i, roda tudo de novo e confere que nada mudou
#   ./database/migracao/simular_migracao.sh --manter   não recria o banco: só roda o pacote (de novo) e a conferência
#
# A senha provisória do C161587 fica em database/.estado/senha-admin-simulacao-migracao (fora do git); o primeiro login exige a troca.
# Saídas do sqlcmd em database/.estado/simulacao-migracao/.
set -euo pipefail
RAIZ="$(cd "$(dirname "$0")/../.." && pwd)"
PACOTE="$RAIZ/database/entrega/2026-10-28-migracao-sharepoint"
ESTADO="$RAIZ/database/.estado"; SAIDA="$ESTADO/simulacao-migracao"
BANCO=Simulacao_Migracao; CONTAINER=listagem-generica-db
mkdir -p "$SAIDA"
set -a; . "$RAIZ/database/.env"; set +a

sqlcmd() { docker exec -i -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b "$@"; }
# Roda um arquivo como o usuário fará: sqlcmd -i <arquivo> -f 65001, um processo por arquivo.
rodar() {
    local arquivo="$1" nome; nome="$(basename "$arquivo")"
    docker cp "$arquivo" "$CONTAINER:/tmp/simulacao/$nome" > /dev/null
    local inicio; inicio=$(date +%s%N)
    if ! sqlcmd -d "$BANCO" -f 65001 -W -i "/tmp/simulacao/$nome" > "$SAIDA/$2-$nome.txt" 2>&1; then
        echo "ERRO em $nome:"; tail -20 "$SAIDA/$2-$nome.txt"; exit 1
    fi
    printf '  %-45s %6d ms  %s\n' "$nome" $(( ($(date +%s%N) - inicio) / 1000000 )) "$(grep -v '^\s*$' "$SAIDA/$2-$nome.txt" | tail -1 | cut -c1-90)"
}
contagens() {
    sqlcmd -d "$BANCO" -h -1 -W -Q "SET NOCOUNT ON;
      SELECT 'registros', COUNT(*), CHECKSUM_AGG(CHECKSUM(NU_NSU_REGISTRO, DE_REGISTRO_DADO, DH_CRIADO, NU_VERSAO_REG)) FROM cat.CATTB011_REGISTRO;
      SELECT 'referencias', COUNT(*), 0 FROM cat.CATTB012_REGISTRO_REFERENCIA;
      SELECT 'chaves_unicas', COUNT(*), 0 FROM cat.CATTB013_REGISTRO_CHAVE_UNICA;
      SELECT 'valores', COUNT(*), 0 FROM cat.CATTB014_REGISTRO_VALOR;
      SELECT 'equipes_da_area', COUNT(*), 0 FROM cat.CATTB010_GRUPO_EQUIPE;
      SELECT 'historico', COUNT(*), 0 FROM cat.CATTB015_HISTORICO_ALTERACAO;
      SELECT 'campos', COUNT(*), CHECKSUM_AGG(CHECKSUM(*)) FROM cat.CATTB003_CAMPO;
      SELECT 'equipes', COUNT(*), 0 FROM cat.CATTB007_EQUIPE;
      SELECT 'controle', COUNT(*), 0 FROM cat.CATTB024_MIGRACAO_CONTROLE;"
}
docker exec "$CONTAINER" mkdir -p /tmp/simulacao

if [ "${1:-}" != "--manter" ]; then
    echo "== Recriando $BANCO com a estrutura da CAIXA (V014 + V015 + administrador C161587)"
    sqlcmd -d master -Q "IF DB_ID(N'$BANCO') IS NOT NULL BEGIN ALTER DATABASE [$BANCO] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$BANCO]; END;
                         CREATE DATABASE [$BANCO] COLLATE Latin1_General_CI_AI;" > /dev/null
    sqlcmd -d "$BANCO" -Q "CREATE TABLE dbo.__marcador_banco_compartilhado (Id int);" > /dev/null  # o CATDB001 é compartilhado
    rodar "$RAIZ/database/scripts/V014__estrutura_padrao_caixa.sql" base
    rodar "$RAIZ/database/scripts/V015__carga_testes_desempenho.sql" base
    # O login da aplicação, para a API apontar para a simulação (database/README.md).
    sqlcmd -d "$BANCO" -Q "IF DATABASE_PRINCIPAL_ID(N'listagem_app') IS NULL CREATE USER [listagem_app] FOR LOGIN [listagem_app] WITH DEFAULT_SCHEMA = [cat];
                           ALTER ROLE [catalogo_aplicacao] ADD MEMBER [listagem_app];" > /dev/null
    # O administrador como em produção: script gerado pelo criar-admin --gerar-sql (só o hash; troca obrigatória da senha).
    SENHA_ARQ="$ESTADO/senha-admin-simulacao-migracao"
    [ -s "$SENHA_ARQ" ] || { (umask 077; printf 'Sim%s#Mig1' "$(head -c 9 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 8)" > "$SENHA_ARQ"); }
    ( cd "$RAIZ/backend" && CATALOGO_ADMIN_SENHA="$(cat "$SENHA_ARQ")" dotnet run --project src/Catalogo.Api -v q -- criar-admin --gerar-sql \
        --matricula C161587 --nome "Administrador da migração (simulação)" --saida "$SAIDA/administrador_C161587.sql" > /dev/null )
    rodar "$SAIDA/administrador_C161587.sql" base
fi

echo "== 1ª execução do pacote"
for f in "$PACOTE"/01_*.sql "$PACOTE"/02_migracao_parte_*.sql; do rodar "$f" exec1; done
contagens > "$SAIDA/contagens-1.txt"
rodar "$PACOTE/03_conferencia.sql" exec1
grep -c ' OK' "$SAIDA/exec1-03_conferencia.sql.txt" | sed 's/^/  conferência: linhas OK = /'
grep -c 'DIVERG' "$SAIDA/exec1-03_conferencia.sql.txt" | sed 's/^/  conferência: linhas DIVERGÊNCIA = /' || true

echo "== 2ª execução (cada parte de novo: nada pode mudar)"
for f in "$PACOTE"/01_*.sql "$PACOTE"/02_migracao_parte_*.sql; do rodar "$f" exec2; done
contagens > "$SAIDA/contagens-2.txt"
if diff -u "$SAIDA/contagens-1.txt" "$SAIDA/contagens-2.txt"; then echo "  nada mudou:"; sed 's/^/    /' "$SAIDA/contagens-2.txt"
else echo "  ERRO: a 2ª execução mudou o banco"; exit 1; fi
rodar "$PACOTE/03_conferencia.sql" exec2
