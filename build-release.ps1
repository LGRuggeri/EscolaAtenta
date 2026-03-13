param (
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$DownloadUrl = "https://example.com/releases/update.zip"
)

$ErrorActionPreference = "Stop"

$solutionDir = $PSScriptRoot
$zipPath = Join-Path $solutionDir "update.zip"
$versionJsonPath = Join-Path $solutionDir "version.json"

# Caminhos de publicação (devem coincidir com os PublishProfiles e o .iss)
$apiPublishDir = Join-Path $solutionDir "src\EscolaAtenta.API\bin\Publish\win-x64"
$trayPublishDir = Join-Path $solutionDir "src\EscolaAtenta.TrayMonitor\bin\Publish\win-x64"

Write-Host "Iniciando build do release versao $Version..." -ForegroundColor Cyan

# Limpar builds anteriores
if (Test-Path $apiPublishDir) { Remove-Item -Recurse -Force $apiPublishDir }
if (Test-Path $trayPublishDir) { Remove-Item -Recurse -Force $trayPublishDir }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

# Build da API (Self-Contained, usando PublishProfile)
Write-Host "Compilando EscolaAtenta.API (self-contained)..."
dotnet publish "$solutionDir\src\EscolaAtenta.API\EscolaAtenta.API.csproj" -p:PublishProfile=win-x64-selfcontained
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar a API" }

# Build do TrayMonitor (Self-Contained, usando PublishProfile)
Write-Host "Compilando EscolaAtenta.TrayMonitor (self-contained)..."
dotnet publish "$solutionDir\src\EscolaAtenta.TrayMonitor\EscolaAtenta.TrayMonitor.csproj" -p:PublishProfile=win-x64-selfcontained
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o TrayMonitor" }

# LIMPEZA DE FICHEIROS SENSIVEIS
Write-Host "Removendo definicoes de desenvolvimento e base de dados..." -ForegroundColor Yellow

foreach ($dir in @($apiPublishDir, $trayPublishDir)) {
    # Dev settings
    $devConfig = Join-Path $dir "appsettings.Development.json"
    if (Test-Path $devConfig) { Remove-Item -Force $devConfig }

    # Eliminar qualquer rasto de banco de dados SQLite
    Get-ChildItem -Path $dir -Filter "*.db*" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
}

# Empacotar ZIP (ambos os projetos)
Write-Host "Gerando pacote update.zip..."
$tempPackDir = Join-Path $solutionDir "publish-package"
if (Test-Path $tempPackDir) { Remove-Item -Recurse -Force $tempPackDir }
New-Item -ItemType Directory -Path "$tempPackDir\API" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPackDir\TrayMonitor" -Force | Out-Null
Copy-Item -Path "$apiPublishDir\*" -Destination "$tempPackDir\API" -Recurse
Copy-Item -Path "$trayPublishDir\*" -Destination "$tempPackDir\TrayMonitor" -Recurse
Compress-Archive -Path "$tempPackDir\*" -DestinationPath $zipPath -Force
Remove-Item -Recurse -Force $tempPackDir

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
Write-Host "`nPara gerar o instalador, execute:"
Write-Host "  iscc.exe escolaatenta-installer.iss" -ForegroundColor Yellow
Write-Host "`nLembre-se de fazer upload do update.zip para a nuvem e atualizar o downloadUrl no version.json"
