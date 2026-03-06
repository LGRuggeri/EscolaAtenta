param (
    [Parameter(Mandatory = $true)]
    [string]$Version,
    
    [string]$DownloadUrl = "https://example.com/releases/update.zip"
)

$ErrorActionPreference = "Stop"

$solutionDir = $PSScriptRoot
$publishDir = Join-Path $solutionDir "publish"
$zipPath = Join-Path $solutionDir "update.zip"
$versionJsonPath = Join-Path $solutionDir "version.json"

Write-Host "Iniciando build do release versão $Version..." -ForegroundColor Cyan

# Limpar build anterior
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

# Build da API (Pode não ser self-contained por default, mas é o garantido)
Write-Host "Compilando EscolaAtenta.API..."
dotnet publish "$solutionDir\src\EscolaAtenta.API\EscolaAtenta.API.csproj" -c Release -o "$publishDir"

# Build do TrayMonitor (Windows-specific)
Write-Host "Compilando EscolaAtenta.TrayMonitor..."
dotnet publish "$solutionDir\src\EscolaAtenta.TrayMonitor\EscolaAtenta.TrayMonitor.csproj" -c Release -o "$publishDir" -r win-x64 --self-contained false

# LIMPEZA CRÍTICA DE FICHEIROS SENSÍVEIS
Write-Host "Removendo definições de desenvolvimento e base de dados..." -ForegroundColor Yellow

# Dev settings
$devConfig = Join-Path $publishDir "appsettings.Development.json"
if (Test-Path $devConfig) { Remove-Item -Force $devConfig }

# Eliminar qualquer rasto de banco de dados SQLite que possa estar na pasta de build
Get-ChildItem -Path $publishDir -Filter "*.db*" -Recurse | Remove-Item -Force

# Empacotar ZIP
Write-Host "Gerando pacote update.zip..."
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

# Gerar version.json
$versionData = @{
    version     = $Version
    downloadUrl = $DownloadUrl
}

$versionData | ConvertTo-Json | Set-Content -Path $versionJsonPath -Encoding utf8

Write-Host "Release gerado com sucesso!" -ForegroundColor Green
Write-Host "Ficheiros criados:"
Write-Host "- $zipPath"
Write-Host "- $versionJsonPath"
Write-Host "`nLembre-se de fazer upload do update.zip para a nuvem e atualizar o downloadUrl no version.json"
