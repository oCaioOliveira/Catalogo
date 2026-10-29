#!/usr/bin/env bash
# Gera o script de carga (ou de reconfiguração) de UMA área de gestão (metadados) a partir de um banco do Catálogo, para levar a
# outro ambiente (ex.: produção, pelo pacote de entrega dos DBAs). Só LÊ o banco de origem.
#
# Uso (da raiz do repositório, com o SQL Server local no ar):
#   database/ferramentas/gerar_carga_area.sh <chave_da_area> <arquivo_saida.sql> [banco_de_origem]
#   ex.: database/ferramentas/gerar_carga_area.sh testes_desempenho database/entrega/<pacote>/01_area_<area>.sql
#
# O script gerado é SEMPRE no padrão da CAIXA (schema cat, tabelas CATTB0xx, docs/adr/0052) e compatível com SQL Server
# 2014 (nível 120): roda manualmente no SSMS (GO, sem variáveis de SQLCMD); grava tudo numa transação; e, antes do COMMIT,
# confere as quantidades e a impressão digital (impressao_digital_area.sql) contra as do banco de origem.
# Entram: área, tipos de item, campos, campo de exibição, unicidades e seus campos, dependências, com os MESMOS GUIDs.
# Não entram: itens, equipes, CATTB010 (equipes habilitadas), usuários, histórico, aprovação e avaliação.
#
# Dois modos (variável MODO):
#   carga         (padrão) a área é NOVA no destino: para, sem alterar nada, se ela já existir.
#   reconfigurar  a área JÁ EXISTE no destino (mesmo GUID) e é levada à configuração da origem: para, sem alterar nada, se ela
#                 tiver itens (ou histórico, configuração por equipe, modelo de avaliação ou importação nos tipos de item); se já
#                 estiver igual à origem (impressão digital), não faz nada. Os tipos de item são atualizados pelo GUID; campos,
#                 unicidades e dependências são excluídos e gravados de novo, com os GUIDs da origem. As equipes habilitadas ficam.
#
# Variáveis de ambiente opcionais:
#   MODO      carga (padrão) ou reconfigurar
#   ORIGEM    schema de onde a área é lida: cat (padrão, ler_area_cat.sql) ou catalogo (as tabelas antigas, V012, que ficaram
#             intactas no banco local; ler_area_catalogo.sql)
#   PREFIXO   prefixo das mensagens PRINT do script gerado (padrão: carga_<area> ou reconfigurar_<area>)
#   CONTAINER (padrão listagem-generica-db) e ENV_FILE (padrão database/.env, para a senha do sa)
set -euo pipefail

if [ $# -lt 2 ]; then
    echo "Uso: $0 <chave_da_area> <arquivo_saida.sql> [banco_de_origem]" >&2
    exit 2
fi

GRUPO="$1"
SAIDA="$2"
BANCO="${3:-ListagemGenerica}"
PASTA="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODO="${MODO:-carga}"
ORIGEM="${ORIGEM:-cat}"
PREFIXO="${PREFIXO:-${MODO}_$GRUPO}"
CONTAINER="${CONTAINER:-listagem-generica-db}"
ENV_FILE="${ENV_FILE:-$PASTA/../.env}"
# A estrutura do schema cat existe desde o V013 (no CATDB001) e o V014 (Docker local, que registra 1 a 13).
VERSAO_MINIMA=13

if [[ ! "$GRUPO" =~ ^[a-z0-9_]+$ ]]; then
    echo "ERRO: chave de área inválida: $GRUPO" >&2
    exit 2
fi
if [[ ! "$PREFIXO" =~ ^[A-Za-z0-9_]+$ ]]; then
    echo "ERRO: prefixo inválido: $PREFIXO" >&2
    exit 2
fi
case "$MODO" in
    carga|reconfigurar) ;;
    *) echo "ERRO: MODO deve ser carga ou reconfigurar (informado: $MODO)" >&2; exit 2 ;;
esac
case "$ORIGEM" in
    cat|catalogo) LEITURA="$PASTA/ler_area_$ORIGEM.sql" ;;
    *) echo "ERRO: ORIGEM deve ser cat ou catalogo (informado: $ORIGEM)" >&2; exit 2 ;;
esac

MSSQL_SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)"

sql() {
    docker exec -i -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" "$CONTAINER" \
        /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -d "$BANCO" -b -I -y 0 -f o:65001 "$@"
}

preambulo="SET NOCOUNT ON; DECLARE @GrupoChave varchar(100) = '$GRUPO'; DECLARE @Prefixo nvarchar(60) = N'$PREFIXO';
DECLARE @Modo varchar(20) = '$MODO';"

