<#
.SYNOPSIS
Script de inicialização segura local (On-Premise) para o EscolaAtenta.

.DESCRIPTION
Como o sistema roda internamente na escola, este script substitui uma pipeline de nuvem (CI/CD).
Ele atua como um "Gatekeeper" local verificando a segurança do Supply Chain antes de subir a API.

Executa as seguintes etapas:
1. Verifica pacotes NuGet vulneráveis (Supply Chain Security).
2. Aplica as migrações (Migrations) do EF Core ao banco de dados Docker local.
3. Inicia a API em modo de Produção (com Rate Limit e Headers de segurança).
#>

$ErrorActionPreference = "Stop"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "[Deploy Local On-Premise] - EscolaAtenta" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# Passo 1: Supply Chain Security (dotnet list package --vulnerable)
Write-Host "[1/3] Verificando Supply Chain - Pacotes Vulneraveis..." -ForegroundColor Yellow
$vulnCheck = & dotnet list EscolaAtenta.sln package --vulnerable --include-transitive 2>&1
$vulnOutput = $vulnCheck | Out-String

if ($vulnOutput -match "tem os seguintes pacotes vulneráveis" -or $vulnOutput -match "has the following vulnerable packages") {
    Write-Host ""
    Write-Host "ALERTA CRÍTICO: VULNERABILIDADES DE SEGURANÇA ENCONTRADAS!" -ForegroundColor Red
    Write-Host "=========================================================" -ForegroundColor Red
    Write-Host $vulnOutput -ForegroundColor Yellow
    Write-Host "=========================================================" -ForegroundColor Red
    Write-Host "A Inicialização foi abortada. Atualize os pacotes corrompidos antes de subir o servidor da escola." -ForegroundColor Red
    exit 1
} else {
    Write-Host "[Segurança] Nenhum pacote malicioso ou vulnerável (CVE) detectado." -ForegroundColor Green
}

Write-Host ""
# Passo 2: Configuracao de Credenciais Seguras (.env)
Write-Host "[2/4] Verificando Credenciais Seguras (.env)..." -ForegroundColor Yellow
if (Test-Path ".env") {
    foreach($line in Get-Content .env) {
        if($line -match "^#" -or [string]::IsNullOrWhiteSpace($line)) { continue }
        $split = $line.Split('=', 2)
        if($split.Length -eq 2) {
            [Environment]::SetEnvironmentVariable($split[0].Trim(), $split[1].Trim())
        }
    }
}

if (-not $env:ConnectionStrings__DefaultConnection) {
    Write-Host "ALERTA CRITICO: A variavel ConnectionStrings__DefaultConnection nao foi definida!" -ForegroundColor Red
    Write-Host "Crie um arquivo '.env' na raiz do projeto contendo a configuracao secreta:" -ForegroundColor Yellow
    Write-Host "ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=escola_atenta_db;Username=admin;Password=SUA_SENHA_AQUI" -ForegroundColor Cyan
    Write-Host "A Inicializacao foi abortada preventivamente. Nunca salve senhas no codigo fonte." -ForegroundColor Red
    exit 1
}
Write-Host "[Seguranca] Senhas e Variaveis de Ambiente Injetadas com Sucesso." -ForegroundColor Green

Write-Host ""
# Passo 3: Atualização do Banco de Dados (EF Core Migrations)
Write-Host "[3/4] Atualizando Banco de Dados Local (EF Core)..." -ForegroundColor Yellow
try {
    dotnet ef database update --project src/EscolaAtenta.Infrastructure --startup-project src/EscolaAtenta.API --no-build
    Write-Host "[Banco de Dados] Migracoes aplicadas com sucesso." -ForegroundColor Green
} catch {
    Write-Host "ERRO ao aplicar as migracoes no banco de dados. O Docker (Postgres) esta rodando?" -ForegroundColor Red
    exit 1
}

Write-Host ""
# Passo 4: Subindo a API
Write-Host "[4/4] Iniciando API em ambiente de Producao..." -ForegroundColor Yellow
# O modo de Produção ativa explicitamente o CORS restrito e reduz o ruído de log (esconde StackTraces).
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --project src/EscolaAtenta.API --no-build

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "Servidor Finalizado." -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
