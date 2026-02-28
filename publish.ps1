param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "publish",
    [switch]$NoFrontendInstall,
    [switch]$NoFrontendBuild,
    [switch]$NoClean,
    [switch]$OpenOutput
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontendDir = Join-Path $projectRoot "frontend"
$backendProject = Join-Path $projectRoot "ToolHub.App/ToolHub.App.csproj"
$outputDir = Join-Path $projectRoot (Join-Path $OutputRoot $Runtime)

function Run-Step([string]$name, [scriptblock]$action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
}

Set-Location $projectRoot

if (-not $NoFrontendBuild) {
    Push-Location $frontendDir
    try {
        if (-not $NoFrontendInstall) {
            if (Test-Path (Join-Path $frontendDir "package-lock.json")) {
                Run-Step "Install frontend dependencies (npm ci)" { npm ci }
            }
            else {
                Run-Step "Install frontend dependencies (npm install)" { npm install }
            }
        }

        Run-Step "Build frontend (npm run build)" { npm run build }
    }
    finally {
        Pop-Location
    }
}

if ((Test-Path $outputDir) -and (-not $NoClean)) {
    Run-Step "Clean output folder: $outputDir" { Remove-Item $outputDir -Recurse -Force }
}

$publishArgs = @(
    "publish", $backendProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $outputDir,
    "-p:SelfContained=true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:IncludeAllContentForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true"
)

Run-Step "Publish backend (dotnet publish)" { dotnet @publishArgs }

Write-Host ""
Write-Host "Publish completed." -ForegroundColor Green
Write-Host "Output: $outputDir"
Write-Host ""
Write-Host "Distribute the entire folder (exe + deps)." -ForegroundColor Yellow

if ($OpenOutput) {
    Start-Process explorer.exe $outputDir
}

