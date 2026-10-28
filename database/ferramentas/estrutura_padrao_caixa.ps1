<#
.SYNOPSIS
    Monta a estrutura do Catálogo no padrão da CAIXA (schema cat, tabelas CATTB0xx) a partir das MESMAS peças, em dois usos:

    -Modo Pacote        Gera database/entrega/2026-10-27-padrao-caixa/01_reestruturar_padrao_caixa.sql (o script que os DBAs
                        rodam no SSMS). Não conecta em banco nenhum.
    -Modo SchemaTestes  Cria (ou recria) um schema de TESTES (nome terminado em _testes, ex.: cat_testes) no banco da
                        connection string, com a carga da área testes_desempenho, para os testes de integração rodarem num
                        banco compartilhado, sem Docker (backend/README.md). Só apaga tabelas do próprio schema de testes.

    As peças, nesta ordem:
      1. a estrutura consolidada do pacote 2026-10-26-instalacao-inicial (02_estrutura.sql, V001 a V012), criada num schema
         provisório catalogo, sem o ISJSON (o CATDB001 é SQL Server 2014);
      2. (só no SchemaTestes) a carga da área testes_desempenho, como os V001, V007, V008 e V010 deixam;
      3. o V013 (database/scripts/V013__padrao_caixa.sql), que move tudo para o schema de destino com os nomes do padrão.
    Assim a estrutura do Docker local (V001 a V013), a do CATDB001 e a do schema de testes são a mesma.

.EXAMPLE
    ./estrutura_padrao_caixa.ps1 -Modo Pacote
    ./estrutura_padrao_caixa.ps1 -Modo SchemaTestes -ConnectionString $env:ConnectionStrings__CatalogoTestes -Schema cat_testes
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Pacote', 'SchemaTestes')][string]$Modo,
    [string]$ConnectionString,
    [string]$Schema = 'cat_testes'
)

$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..')
$utf8 = New-Object System.Text.UTF8Encoding($true)

function Ler([string]$caminho) { [System.IO.File]::ReadAllText((Join-Path $raiz $caminho), [System.Text.Encoding]::UTF8) }

# Peça 1: o DDL do 02_estrutura.sql (entre o BEGIN TRANSACTION e o COMMIT), sem o ISJSON. Os CHECKs de JSON ficam com uma
# condição neutra só até o V013, que os remove pelo nome.
function EstruturaBase {
    $texto = Ler 'entrega/2026-10-26-instalacao-inicial/02_estrutura.sql'
    $inicio = $texto.IndexOf("BEGIN TRANSACTION;")
    $fim = $texto.IndexOf("COMMIT TRANSACTION;")
    if ($inicio -lt 0 -or $fim -lt $inicio) { throw '02_estrutura.sql: BEGIN/COMMIT TRANSACTION não encontrados.' }
    $ddl = $texto.Substring($inicio + "BEGIN TRANSACTION;".Length, $fim - $inicio - "BEGIN TRANSACTION;".Length)
    $ddl = [regex]::Replace($ddl, 'ISJSON\((\w+)\)\s*=\s*1', '$1 IS NOT NULL')
    if ($ddl -match 'ISJSON') { throw 'Sobrou ISJSON na estrutura base.' }
    # Collation explícita em toda coluna de texto: a aplicação conta com Latin1_General_CI_AI (buscas e unicidades sem diferenciar
    # maiúsculas nem acentos), e o CATDB001 foi criado com SQL_Latin1_General_CP1_CI_AS (docs/adr/0052).
    $ddl = [regex]::Replace($ddl, '(?i)\b(n?varchar\s*\(\s*(?:\d+|max)\s*\))', '$1 COLLATE Latin1_General_CI_AI')
    return $ddl
}

# Peça 3: o V013, com o schema de destino trocado.
function V013([string]$destino) {
    $texto = Ler 'scripts/V013__padrao_caixa.sql'
    $original = "DECLARE @Destino sysname = N'cat';"
    if (-not $texto.Contains($original)) { throw "V013: linha '$original' não encontrada." }
    return $texto.Replace($original, "DECLARE @Destino sysname = N'$destino';")
}

