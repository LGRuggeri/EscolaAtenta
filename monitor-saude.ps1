<#
.SYNOPSIS
Monitor de saude do EscolaAtenta para a secretaria da escola.

.DESCRIPTION
Script de monitoramento continuo que verifica se a API e o banco de dados
estao funcionando corretamente. Ideal para o administrador da escola manter
o terminal aberto e acompanhar visualmente o status do sistema.

Faz uma requisicao ao endpoint /health a cada 30 segundos e exibe o resultado
com cores no terminal: verde (operacional) ou vermelho (falha).

.NOTES
Ajuste a variavel $apiUrl se a porta da API for diferente.
#>

$apiUrl = "http://localhost:5114/health"
$intervaloSegundos = 30

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Monitor de Saude - EscolaAtenta" -ForegroundColor Cyan
Write-Host "  Verificando: $apiUrl" -ForegroundColor Cyan
Write-Host "  Intervalo: a cada $intervaloSegundos segundos" -ForegroundColor Cyan
Write-Host "  Pressione Ctrl+C para encerrar" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

while ($true) {
    $dataHora = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    try {
        $response = Invoke-RestMethod -Uri $apiUrl -Method Get -TimeoutSec 10

        if ($response.status -eq "Healthy") {
            Write-Host "[$dataHora] " -NoNewline
            Write-Host "Sistema EscolaAtenta Operacional" -ForegroundColor Green -NoNewline
            Write-Host " | DB: $($response.components.PostgreSQL.status) ($($response.components.PostgreSQL.duration))"
        }
        elseif ($response.status -eq "Degraded") {
            Write-Host "[$dataHora] " -NoNewline
            Write-Host "ATENCAO: Sistema Degradado (funcionando com restricoes)" -ForegroundColor Yellow
        }
        else {
            Write-Host "[$dataHora] " -NoNewline
            Write-Host "ALERTA: Falha no Sistema (Verifique o banco de dados)" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "[$dataHora] " -NoNewline
        Write-Host "ALERTA: Falha no Sistema (Verifique o banco de dados)" -ForegroundColor Red
        Write-Host "         Erro: $($_.Exception.Message)" -ForegroundColor DarkRed
    }

    Start-Sleep -Seconds $intervaloSegundos
}
