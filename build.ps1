param(
    [switch]$NoRun,
    [switch]$NoFrontendInstall,
    [switch]$NoHostRegression
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Set-Location $PSScriptRoot

function Run-Step([string]$name, [scriptblock]$action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
}

function Assert-LastExitCode([string]$context) {
    if ($LASTEXITCODE -ne 0) {
        throw "$context failed with exit code $LASTEXITCODE."
    }
}

function Get-NpmExecutable() {
    $cmdCommand = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if ($null -ne $cmdCommand) {
        return $cmdCommand.Source
    }

    $command = Get-Command npm -ErrorAction Stop
    return $command.Source
}

function Invoke-NpmCommand([string[]]$arguments, [string]$context) {
    $npmExe = Get-NpmExecutable
    & $npmExe @arguments
    Assert-LastExitCode $context
}

Push-Location frontend
try {
    if (-not $NoFrontendInstall) {
        $installCommand = if (Test-Path 'package-lock.json') { 'ci' } else { 'install' }
        Run-Step "Install frontend dependencies (npm $installCommand)" {
            Invoke-NpmCommand -arguments @($installCommand) -context "npm $installCommand"
        }
    }

    Run-Step 'Build frontend (npm run build)' {
        Invoke-NpmCommand -arguments @('run', 'build') -context 'npm run build'
    }
}
finally {
    Pop-Location
}

Run-Step 'Build backend (dotnet build)' {
    dotnet build ToolHub.App/ToolHub.App.csproj
    Assert-LastExitCode 'dotnet build'
}

if (-not $NoHostRegression) {
    Run-Step 'Run host regression checks' {
        powershell -NoProfile -ExecutionPolicy Bypass -File .\ToolHub.App.Tests\run-tests.ps1
        Assert-LastExitCode 'host regression checks'
    }
}

if (-not $NoRun) {
    Run-Step 'Launch desktop app' {
        dotnet run --project ToolHub.App/ToolHub.App.csproj
        Assert-LastExitCode 'dotnet run'
    }
}