# Interrompe os lotes seguintes (no SSMS) se a transação do script não estiver mais ativa: um erro já desfez tudo.
$guarda = @"
IF XACT_STATE() <> 1
BEGIN
    RAISERROR (N'Um passo anterior falhou e a transação foi desfeita. Nada foi alterado; os passos seguintes não serão executados.', 16, 1);
    SET NOEXEC ON;
END;
GO
"@

if ($Modo -eq 'Pacote') {
    $cabecalho = @"
/* =====================================================================
   CATÁLOGO — Pacote 2026-10-27-padrao-caixa — Script 01 de 01: reestruturação do schema cat
   ---------------------------------------------------------------------
   GERADO por database/ferramentas/estrutura_padrao_caixa.ps1 -Modo Pacote. Não edite à mão: gere de novo.

   Substitui a estrutura atual do schema cat (tabelas CATTB001 a CATTB023, criadas sem dados do Catálogo) pela estrutura da
   aplicação, com os nomes do padrão da casa:
     1. guarda os usuários já cadastrados (CATTB008: matrícula, nome, hash da senha, perfil) e exclui as 23 tabelas antigas;
     2. cria a estrutura consolidada da aplicação (versões 1 a 12) num schema provisório catalogo;
     3. converte para o padrão (V013): move para o schema cat e renomeia tabelas e colunas; o schema catalogo é excluído;
     4. registra a versão 13, devolve os usuários guardados (com novo identificador, a mesma senha e a linha de criação no
        histórico) e confere o resultado antes do COMMIT.
   Identificadores uniqueidentifier, textos nvarchar, versão rowversion e exclusões em cascata, como a aplicação exige.

   Compatível com SQL Server 2014 (nível 120). Sem variáveis de SQLCMD, sem USE. Execução: SSMS, conectado ao CATDB001, com um
   usuário db_owner (ou com CREATE SCHEMA, CREATE TABLE, ALTER e CONTROL no schema cat). Tempo estimado: menos de 10 segundos.
   Proteção: para sem alterar nada se houver itens, áreas, equipes ou histórico além da criação dos usuários. Sem isso, pode ser
   executado de novo (ex.: para aplicar uma correção do pacote): os usuários são sempre preservados.
   Uma única transação: em caso de erro, nada fica alterado.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Exigidos pelo índice filtrado UX_ModeloAvaliacao_Ativo (já são o padrão do SSMS).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

PRINT '01_reestruturar: verificando pré-requisitos...';
DECLARE @ComDados nvarchar(2000) = N'';
SELECT @ComDados = @ComDados + N' ' + t.name
FROM sys.tables t
WHERE t.schema_id = SCHEMA_ID(N'cat')
  AND t.name NOT IN (N'CATTB008_USUARIO', N'CATTB015_HISTORICO_ALTERACAO', N'CATTB023_VERSAO_SCHEMA')
  AND EXISTS (SELECT 1 FROM sys.partitions p WHERE p.object_id = t.object_id AND p.index_id IN (0, 1) AND p.rows > 0);

IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'catalogo'))
BEGIN
    RAISERROR (N'01_reestruturar: o schema catalogo já tem tabelas neste banco. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'cat.CATTB008_USUARIO', N'U') IS NULL
BEGIN
    RAISERROR (N'01_reestruturar: a tabela cat.CATTB008_USUARIO não existe. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF @ComDados <> N''
BEGIN
    RAISERROR (N'01_reestruturar: há dados do Catálogo nas tabelas:%s. Nada foi alterado.', 16, 1, @ComDados);
    SET NOEXEC ON;
END
ELSE
BEGIN
    -- Histórico: só a criação dos usuários (que é refeita no passo 4). As colunas têm o mesmo nome nas duas estruturas.
    DECLARE @HistoricoOutro int;
    EXEC sys.sp_executesql N'SELECT @n = COUNT(*) FROM cat.CATTB015_HISTORICO_ALTERACAO WHERE NOT (NO_ALVO_TIPO = ''USUARIO'' AND NO_OPERACAO = ''CRIACAO'')',
        N'@n int OUTPUT', @n = @HistoricoOutro OUTPUT;
    IF @HistoricoOutro > 0
    BEGIN
        RAISERROR (N'01_reestruturar: o histórico tem %d alteração(ões) além da criação dos usuários. Nada foi alterado.', 16, 1, @HistoricoOutro);
        SET NOEXEC ON;
    END;
END;
GO

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------
   Passo 1 de 4: guarda os usuários e exclui a estrutura anterior do schema cat
   --------------------------------------------------------------------- */
PRINT '01_reestruturar: 1/4 guardando os usuários e excluindo a estrutura anterior do schema cat...';
IF OBJECT_ID(N'tempdb..#UsuariosAnteriores') IS NOT NULL DROP TABLE #UsuariosAnteriores;
SELECT CAST(CO_MATRICULA AS varchar(20)) AS Matricula, CAST(NO_USUARIO AS nvarchar(150)) AS Nome,
       CAST(CO_SENHA_HASH AS nvarchar(500)) AS SenhaHash, CAST(NO_PERFIL AS varchar(20)) AS Perfil,
       CAST(IC_ATIVO_USR AS bit) AS Ativo, CAST(IC_TROCAR_SENHA AS bit) AS DeveTrocarSenha
INTO #UsuariosAnteriores
FROM cat.CATTB008_USUARIO;
PRINT CONCAT('01_reestruturar: ', @@ROWCOUNT, ' usuário(s) guardado(s).');

DECLARE @Sql nvarchar(max) = N'';
SELECT @Sql = @Sql + N'ALTER TABLE cat.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + NCHAR(10)
FROM sys.foreign_keys WHERE schema_id = SCHEMA_ID(N'cat');
EXEC (@Sql);
SET @Sql = N'';
SELECT @Sql = @Sql + N'DROP TABLE cat.' + QUOTENAME(name) + N';' + NCHAR(10)
FROM sys.tables WHERE schema_id = SCHEMA_ID(N'cat');
EXEC (@Sql);
IF SCHEMA_ID(N'catalogo') IS NULL EXEC (N'CREATE SCHEMA catalogo AUTHORIZATION dbo;');
GO

$guarda
"@
    $final = @"
/* ---------------------------------------------------------------------
   Passo 4 de 4: versão 13, usuários guardados e conferência
   --------------------------------------------------------------------- */
PRINT '01_reestruturar: 4/4 registrando a versão 13, devolvendo os usuários e conferindo...';

INSERT cat.CATTB023_VERSAO_SCHEMA (NU_NSU_VERSAO, DE_ARQUIVO_VS, DE_OBSERVACAO_VS)
VALUES (13, N'V013__padrao_caixa.sql', N'Pacote 2026-10-27-padrao-caixa (01_reestruturar_padrao_caixa.sql).');

DECLARE @Correlacao uniqueidentifier = NEWID();
DECLARE @Novos TABLE (Id uniqueidentifier NOT NULL, Matricula varchar(20) NOT NULL, Nome nvarchar(150) NOT NULL,
                      Perfil varchar(20) NOT NULL, Ativo bit NOT NULL);

INSERT cat.CATTB008_USUARIO (NU_NSU_USUARIO, CO_MATRICULA, NO_USUARIO, CO_SENHA_HASH, NO_PERFIL, IC_ATIVO_USR, IC_TROCAR_SENHA)
OUTPUT inserted.NU_NSU_USUARIO, inserted.CO_MATRICULA, inserted.NO_USUARIO, inserted.NO_PERFIL, inserted.IC_ATIVO_USR
INTO @Novos (Id, Matricula, Nome, Perfil, Ativo)
SELECT NEWID(), Matricula, Nome, SenhaHash, Perfil, Ativo, DeveTrocarSenha FROM #UsuariosAnteriores;

-- Histórico (RN32): a criação de cada usuário devolvido, com o próprio usuário como autor (como o 03 da instalação inicial).
INSERT cat.CATTB015_HISTORICO_ALTERACAO (NO_ALVO_TIPO, NU_ALVO, NO_OPERACAO, NU_NSU_CE_USUARIO_HA, CO_MATRI_USUARIO, NU_CORRELACAO, DE_DADO_DEPOIS)
SELECT 'USUARIO', Id, 'CRIACAO', Id, Matricula, @Correlacao,
       N'{"matricula":"' + REPLACE(REPLACE(Matricula, N'\', N'\\'), N'"', N'\"')
     + N'","nome":"' + REPLACE(REPLACE(Nome, N'\', N'\\'), N'"', N'\"')
     + N'","perfil":"' + Perfil + N'","ativo":' + CASE WHEN Ativo = 1 THEN N'true' ELSE N'false' END + N',"equipes":[]}'
FROM @Novos;

DECLARE @Tabelas int = (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'cat') AND name LIKE N'CATTB0[0-2][0-9][_]%');
DECLARE @Usuarios int = (SELECT COUNT(*) FROM cat.CATTB008_USUARIO);
DECLARE @Guardados int = (SELECT COUNT(*) FROM #UsuariosAnteriores);
DECLARE @Cascatas int = (SELECT COUNT(*) FROM sys.foreign_keys WHERE schema_id = SCHEMA_ID(N'cat') AND delete_referential_action = 1);
IF @Tabelas <> 23 OR @Usuarios <> @Guardados OR SCHEMA_ID(N'catalogo') IS NOT NULL OR @Cascatas = 0
   OR TYPE_NAME((SELECT system_type_id FROM sys.columns WHERE object_id = OBJECT_ID(N'cat.CATTB001_GRUPO') AND name = N'NU_NSU_GRUPO')) <> N'uniqueidentifier'
BEGIN
    DECLARE @Mensagem nvarchar(400) = CONCAT(N'01_reestruturar: conferência falhou (tabelas=', @Tabelas, N' de 23, usuários=', @Usuarios,
        N' de ', @Guardados, N', cascatas=', @Cascatas, N'). Nada foi alterado.');
    THROW 50013, @Mensagem, 1;
END;

COMMIT TRANSACTION;
DROP TABLE #UsuariosAnteriores;
PRINT CONCAT('01_reestruturar: concluído com sucesso (23 tabelas no schema cat, versão 13, ', @Usuarios, ' usuário(s) preservado(s)).');
GO

SET NOEXEC OFF;
GO
"@
    $script = @(
        $cabecalho.TrimEnd()
        ''
        '/* ---------------------------------------------------------------------'
        '   Passo 2 de 4: estrutura consolidada (V001 a V012) no schema provisório catalogo, sem ISJSON'
        '   (gerado de entrega/2026-10-26-instalacao-inicial/02_estrutura.sql)'
        '   --------------------------------------------------------------------- */'
        "PRINT '01_reestruturar: 2/4 criando a estrutura consolidada no schema provisório catalogo...';"
        (EstruturaBase).Trim()
        'GO'
        ''
        $guarda
        ''
        '/* ---------------------------------------------------------------------'
        '   Passo 3 de 4: V013 (database/scripts/V013__padrao_caixa.sql): schema cat e nomes do padrão'
        '   --------------------------------------------------------------------- */'
        "PRINT '01_reestruturar: 3/4 convertendo para o padrão de nomes (V013)...';"
        'GO'
        (V013 'cat').Trim()
        ''
        $guarda
        ''
        $final.Trim()
        ''
    ) -join "`r`n"
    $destino = Join-Path $raiz 'entrega/2026-10-27-padrao-caixa/01_reestruturar_padrao_caixa.sql'
    [void](New-Item -ItemType Directory -Force -Path (Split-Path $destino))
    [System.IO.File]::WriteAllText($destino, $script, $utf8)
    Write-Host "Gerado: $destino"
    return
}

# ---------------------------------------------------------------- SchemaTestes
if (-not $ConnectionString) { throw 'Informe -ConnectionString.' }
if ($Schema -notmatch '^[a-z][a-z0-9_]*_testes$') { throw "O schema de testes precisa terminar em _testes (informado: '$Schema')." }

Add-Type -AssemblyName System.Data
$conexao = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$conexao.add_InfoMessage({ param($s, $e) foreach ($m in $e.Message -split "`n") { if ($m -and $m -notmatch '^Caution|^Aten') { Write-Host "  $m" } } })
$conexao.Open()

function Executar([string]$sql, [string]$descricao) {
    Write-Host "==> $descricao"
    foreach ($lote in [regex]::Split($sql, '(?im)^\s*GO\s*$')) {
        if ([string]::IsNullOrWhiteSpace($lote)) { continue }
        $comando = $conexao.CreateCommand()
        $comando.CommandTimeout = 600
        $comando.CommandText = $lote
        [void]$comando.ExecuteNonQuery()
    }
}

try {
    # O schema provisório não pode existir: ele é o "catalogo" da peça 1.
    $verificacao = $conexao.CreateCommand()
    $verificacao.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'catalogo')"
    if ([int]$verificacao.ExecuteScalar() -gt 0) { throw 'O schema catalogo já tem tabelas neste banco: o script não continua.' }

    Executar @"
DECLARE @schema sysname = N'$Schema', @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id))
            + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.foreign_keys WHERE schema_id = SCHEMA_ID(@schema);
EXEC (@sql);
SET @sql = N'';
SELECT @sql = @sql + N'DROP TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.tables WHERE schema_id = SCHEMA_ID(@schema);
EXEC (@sql);
IF SCHEMA_ID(N'catalogo') IS NULL EXEC (N'CREATE SCHEMA catalogo AUTHORIZATION dbo;');
"@ "Limpando o schema $Schema e criando o schema provisório catalogo"

    # Tudo numa transação só: se algo falhar, nem o schema provisório nem o de testes ficam pela metade.
    Executar "SET XACT_ABORT ON;`r`nBEGIN TRANSACTION;" 'Abrindo a transação'
    Executar ((EstruturaBase) + "`r`n") 'Estrutura consolidada (V001 a V012) em catalogo'

    # Peça 2: a área testes_desempenho como os V001, V003, V007, V008 e V010 deixam (os testes de integração contam com ela).
    $v001 = Ler 'scripts/V001__listagem_generica.sql'
    $marcador = $v001.IndexOf('DECLARE @grupo uniqueidentifier')
    if ($marcador -lt 0) { throw 'V001: início da carga não encontrado.' }
    $carga = $v001.Substring($marcador).Replace('dbo.', 'catalogo.')
    $carga += @"

-- V003: cadastros de apoio globais (RN27).
UPDATE e SET EscopoEquipe = 0
FROM catalogo.Entidade e
JOIN catalogo.Grupo g ON g.Id = e.GrupoId AND g.Chave = 'testes_desempenho'
WHERE e.Chave IN ('comunidade', 'sigla', 'ambiente', 'status_resultado');

UPDATE c SET TipoDado = 'URL'
FROM catalogo.Campo c
JOIN catalogo.Entidade e ON e.Id = c.EntidadeId
JOIN catalogo.Grupo g ON g.Id = e.GrupoId AND g.Chave = 'testes_desempenho'
WHERE (e.Chave = 'link' AND c.Chave = 'url') OR (e.Chave = 'projeto' AND c.Chave = 'url_projeto');

UPDATE e SET NomePlural = v.NomePlural, Ordem = v.Ordem, Icone = v.Icone
FROM catalogo.Entidade e
JOIN catalogo.Grupo g ON g.Id = e.GrupoId AND g.Chave = 'testes_desempenho'
JOIN (VALUES
        ('relatorio_teste',  N'Relatórios de Teste',   1, 'pi-chart-line'),
        ('projeto',          N'Projetos',              2, 'pi-briefcase'),
        ('endpoint_servico', N'Endpoints de Serviço',  3, 'pi-link'),
        ('rtc',              N'RTCs',                  4, 'pi-ticket'),
        ('comunidade',       N'Comunidades',           5, 'pi-users'),
        ('sigla',            N'Siglas',                6, 'pi-tag'),
        ('ambiente',         N'Ambientes',             7, 'pi-server'),
        ('status_resultado', N'Status de Resultado',   8, 'pi-flag'),
        ('cenario_teste',    N'Cenários de Teste',     9, 'pi-list-check'),
        ('link',             N'Links',                10, 'pi-external-link')) v (Chave, NomePlural, Ordem, Icone)
     ON v.Chave = e.Chave;

INSERT catalogo.__VersaoSchema (Versao, ArquivoNome, Observacao)
VALUES (13, N'V013__padrao_caixa.sql', N'Schema de testes montado por database/ferramentas/estrutura_padrao_caixa.ps1.');
"@
    Executar $carga 'Carga da área testes_desempenho'

    Executar (V013 $Schema) "V013 para o schema $Schema"
    Executar 'COMMIT TRANSACTION;' 'Confirmando'
    Write-Host "Concluído: schema $Schema pronto."
}
finally {
    $conexao.Close()
}
