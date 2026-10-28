<#
.SYNOPSIS
    Executa um script .sql como o SSMS: lote a lote (separados por GO), na mesma sessão, mostrando as mensagens (PRINT e
    erros). Para quem não tem SSMS nem sqlcmd na máquina. Sai com código 1 se algum lote der erro.

.EXAMPLE
    ./executar_script_sql.ps1 -Arquivo ../entrega/2026-10-27-padrao-caixa/01_reestruturar_padrao_caixa.sql -ConnectionString "Server=...;Database=CATDB001;..."
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Arquivo,
    [Parameter(Mandatory)][string]$ConnectionString
)

$ErrorActionPreference = 'Stop'
$caminho = Resolve-Path $Arquivo
$texto = [System.IO.File]::ReadAllText($caminho, [System.Text.Encoding]::UTF8)

Add-Type -AssemblyName System.Data
$conexao = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$conexao.FireInfoMessageEventOnUserErrors = $true
$conexao.add_InfoMessage({
        param($s, $e)
        foreach ($erro in $e.Errors) {
            if ($erro.Message -match '^Caution|^Atenção') { continue }
            if ($erro.Class -gt 10) { $script:erros++; Write-Host "  ERRO $($erro.Number): $($erro.Message)" -ForegroundColor Red }
            else { Write-Host "  $($erro.Message)" }
        }
    })
$script:erros = 0
$conexao.Open()
try {
    Write-Host "==> $caminho em $($conexao.DataSource)/$($conexao.Database)"
    foreach ($lote in [regex]::Split($texto, '(?im)^\s*GO\s*$')) {
        if ([string]::IsNullOrWhiteSpace($lote)) { continue }
        $comando = $conexao.CreateCommand()
        $comando.CommandTimeout = 600
        $comando.CommandText = $lote
        [void]$comando.ExecuteNonQuery()
    }
}
finally {
    $conexao.Close()
}
if ($script:erros -gt 0) { Write-Host "Concluído com $($script:erros) erro(s)." -ForegroundColor Red; exit 1 }
Write-Host 'Concluído sem erros.'
