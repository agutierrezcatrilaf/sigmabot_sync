# Genera release del configurador: SigmabotConfig.Api + Angular estático.
# Repo: SigmabotSync.Api (raíz = $RepoRoot). Angular hermano: ..\SigmabotConfig
# Ejemplo:
#   .\Scripts\Build-ConfiguradorRelease.ps1 -ServerHost "192.168.1.50" -ApiPort 5154 -WebPort 8080
#
# appsettings.Production.json se genera en publish con:
#   - ConnectionString: -ConnectionString, appsettings.Development.json, o settings.json del worker (..\SigmabotSync.Worker)
#   - Cors: origen web del servidor + http://localhost:<WebPort>

param(
    [Parameter(Mandatory = $true)]
    [string] $ServerHost,
    [int] $ApiPort = 5154,
    [int] $WebPort = 8080,
    [string] $ConnectionString = "",
    [string] $RepoRoot = "",
    [string] $AngularRoot = "",
    [switch] $SkipZip,
    [switch] $UseUrlRewrite
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $scriptDir = $PSScriptRoot
    if (-not $scriptDir) {
        $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    if (-not $scriptDir) {
        throw "No se pudo determinar la carpeta del repo. Ejecute con -RepoRoot."
    }
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
}

if (-not $AngularRoot) {
    $AngularRoot = Join-Path (Split-Path $RepoRoot -Parent) "SigmabotConfig"
}

function Resolve-ConnectionString {
    param([string] $Explicit, [string] $Root)

    if ($Explicit.Trim()) { return $Explicit.Trim() }

    $devApi = Join-Path $Root "SigmabotConfig.Api\appsettings.Development.json"
    if (Test-Path $devApi) {
        $cfg = Get-Content $devApi -Raw | ConvertFrom-Json
        $cs = $cfg.Database.ConnectionString
        if ($cs -and $cs.Trim()) {
            Write-Host "ConnectionString: appsettings.Development.json"
            return $cs.Trim()
        }
    }

    $siblingRoot = Split-Path $Root -Parent
    $workerSettings = Join-Path $siblingRoot "SigmabotSync.Worker\SigmabotSync.Console\settings.json"
    if (Test-Path $workerSettings) {
        $cfg = Get-Content $workerSettings -Raw | ConvertFrom-Json
        $cs = $cfg.DatabaseConnectionString
        if ($cs -and $cs.Trim()) {
            Write-Host "ConnectionString: SigmabotSync.Worker\SigmabotSync.Console\settings.json"
            return $cs.Trim()
        }
    }

    $legacyConsoleSettings = Join-Path $Root "SigmabotSync.Console\settings.json"
    if (Test-Path $legacyConsoleSettings) {
        $cfg = Get-Content $legacyConsoleSettings -Raw | ConvertFrom-Json
        $cs = $cfg.DatabaseConnectionString
        if ($cs -and $cs.Trim()) {
            Write-Host "ConnectionString: SigmabotSync.Console\settings.json (legacy monorepo)"
            return $cs.Trim()
        }
    }

    return $null
}

function Ensure-TrustServerCertificate {
    param([string] $Cs)
    if ($Cs -match 'TrustServerCertificate\s*=') { return $Cs }
    if ($Cs.EndsWith(';')) { return "${Cs}TrustServerCertificate=True;" }
    return "${Cs};TrustServerCertificate=True;"
}

$apiUrl = "http://${ServerHost}:${ApiPort}"
$webUrl = "http://${ServerHost}:${WebPort}"
$publishRoot = Join-Path $RepoRoot "publish\configurador"
$apiOut = Join-Path $publishRoot "api"
$webOut = Join-Path $publishRoot "web"

Write-Host "Servidor: $ServerHost"
Write-Host "API:      $apiUrl"
Write-Host "Web:      $webUrl"

if (-not (Test-Path $AngularRoot)) {
    throw "No se encontró SigmabotConfig en: $AngularRoot"
}

Push-Location $AngularRoot
try {
    if (-not (Test-Path "node_modules")) { npm ci }
    npm run build
}
finally {
    Pop-Location
}

# API publish
if (Test-Path $apiOut) { Remove-Item $apiOut -Recurse -Force }
dotnet publish (Join-Path $RepoRoot "SigmabotConfig.Api\SigmabotConfig.Api.csproj") -c Release -o $apiOut

# Copiar front estático (Angular 19: dist/.../browser)
$distBrowser = Join-Path $AngularRoot "dist\sigmabot-config\browser"
if (-not (Test-Path $distBrowser)) {
    throw "No se encontró build Angular en: $distBrowser"
}
if (Test-Path $webOut) { Remove-Item $webOut -Recurse -Force }
Copy-Item $distBrowser $webOut -Recurse

# web.config para IIS (sin URL Rewrite por defecto; ver -UseUrlRewrite)
$dep = Join-Path $RepoRoot "deployment\configurador"
if ($UseUrlRewrite) {
    Copy-Item (Join-Path $dep "web.config") (Join-Path $webOut "web.config") -Force
    Copy-Item (Join-Path $dep "web.config.sin-rewrite") (Join-Path $webOut "web.config.sin-rewrite") -Force
} else {
    Copy-Item (Join-Path $dep "web.config.sin-rewrite") (Join-Path $webOut "web.config") -Force
    Copy-Item (Join-Path $dep "web.config") (Join-Path $webOut "web.config.con-rewrite") -Force
}

# config.json: única fuente de URL del backend (editar en servidor si hace falta)
@"

{
  `"apiBaseUrl`": `"$apiUrl`"
}
"@ | Set-Content -Path (Join-Path $webOut "config.json") -Encoding UTF8
Copy-Item (Join-Path $RepoRoot "deployment\configurador\config.json.example") (Join-Path $webOut "config.json.example") -Force

# appsettings.Production.json listo para el servidor (Cors + BD)
$resolvedCs = Resolve-ConnectionString -Explicit $ConnectionString -Root $RepoRoot
Copy-Item (Join-Path $dep "appsettings.Production.json.example") (Join-Path $apiOut "appsettings.Production.json.example") -Force
if ($resolvedCs) {
    $resolvedCs = Ensure-TrustServerCertificate $resolvedCs
    $productionSettings = @{
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        Database = @{
            ConnectionString = $resolvedCs
        }
        Cors = @{
            AllowedOrigins = @($webUrl, "http://localhost:${WebPort}")
        }
        OnDemandExecution = @{
            Enabled = $false
            WorkerExePath = "C:\ProgramData\Sigmatec\Salfa\worker\SigmabotSync.Console.exe"
        }
    }
    $productionSettings | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $apiOut "appsettings.Production.json") -Encoding UTF8
    Write-Host "Generado: api\appsettings.Production.json (Cors + ConnectionString)"
} else {
    Write-Warning "No se encontró ConnectionString. Solo se copió appsettings.Production.json.example; renómbrelo y complete la BD en el servidor."
}

Copy-Item (Join-Path $dep "run-api.bat") (Join-Path $apiOut "run-api.bat") -Force
Copy-Item (Join-Path $dep "install-api-task.bat") (Join-Path $apiOut "install-api-task.bat") -Force
Copy-Item (Join-Path $dep "LEEME-SERVIDOR.md") (Join-Path $publishRoot "LEEME-SERVIDOR.md") -Force

if (-not $SkipZip) {
    $zipPath = Join-Path $RepoRoot "publish\SigmabotConfig-Servidor.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $zipPath -Force
    Write-Host "ZIP: $zipPath"
}

Write-Host ""
Write-Host "Listo:"
Write-Host "  API:  $apiOut"
Write-Host "  Web:  $webOut"
Write-Host "Siguiente: copiar publish\configurador al servidor y seguir LEEME-SERVIDOR.md"