corpo="$( { echo "$preambulo"; cat "$LEITURA"; cat "$PASTA/extrair_carga_area.sql"; } | sql -i /dev/stdin)"

leitura_cat="$(cat "$PASTA/ler_area_cat.sql")"
trecho="$(cat "$PASTA/impressao_digital_area.sql")"
resumo="$( { echo "$preambulo"; cat "$LEITURA"; echo "$trecho";
  echo "SELECT CONCAT((SELECT COUNT(*) FROM @Canonico WHERE Secao = 2), '|', (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3), '|',
       (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4), '|', (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5), '|',
       (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6), '|', CONVERT(varchar(66), @Impressao, 1), '|',
       (SELECT Nome FROM #AreaGrupo));"; } \
  | sql -i /dev/stdin | tr -d '\r' | sed '/^$/d')"
IFS='|' read -r ENTIDADES CAMPOS RESTRICOES RESTRICAO_CAMPOS DEPENDENCIAS IMPRESSAO NOME_AREA <<< "$resumo"
if [[ ! "$IMPRESSAO" =~ ^0x[0-9A-F]{64}$ ]]; then
    echo "ERRO: não consegui calcular a impressão digital da área $GRUPO: $resumo" >&2
    exit 1
fi

if [ "$MODO" = "carga" ]; then
    TITULO="Carga da área de gestão \"$NOME_AREA\" ($GRUPO), padrão CAIXA (schema cat)"
    O_QUE="Cria a configuração da área (metadados) no schema cat, com os mesmos identificadores (GUIDs) e as mesmas chaves da origem:"
    PROTECAO="para, sem alterar nada, se a estrutura não estiver instalada ou se a área já existir."
    EQUIPES="A área nasce SEM equipes habilitadas: só o
   administrador a vê, até habilitar as equipes pela interface (Configurações → Áreas de gestão)."
    EXISTENCIA="ELSE IF EXISTS (SELECT 1 FROM cat.CATTB001_GRUPO WHERE CO_CHAVE = '$GRUPO')
BEGIN
    RAISERROR (N'$PREFIXO: a área de gestão $GRUPO já existe neste banco. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO"
    FINAL="carregada"
else
    TITULO="Reconfiguração da área de gestão \"$NOME_AREA\" ($GRUPO), padrão CAIXA (schema cat)"
    O_QUE="Leva a área (que já existe, com o mesmo GUID) à configuração da origem: atualiza a área e os tipos de item pelo GUID,
   exclui os campos, as unicidades e as dependências atuais e grava os da origem, com os mesmos identificadores (GUIDs) e chaves:"
    PROTECAO="para, sem alterar nada, se a estrutura não estiver instalada, se a área não existir ou se ela tiver itens
   (ou histórico, configuração por equipe, modelo de avaliação ou importação nos tipos de item). Se a área já estiver na
   configuração nova (mesma impressão digital), não faz nada: pode ser executado de novo."
    EQUIPES="As equipes habilitadas na área não mudam."
    EXISTENCIA="ELSE IF NOT EXISTS (SELECT 1 FROM cat.CATTB001_GRUPO WHERE CO_CHAVE = '$GRUPO')
BEGIN
    RAISERROR (N'$PREFIXO: a área de gestão $GRUPO não existe neste banco. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

PRINT '$PREFIXO: conferindo se a área já está na configuração nova e se não tem dados...';
DECLARE @GrupoChave varchar(100) = '$GRUPO';
$leitura_cat
$trecho
IF (SELECT COUNT(*) FROM @Canonico WHERE Secao = 2) = $ENTIDADES
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3) = $CAMPOS
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4) = $RESTRICOES
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5) = $RESTRICAO_CAMPOS
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6) = $DEPENDENCIAS
   AND @Impressao = $IMPRESSAO
BEGIN
    PRINT '$PREFIXO: a área $GRUPO já está na configuração nova. Nada a fazer.';
    SET NOEXEC ON;
