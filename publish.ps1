param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "publish",
    [string]$BundledPythonSourcePath = "",
    [switch]$AllowCondaPythonSource,
    [bool]$BundlePortablePython = $true,
    [bool]$BundlePortablePip = $true,
    [string]$PortablePythonVersion = "3.12.10",
    [string]$PortablePythonZipPath = "",
    [string]$GetPipScriptPath = "",
    [string]$PortablePipIndexUrl = "",
    [string[]]$PortablePipArgs = @(),
    [switch]$NoBundlePortablePython,
    [switch]$NoBundlePortablePip,
    [switch]$NoFrontendInstall,
    [switch]$NoFrontendBuild,
    [switch]$NoHostRegression,
    [switch]$NoClean,
    [switch]$OpenOutput
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontendDir = Join-Path $projectRoot "frontend"
$backendProject = Join-Path $projectRoot "ToolHub.App/ToolHub.App.csproj"
$outputDir = Join-Path $projectRoot (Join-Path $OutputRoot $Runtime)
$publishScriptRoot = Join-Path $projectRoot "scripts\publish"

. (Join-Path $publishScriptRoot "Common.ps1")
. (Join-Path $publishScriptRoot "Frontend.ps1")
. (Join-Path $publishScriptRoot "PythonRuntime.ps1")
. (Join-Path $publishScriptRoot "Policy.ps1")

if ($NoBundlePortablePython) {
    $BundlePortablePython = $false
}

if ($NoBundlePortablePip) {
    $BundlePortablePip = $false
}

if ($Runtime -notlike "win-*") {
    throw "publish.ps1 only supports Windows runtimes. Use win-x64, win-x86, or another win-* RID."
}

if (-not $BundlePortablePython -and $BundlePortablePip) {
    Write-Warn "BundlePortablePip requires BundlePortablePython. Portable pip bundling will be skipped."
    $BundlePortablePip = $false
}

if ($BundlePortablePython -and -not [string]::IsNullOrWhiteSpace($PortablePythonZipPath)) {
    Write-Warn "PortablePythonZipPath is ignored. publish.ps1 now bundles a full Python installation directory instead of an embed zip."
}

if ($BundlePortablePython -and $PortablePythonVersion -ne "3.12.10") {
    Write-Warn "PortablePythonVersion is ignored. The bundled Python version now follows the selected full Python source installation."
}

if (-not $BundlePortablePython -and -not [string]::IsNullOrWhiteSpace($BundledPythonSourcePath)) {
    Write-Warn "BundledPythonSourcePath is ignored because portable Python bundling is disabled."
}

if (-not $BundlePortablePython -and $AllowCondaPythonSource) {
    Write-Warn "AllowCondaPythonSource has no effect because portable Python bundling is disabled."
}

if (-not $BundlePortablePip -and -not [string]::IsNullOrWhiteSpace($GetPipScriptPath)) {
    Write-Warn "GetPipScriptPath is ignored because bundled pip installation is disabled."
}

if (-not $BundlePortablePip -and -not [string]::IsNullOrWhiteSpace($PortablePipIndexUrl)) {
    Write-Warn "PortablePipIndexUrl is ignored because bundled pip installation is disabled."
}

if (-not $BundlePortablePip -and $PortablePipArgs.Count -gt 0) {
    Write-Warn "PortablePipArgs are ignored because bundled pip installation is disabled."
}

Set-Location $projectRoot

Write-Info "Project root: $projectRoot"
Write-Info "Output directory: $outputDir"
if ($env:CONDA_PREFIX) {
    Write-Warn "Conda environment detected: $($env:CONDA_PREFIX)"
    Write-Warn "If you see unrelated PowerShell/conda Unicode errors, re-run this script with -NoProfile."
}

Write-PublishPolicySummary -bundlePortablePython:$BundlePortablePython -bundlePortablePip:$BundlePortablePip

if (-not $NoFrontendBuild) {
    Push-Location $frontendDir
    try {
        if (-not $NoFrontendInstall) {
            Invoke-FrontendInstall $frontendDir
        }
        else {
            Assert-FrontendDependenciesAvailable $frontendDir
        }

        Run-Step "Build frontend (npm run build)" {
            Invoke-NpmCommand -arguments @("run", "build") -context "npm run build"
        }
    }
    finally {
        Pop-Location
    }
}

Run-Step "Build backend for host regression (dotnet build Debug)" {
    dotnet build $backendProject -c Debug
    Assert-LastExitCode "dotnet build Debug"
}

if (-not $NoHostRegression) {
    Invoke-HostRegressionChecks -projectRoot $projectRoot
}

if ((Test-Path $outputDir) -and (-not $NoClean)) {
    Run-Step "Clean output folder: $outputDir" { Remove-DirectoryRobustly $outputDir }
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

Run-Step "Publish backend (dotnet publish)" {
    dotnet @publishArgs
    Assert-LastExitCode "dotnet publish"
}

if ($BundlePortablePython) {
    $portablePythonDir = Join-Path $outputDir "python"
    $bundledPython = Resolve-FullPythonSource $BundledPythonSourcePath
    Write-Info "Bundled Python source: $($bundledPython.PythonExe)"

    Run-Step "Bundle full Python runtime" {
        Copy-FullPythonRuntime -pythonDir $portablePythonDir -sourcePythonRoot $bundledPython.PythonRoot
    }

    if ($BundlePortablePip) {
        Run-Step "Ensure pip in bundled Python" {
            Ensure-FullPythonPip -pythonDir $portablePythonDir -getPipScriptPath $GetPipScriptPath -pipIndexUrl $PortablePipIndexUrl -pipArgs $PortablePipArgs
        }
    }
}

$bundledPythonSourceExe = if ($BundlePortablePython) { $bundledPython.PythonExe } else { "" }
Write-ReleaseManifest `
    -outputDir $outputDir `
    -runtime $Runtime `
    -configuration $Configuration `
    -bundlePortablePython:$BundlePortablePython `
    -bundlePortablePip:$BundlePortablePip `
    -pythonSourceExe $bundledPythonSourceExe

Write-Host ""
Write-Host "Publish completed." -ForegroundColor Green
Write-Host "Output: $outputDir"
Write-Host ""
Write-Host "Distribute the entire folder (exe + deps)." -ForegroundColor Yellow
Write-Host "See release-manifest.txt for the packaged runtime policy." -ForegroundColor Yellow

if ($OpenOutput) {
    Start-Process explorer.exe $outputDir
}
