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

function Write-Warn([string]$message) {
    Write-Host "[WARN] $message" -ForegroundColor Yellow
}

function Should-FallbackToOfflineHostRegression([string]$message) {
    if ([string]::IsNullOrWhiteSpace($message)) {
        return $false
    }

    return $message -match 'NU1301|project\.assets\.json|Unable to load the service index|repository signatures|restore'
}

function Invoke-HostRegressionChecks() {
    $standardTestProject = Join-Path $PSScriptRoot 'ToolHub.App.Tests\ToolHub.App.Tests.csproj'
    if (Test-Path $standardTestProject) {
        try {
            Run-Step 'Run host regression checks (dotnet test)' {
                dotnet test $standardTestProject -c Debug
                Assert-LastExitCode 'dotnet test host regression checks'
            }
            return
        }
        catch {
            if (-not (Should-FallbackToOfflineHostRegression $_.Exception.Message)) {
                throw
            }

            Write-Warn "dotnet test is unavailable in the current environment. Falling back to the offline host regression runner."
        }
    }

    Run-Step 'Run host regression checks (offline)' {
        powershell -NoProfile -ExecutionPolicy Bypass -File .\ToolHub.App.Tests\run-tests.ps1
        Assert-LastExitCode 'host regression checks'
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
        $installCommand = 'install'
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
    Invoke-HostRegressionChecks
}

if (-not $NoRun) {
    Run-Step 'Launch desktop app' {
        dotnet run --project ToolHub.App/ToolHub.App.csproj
        Assert-LastExitCode 'dotnet run'
    }
}