END
ELSE
BEGIN
    DECLARE @Dados nvarchar(400) = CONCAT(
        (SELECT COUNT(*) FROM cat.CATTB011_REGISTRO WHERE NU_NSU_CE_ENTIDADE_REG IN (SELECT Id FROM #AreaEntidade)), N' item(ns), ',
        (SELECT COUNT(*) FROM cat.CATTB015_HISTORICO_ALTERACAO WHERE NU_NSU_CE_ENTIDADE_HA IN (SELECT Id FROM #AreaEntidade)), N' linha(s) de histórico, ',
        (SELECT COUNT(*) FROM cat.CATTB016_CONFIG_ENTIDADE_EQUIPE WHERE NU_NSU_CE_ENTIDADE_CEE IN (SELECT Id FROM #AreaEntidade)), N' configuração(ões) por equipe, ',
        (SELECT COUNT(*) FROM cat.CATTB017_MODELO_AVALIACAO WHERE NU_NSU_CE_ENTIDADE_MA IN (SELECT Id FROM #AreaEntidade)), N' modelo(s) de avaliação, ',
        (SELECT COUNT(*) FROM cat.CATTB021_IMPORTACAO WHERE NU_NSU_CE_GRUPO_IMP IN (SELECT Id FROM #AreaGrupo)), N' importação(ões)');
    IF @Dados <> N'0 item(ns), 0 linha(s) de histórico, 0 configuração(ões) por equipe, 0 modelo(s) de avaliação, 0 importação(ões)'
    BEGIN
        RAISERROR (N'$PREFIXO: a área $GRUPO tem dados (%s). A reconfiguração só roda com a área vazia. Nada foi alterado.', 16, 1, @Dados);
        SET NOEXEC ON;
    END;
END;
GO"
    FINAL="reconfigurada"
fi

{
cat <<EOF
/* =====================================================================
   CATÁLOGO — $TITULO
   ---------------------------------------------------------------------
   GERADO por database/ferramentas/gerar_carga_area.sh (MODO=$MODO) em $(date '+%Y-%m-%d %H:%M'), a partir do banco $BANCO
   (schema de origem: $ORIGEM). Não edite à mão: gere de novo.

   $O_QUE
     - 1 área de gestão, $ENTIDADES tipos de item, $CAMPOS campos (tipos, tamanhos, obrigatoriedade, opções, ordem, referências
       e ações de exclusão), o campo de exibição de cada tipo de item, $RESTRICOES unicidades ($RESTRICAO_CAMPOS campos) e $DEPENDENCIAS dependência(s).
   NÃO cria itens, equipes, usuários, histórico, aprovação nem avaliação. $EQUIPES

   Pré-requisito: a estrutura do Catálogo no schema cat (cat.CATTB023_VERSAO_SCHEMA na versão $VERSAO_MINIMA ou maior).
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo. Não usa variáveis de SQLCMD. Compatível com SQL Server 2014
   (nível 120). Tempo estimado: menos de 1 segundo.
   Proteção: $PROTECAO
   Tudo roda numa transação (um identificador repetido também desfaz tudo): antes do COMMIT, o script confere as quantidades e
   a impressão digital da configuração ($IMPRESSAO); se algo divergir, desfaz tudo.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '$PREFIXO: verificando pré-requisitos...';
IF OBJECT_ID(N'cat.CATTB023_VERSAO_SCHEMA', N'U') IS NULL OR OBJECT_ID(N'cat.CATTB001_GRUPO', N'U') IS NULL
BEGIN
    RAISERROR (N'$PREFIXO: a estrutura do Catálogo não está instalada no schema cat. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF (SELECT MAX(NU_NSU_VERSAO) FROM cat.CATTB023_VERSAO_SCHEMA) < $VERSAO_MINIMA
BEGIN
    RAISERROR (N'$PREFIXO: este script exige a estrutura na versão $VERSAO_MINIMA ou maior (consulte cat.CATTB023_VERSAO_SCHEMA). Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
$EXISTENCIA

BEGIN TRANSACTION;

EOF
echo "$corpo" | tr -d '\r' | sed -e 's/[[:space:]]*$//' -e '/./,$!d'
cat <<EOF

PRINT '$PREFIXO: conferindo a configuração antes de gravar...';
DECLARE @GrupoChave varchar(100) = '$GRUPO';
$leitura_cat
$trecho
IF (SELECT COUNT(*) FROM @Canonico WHERE Secao = 2) <> $ENTIDADES
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3) <> $CAMPOS
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4) <> $RESTRICOES
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5) <> $RESTRICAO_CAMPOS
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6) <> $DEPENDENCIAS
   OR @Impressao <> $IMPRESSAO
    THROW 50003, N'$PREFIXO: a configuração gravada difere da esperada (quantidades ou impressão digital). Nada foi gravado.', 1;

COMMIT TRANSACTION;
PRINT '$PREFIXO: área $GRUPO $FINAL com sucesso ($ENTIDADES tipos de item, $CAMPOS campos, $RESTRICOES unicidades, $DEPENDENCIAS dependência(s)).';
GO

SET NOEXEC OFF;
GO
EOF
} > "$SAIDA"

echo "Gerado: $SAIDA"
echo "  área: $NOME_AREA ($GRUPO), origem $BANCO (schema $ORIGEM), modo $MODO"
echo "  tipos de item: $ENTIDADES | campos: $CAMPOS | unicidades: $RESTRICOES ($RESTRICAO_CAMPOS campos) | dependências: $DEPENDENCIAS"
echo "  impressão digital: $IMPRESSAO"
