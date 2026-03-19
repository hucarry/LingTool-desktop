param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "publish",
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
    [switch]$NoClean,
    [switch]$OpenOutput
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($NoBundlePortablePython) {
    $BundlePortablePython = $false
}

if ($NoBundlePortablePip) {
    $BundlePortablePip = $false
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontendDir = Join-Path $projectRoot "frontend"
$backendProject = Join-Path $projectRoot "ToolHub.App/ToolHub.App.csproj"
$outputDir = Join-Path $projectRoot (Join-Path $OutputRoot $Runtime)

function Run-Step([string]$name, [scriptblock]$action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
}

function Write-Info([string]$message) {
    Write-Host "[INFO] $message" -ForegroundColor DarkGray
}

function Write-Warn([string]$message) {
    Write-Host "[WARN] $message" -ForegroundColor Yellow
}

function Assert-LastExitCode([string]$context) {
    if ($LASTEXITCODE -ne 0) {
        throw "$context failed with exit code $LASTEXITCODE."
    }
}

function Invoke-FrontendInstall([string]$frontendDir) {
    try {
        if (Test-Path (Join-Path $frontendDir "package-lock.json")) {
            Run-Step "Install frontend dependencies (npm ci)" {
                npm ci
                Assert-LastExitCode "npm ci"
            }
        }
        else {
            Run-Step "Install frontend dependencies (npm install)" {
                npm install
                Assert-LastExitCode "npm install"
            }
        }
    }
    catch {
        $message = @"
Frontend dependency installation failed.

Common causes:
- frontend/node_modules contains files locked by Vite, VS Code, antivirus, or another Node process
- the current shell profile prints unrelated errors before/after npm runs

Recommended retries:
1. Close running frontend dev servers and editors using this repo
2. Re-run with a clean shell:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
3. If dependencies are already installed, skip reinstall:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoFrontendInstall
"@
        throw "$message`n$($_.Exception.Message)"
    }
}

function Assert-FrontendDependenciesAvailable([string]$frontendDir) {
    $requiredBins = @(
        (Join-Path $frontendDir "node_modules\.bin\vue-tsc.cmd"),
        (Join-Path $frontendDir "node_modules\.bin\vite.cmd")
    )

    $missing = @($requiredBins | Where-Object { -not (Test-Path $_) })
    if ($missing.Count -gt 0) {
        $message = @"
Frontend dependencies look incomplete.

Missing local tools:
$($missing -join "`n")

You used -NoFrontendInstall, but the frontend toolchain is not fully available.
Re-run one of these commands:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoFrontendBuild
"@
        throw $message
    }
}

function Get-PortablePythonArch([string]$runtime) {
    $normalized = $runtime.Trim().ToLowerInvariant()
    if ($normalized.Contains("arm64")) {
        return "arm64"
    }

    if ($normalized.Contains("x86")) {
        return "win32"
    }

    return "amd64"
}

function Enable-PortablePythonSiteImport([string]$pythonDir) {
    $pthFile = Get-ChildItem -Path $pythonDir -Filter "python*._pth" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $pthFile) {
        return
    }

    $lines = Get-Content -Path $pthFile.FullName
    $updated = @()
    foreach ($line in $lines) {
        $normalizedLine = $line.TrimStart([char]0xFEFF)
        if ($normalizedLine -match "^\s*#\s*import site\s*$") {
            $updated += "import site"
        }
        else {
            $updated += $normalizedLine
        }
    }

    if (-not ($updated -contains "Lib\site-packages")) {
        $updated += "Lib\site-packages"
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($pthFile.FullName, $updated, $utf8NoBom)
    New-Item -ItemType Directory -Path (Join-Path $pythonDir "Lib\site-packages") -Force | Out-Null
}

function Bundle-PortablePython(
    [string]$pythonDir,
    [string]$runtime,
    [string]$version,
    [string]$zipPath
) {
    $arch = Get-PortablePythonArch $runtime

    if (Test-Path $pythonDir) {
        Remove-Item $pythonDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $pythonDir -Force | Out-Null

    $resolvedZip = ""
    if (-not [string]::IsNullOrWhiteSpace($zipPath)) {
        $resolvedZip = (Resolve-Path $zipPath).Path
    }
    else {
        $cacheDir = Join-Path $projectRoot ".tmp_build\python-cache"
        New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null

        $fileName = "python-$version-embed-$arch.zip"
        $resolvedZip = Join-Path $cacheDir $fileName
        $url = "https://www.python.org/ftp/python/$version/$fileName"

        if (-not (Test-Path $resolvedZip)) {
            Write-Host "Downloading portable Python: $url" -ForegroundColor DarkGray
            try {
                Invoke-WebRequest -Uri $url -OutFile $resolvedZip
            }
            catch {
                $message = @"
Failed to download portable Python from:
  $url

Recommended retries:
1. Re-run in a clean shell:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
2. If your network is unstable, download the embed package manually and pass:
   -PortablePythonZipPath C:\path\to\python-$version-embed-$arch.zip
3. If you do not need bundled Python, skip it:
   -NoBundlePortablePython
"@
                throw "$message`n$($_.Exception.Message)"
            }
        }
    }

    Expand-Archive -Path $resolvedZip -DestinationPath $pythonDir -Force
    Enable-PortablePythonSiteImport $pythonDir
}

function Resolve-GetPipScript([string]$manualPath) {
    if (-not [string]::IsNullOrWhiteSpace($manualPath)) {
        return (Resolve-Path $manualPath).Path
    }

    $cacheDir = Join-Path $projectRoot ".tmp_build\python-cache"
    New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null

    $target = Join-Path $cacheDir "get-pip.py"
    if (-not (Test-Path $target)) {
        $url = "https://bootstrap.pypa.io/get-pip.py"
        Write-Host "Downloading get-pip.py: $url" -ForegroundColor DarkGray
        try {
            Invoke-WebRequest -Uri $url -OutFile $target
        }
        catch {
            $message = @"
Failed to download get-pip.py from:
  $url

Recommended retries:
1. Re-run in a clean shell:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
2. Download get-pip.py manually and pass:
   -GetPipScriptPath C:\path\to\get-pip.py
3. If you can publish without bundled pip, skip it:
   -NoBundlePortablePip
"@
            throw "$message`n$($_.Exception.Message)"
        }
    }

    return $target
}

function Install-PortablePip(
    [string]$pythonDir,
    [string]$getPipScriptPath,
    [string]$pipIndexUrl,
    [string[]]$pipArgs
) {
    $pythonExe = Join-Path $pythonDir "python.exe"
    if (-not (Test-Path $pythonExe)) {
        throw "python.exe not found in portable runtime: $pythonExe"
    }

    $script = Resolve-GetPipScript $getPipScriptPath
    $commandArgs = @(
        $script,
        "--disable-pip-version-check",
        "--no-warn-script-location"
    )

    $pipArgs = @($pipArgs)

    if (-not [string]::IsNullOrWhiteSpace($pipIndexUrl)) {
        $commandArgs += @("--index-url", $pipIndexUrl.Trim())
    }

    if ($pipArgs.Count -gt 0) {
        $commandArgs += $pipArgs
    }

    try {
        & $pythonExe @commandArgs
    }
    catch {
        $message = @"
Portable pip bootstrap failed.

Recommended retries:
1. Re-run in a clean shell:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
2. If your network to PyPI is unstable, pass a mirror explicitly:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -PortablePipIndexUrl https://pypi.tuna.tsinghua.edu.cn/simple
3. If you only need the desktop app and can skip packaged pip:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoBundlePortablePip
"@
        throw "$message`n$($_.Exception.Message)"
    }

    if ($LASTEXITCODE -ne 0) {
        $mirrorHint = ""
        if ($pipArgs.Count -eq 0) {
            $mirrorHint = @"
2. If your network to PyPI is unstable, retry with a mirror:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -PortablePipIndexUrl https://pypi.tuna.tsinghua.edu.cn/simple
"@
        }

        $message = @"
Failed to install pip into portable Python.

Recommended retries:
1. Re-run in a clean shell:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
$mirrorHint
3. If you only need the desktop app and can skip packaged pip:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoBundlePortablePip
"@
        throw $message
    }

    & $pythonExe -m pip --version
    if ($LASTEXITCODE -ne 0) {
        throw "pip verification failed in portable Python."
    }
}

Set-Location $projectRoot

Write-Info "Project root: $projectRoot"
Write-Info "Output directory: $outputDir"
if ($env:CONDA_PREFIX) {
    Write-Warn "Conda environment detected: $($env:CONDA_PREFIX)"
    Write-Warn "If you see unrelated PowerShell/conda Unicode errors, re-run this script with -NoProfile."
}

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
            npm run build
            Assert-LastExitCode "npm run build"
        }
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

Run-Step "Publish backend (dotnet publish)" {
    dotnet @publishArgs
    Assert-LastExitCode "dotnet publish"
}

if ($BundlePortablePython) {
    $portablePythonDir = Join-Path $outputDir "python"
    Run-Step "Bundle portable Python (embed $PortablePythonVersion)" {
        Bundle-PortablePython -pythonDir $portablePythonDir -runtime $Runtime -version $PortablePythonVersion -zipPath $PortablePythonZipPath
    }

    if ($BundlePortablePip) {
        Run-Step "Install pip into portable Python" {
            Install-PortablePip -pythonDir $portablePythonDir -getPipScriptPath $GetPipScriptPath -pipIndexUrl $PortablePipIndexUrl -pipArgs $PortablePipArgs
        }
    }
}

Write-Host ""
Write-Host "Publish completed." -ForegroundColor Green
Write-Host "Output: $outputDir"
Write-Host ""
Write-Host "Distribute the entire folder (exe + deps)." -ForegroundColor Yellow

if ($OpenOutput) {
    Start-Process explorer.exe $outputDir
}
