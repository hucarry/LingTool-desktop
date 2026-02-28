param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "publish",
    [bool]$BundlePortablePython = $true,
    [bool]$BundlePortablePip = $true,
    [string]$PortablePythonVersion = "3.12.10",
    [string]$PortablePythonZipPath = "",
    [string]$GetPipScriptPath = "",
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
            Invoke-WebRequest -Uri $url -OutFile $resolvedZip
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
        Invoke-WebRequest -Uri $url -OutFile $target
    }

    return $target
}

function Install-PortablePip(
    [string]$pythonDir,
    [string]$getPipScriptPath
) {
    $pythonExe = Join-Path $pythonDir "python.exe"
    if (-not (Test-Path $pythonExe)) {
        throw "python.exe not found in portable runtime: $pythonExe"
    }

    $script = Resolve-GetPipScript $getPipScriptPath
    & $pythonExe $script "--disable-pip-version-check" "--no-warn-script-location"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install pip into portable Python."
    }

    & $pythonExe -m pip --version
    if ($LASTEXITCODE -ne 0) {
        throw "pip verification failed in portable Python."
    }
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

if ($BundlePortablePython) {
    $portablePythonDir = Join-Path $outputDir "python"
    Run-Step "Bundle portable Python (embed $PortablePythonVersion)" {
        Bundle-PortablePython -pythonDir $portablePythonDir -runtime $Runtime -version $PortablePythonVersion -zipPath $PortablePythonZipPath
    }

    if ($BundlePortablePip) {
        Run-Step "Install pip into portable Python" {
            Install-PortablePip -pythonDir $portablePythonDir -getPipScriptPath $GetPipScriptPath
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
