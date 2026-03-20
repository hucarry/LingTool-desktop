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

if ($NoBundlePortablePython) {
    $BundlePortablePython = $false
}

if ($NoBundlePortablePip) {
    $BundlePortablePip = $false
}

if ($BundlePortablePython -and -not [string]::IsNullOrWhiteSpace($PortablePythonZipPath)) {
    Write-Warn "PortablePythonZipPath is ignored. publish.ps1 now bundles a full Python installation directory instead of an embed zip."
}

if ($BundlePortablePython -and $PortablePythonVersion -ne "3.12.10") {
    Write-Warn "PortablePythonVersion is ignored. The bundled Python version now follows the selected full Python source installation."
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

function Should-FallbackToOfflineHostRegression([string]$message) {
    if ([string]::IsNullOrWhiteSpace($message)) {
        return $false
    }

    return $message -match 'NU1301|project\.assets\.json|Unable to load the service index|repository signatures|restore'
}

function Invoke-HostRegressionChecks([string]$projectRoot) {
    $standardTestProject = Join-Path $projectRoot "ToolHub.App.Tests\ToolHub.App.Tests.csproj"
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
        powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "ToolHub.App.Tests\run-tests.ps1")
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

function Test-BenignNpmEpermOutput([string[]]$lines) {
    if ($null -eq $lines) {
        return $false
    }

    [string[]]$normalizedLines = @($lines)
    $joined = [string]::Join("`n", $normalizedLines)
    if ([string]::IsNullOrWhiteSpace($joined)) {
        return $false
    }
    if ($joined -notmatch "tailwindcss-oxide\.win32-x64-msvc\.node\.locktest") {
        return $false
    }

    return $joined -match "npm error code EPERM"
}

function Write-CommandOutput([string[]]$lines) {
    foreach ($line in $lines) {
        if ($null -ne $line) {
            Write-Host $line
        }
    }
}

function Invoke-NpmCommand(
    [string[]]$arguments,
    [string]$context,
    [switch]$SuppressBenignEpermOnSuccess
) {
    $npmExe = Get-NpmExecutable
    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()

    try {
        $process = Start-Process -FilePath $npmExe -ArgumentList $arguments -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath

        $stdoutLines = if (Test-Path $stdoutPath) { @(Get-Content -Path $stdoutPath) } else { @() }
        $stderrLines = if (Test-Path $stderrPath) { @(Get-Content -Path $stderrPath) } else { @() }
        $allLines = @($stdoutLines + $stderrLines)

        $shouldSuppress = $SuppressBenignEpermOnSuccess -and $process.ExitCode -eq 0 -and (Test-BenignNpmEpermOutput $stderrLines)
        if ($shouldSuppress) {
            Write-CommandOutput $stdoutLines
        }
        else {
            Write-CommandOutput $allLines
        }

        $global:LASTEXITCODE = $process.ExitCode
        if ($process.ExitCode -ne 0) {
            throw "$context failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        Remove-Item $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-FrontendInstall([string]$frontendDir) {
    try {
        if (Test-Path (Join-Path $frontendDir "package-lock.json")) {
            Run-Step "Install frontend dependencies (npm ci)" {
                Invoke-NpmCommand -arguments @("ci") -context "npm ci" -SuppressBenignEpermOnSuccess
            }
        }
        else {
            Run-Step "Install frontend dependencies (npm install)" {
                Invoke-NpmCommand -arguments @("install") -context "npm install" -SuppressBenignEpermOnSuccess
            }
        }
    }
    catch {
        $installExitCode = if (Get-Variable LASTEXITCODE -ErrorAction SilentlyContinue) { $LASTEXITCODE } else { -1 }
        $hasLockFile = Test-Path (Join-Path $frontendDir "package-lock.json")
        if ($hasLockFile -and $installExitCode -eq -4048) {
            Write-Warn "npm ci failed with EPERM (-4048). A file in node_modules is likely locked by VS Code, antivirus, or the OS."
            Write-Warn "Falling back to npm install without removing the whole dependency tree."

            Run-Step "Retry frontend dependencies (npm install fallback)" {
                Invoke-NpmCommand -arguments @("install") -context "npm install fallback" -SuppressBenignEpermOnSuccess
            }

            return
        }

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

function Get-FirstCommandOutput([string]$command, [string[]]$arguments) {
    try {
        $lines = & $command @arguments 2>$null
        if ($LASTEXITCODE -ne 0) {
            return ""
        }

        $lastLine = $lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1
        if ($null -eq $lastLine) {
            return ""
        }

        return $lastLine.ToString().Trim()
    }
    catch {
        return ""
    }
}

function Test-FullPythonInstallation([string]$pythonExePath) {
    if ([string]::IsNullOrWhiteSpace($pythonExePath)) {
        return $null
    }

    $normalizedExe = $pythonExePath.Trim().Trim('"')
    if (-not [System.IO.Path]::IsPathRooted($normalizedExe)) {
        return $null
    }

    try {
        $normalizedExe = [System.IO.Path]::GetFullPath($normalizedExe)
    }
    catch {
        return $null
    }

    if (-not (Test-Path $normalizedExe -PathType Leaf)) {
        return $null
    }

    $pythonRoot = Split-Path -Parent $normalizedExe
    $requiredPaths = @(
        (Join-Path $pythonRoot "Lib"),
        (Join-Path $pythonRoot "pythonw.exe")
    )

    if (@($requiredPaths | Where-Object { -not (Test-Path $_) }).Count -gt 0) {
        return $null
    }

    try {
        & $normalizedExe -c "import ensurepip, tkinter, venv; print('ok')" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            return $null
        }
    }
    catch {
        return $null
    }

    return [PSCustomObject]@{
        PythonExe = $normalizedExe
        PythonRoot = $pythonRoot
    }
}

function Add-PythonCandidate(
    [System.Collections.Generic.List[string]]$target,
    [string]$candidate
) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        return
    }

    $normalized = $candidate.Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return
    }

    $target.Add($normalized)
}

function Test-CondaPythonInstallation([string]$pythonExePath) {
    if ([string]::IsNullOrWhiteSpace($pythonExePath)) {
        return $false
    }

    try {
        $normalizedExe = [System.IO.Path]::GetFullPath($pythonExePath.Trim().Trim('"'))
    }
    catch {
        return $false
    }

    if (-not (Test-Path $normalizedExe -PathType Leaf)) {
        return $false
    }

    $pythonRoot = Split-Path -Parent $normalizedExe
    return (Test-Path (Join-Path $pythonRoot "conda-meta")) -or (Test-Path (Join-Path $pythonRoot "_conda.exe"))
}

function Get-RegistryPythonCandidates() {
    $candidates = New-Object System.Collections.Generic.List[string]
    $roots = @(
        "HKCU:\Software\Python\PythonCore",
        "HKLM:\Software\Python\PythonCore",
        "HKLM:\Software\WOW6432Node\Python\PythonCore"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) {
            continue
        }

        try {
            $versions = Get-ChildItem -Path $root -ErrorAction SilentlyContinue
            foreach ($version in $versions) {
                $installPathKey = Join-Path $version.PSPath "InstallPath"
                if (-not (Test-Path $installPathKey)) {
                    continue
                }

                $installPath = (Get-ItemProperty -Path $installPathKey -ErrorAction SilentlyContinue).'(default)'
                if (-not [string]::IsNullOrWhiteSpace($installPath)) {
                    Add-PythonCandidate $candidates (Join-Path $installPath "python.exe")
                }

                $executablePath = (Get-ItemProperty -Path $installPathKey -ErrorAction SilentlyContinue).ExecutablePath
                if (-not [string]::IsNullOrWhiteSpace($executablePath)) {
                    Add-PythonCandidate $candidates $executablePath
                }
            }
        }
        catch {
            # Ignore registry probing errors.
        }
    }

    return @($candidates | Select-Object -Unique)
}

function Get-CommonPythonCandidates() {
    $candidates = New-Object System.Collections.Generic.List[string]

    Add-PythonCandidate $candidates (Get-FirstCommandOutput "where.exe" @("python"))
    Add-PythonCandidate $candidates (Get-FirstCommandOutput "where.exe" @("python3"))

    $roots = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Python"),
        (Join-Path $env:ProgramFiles "Python"),
        (Join-Path ${env:ProgramFiles(x86)} "Python"),
        "C:\Python311",
        "C:\Python312",
        "C:\Python313"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) {
            continue
        }

        if (Test-Path $root -PathType Leaf) {
            Add-PythonCandidate $candidates $root
            continue
        }

        Add-PythonCandidate $candidates (Join-Path $root "python.exe")

        try {
            $subDirs = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue
            foreach ($subDir in $subDirs) {
                Add-PythonCandidate $candidates (Join-Path $subDir.FullName "python.exe")
            }
        }
        catch {
            # Ignore directory enumeration errors.
        }
    }

    return @($candidates | Select-Object -Unique)
}

function Get-CondaPythonCandidates() {
    $candidates = New-Object System.Collections.Generic.List[string]

    Add-PythonCandidate $candidates $env:CONDA_PREFIX

    $roots = @(
        "D:\ProgramData\miniconda3",
        "C:\ProgramData\miniconda3",
        "C:\ProgramData\Anaconda3",
        "D:\ProgramData\Anaconda3"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($root in $roots) {
        if (-not (Test-Path $root -PathType Container)) {
            continue
        }

        Add-PythonCandidate $candidates (Join-Path $root "python.exe")
    }

    return @($candidates | Select-Object -Unique)
}

function Resolve-FullPythonSource([string]$manualPath) {
    $candidatePaths = New-Object System.Collections.Generic.List[string]
    $ignoredCondaCandidates = New-Object System.Collections.Generic.List[string]
    $manualSelection = -not [string]::IsNullOrWhiteSpace($manualPath)

    if ($manualSelection) {
        $resolvedManual = (Resolve-Path $manualPath -ErrorAction Stop).Path
        if (Test-Path $resolvedManual -PathType Container) {
            $candidatePaths.Add((Join-Path $resolvedManual "python.exe"))
        }
        else {
            $candidatePaths.Add($resolvedManual)
        }
    }
    else {
        $pyLauncherPath = Get-FirstCommandOutput "py" @("-3", "-c", "import sys; print(sys.executable)")
        if (-not [string]::IsNullOrWhiteSpace($pyLauncherPath)) {
            $candidatePaths.Add($pyLauncherPath)
        }

        $pythonPath = Get-FirstCommandOutput "python" @("-c", "import sys; print(sys.executable)")
        if (-not [string]::IsNullOrWhiteSpace($pythonPath)) {
            $candidatePaths.Add($pythonPath)
        }

        foreach ($registryCandidate in Get-RegistryPythonCandidates) {
            Add-PythonCandidate $candidatePaths $registryCandidate
        }

        foreach ($commonCandidate in Get-CommonPythonCandidates) {
            Add-PythonCandidate $candidatePaths $commonCandidate
        }

        if ($AllowCondaPythonSource) {
            foreach ($condaCandidate in Get-CondaPythonCandidates) {
                Add-PythonCandidate $candidatePaths $condaCandidate
            }
        }
    }

    $checkedCandidates = @()
    foreach ($candidate in $candidatePaths | Select-Object -Unique) {
        if (-not $manualSelection -and -not $AllowCondaPythonSource -and (Test-CondaPythonInstallation $candidate)) {
            $ignoredCondaCandidates.Add($candidate)
            continue
        }

        $checkedCandidates += $candidate
        $validated = Test-FullPythonInstallation $candidate
        if ($null -ne $validated) {
            return $validated
        }
    }

    $checkedSummary = if ($checkedCandidates.Count -gt 0) {
        ($checkedCandidates | Select-Object -Unique | ForEach-Object { "  - $_" }) -join "`n"
    }
    else {
        "  - (no candidates found)"
    }

    $ignoredCondaSummary = if ($ignoredCondaCandidates.Count -gt 0) {
        ($ignoredCondaCandidates | Select-Object -Unique | ForEach-Object { "  - $_" }) -join "`n"
    }
    else {
        ""
    }

    $message = @"
Failed to locate a usable full Python installation for bundling.

Requirements:
- a real Python installation directory, not an embed package or a venv
- tkinter available
- ensurepip available
- venv available

Checked candidates:
$checkedSummary
"@

    if (-not [string]::IsNullOrWhiteSpace($ignoredCondaSummary)) {
        $message += @"

Ignored conda candidates:
$ignoredCondaSummary
"@
    }

    $message += @"

Recommended retries:
1. Install the official full Python for Windows from python.org
2. Re-run with an explicit source path:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -BundledPythonSourcePath C:\Path\To\Python312
3. If you intentionally want to bundle a conda base environment, allow it explicitly:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -AllowCondaPythonSource
4. If you do not need bundled Python, skip it:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoBundlePortablePython
"@
    throw $message
}

function Invoke-RoboCopy([string]$sourceDir, [string]$destinationDir, [string[]]$extraArgs = @()) {
    & robocopy $sourceDir $destinationDir @extraArgs
    $robocopyExitCode = $LASTEXITCODE
    if ($robocopyExitCode -gt 7) {
        throw "robocopy failed with exit code $robocopyExitCode while copying '$sourceDir' to '$destinationDir'."
    }
}

function Remove-DirectoryRobustly([string]$path) {
    if (-not (Test-Path $path)) {
        return
    }

    try {
        Remove-Item $path -Recurse -Force -ErrorAction Stop
        return
    }
    catch {
        Write-Warn "Standard folder cleanup failed for '$path'. Retrying with cmd rmdir."
    }

    & cmd.exe /d /c "rmdir /s /q `"$path`""
    if (Test-Path $path) {
        throw "Failed to clean output folder: $path"
    }
}

function Copy-FullPythonRuntime(
    [string]$pythonDir,
    [string]$sourcePythonRoot
) {
    if (Test-Path $pythonDir) {
        Remove-Item $pythonDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $pythonDir -Force | Out-Null

    $excludedDirs = @(
        (Join-Path $sourcePythonRoot "Lib\site-packages"),
        (Join-Path $sourcePythonRoot "Scripts"),
        (Join-Path $sourcePythonRoot "__pycache__")
    ) | Where-Object { Test-Path $_ }

    $excludedFiles = @("*.pdb")

    if (Test-Path (Join-Path $sourcePythonRoot "conda-meta")) {
        $excludedDirs += @(
            (Join-Path $sourcePythonRoot "conda-meta"),
            (Join-Path $sourcePythonRoot "condabin"),
            (Join-Path $sourcePythonRoot "envs"),
            (Join-Path $sourcePythonRoot "pkgs"),
            (Join-Path $sourcePythonRoot "Menu"),
            (Join-Path $sourcePythonRoot "shell")
        ) | Where-Object { Test-Path $_ }

        $excludedFiles += @(
            ".condarc",
            ".nonadmin",
            "_conda.exe",
            "cwp.py",
            "pre_uninstall.bat",
            "Uninstall-Miniconda3.exe"
        )
    }

    $robocopyArgs = @(
        "/E",
        "/R:2",
        "/W:1",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS",
        "/NP"
    )

    if ($excludedDirs.Count -gt 0) {
        $robocopyArgs += "/XD"
        $robocopyArgs += $excludedDirs
    }

    if ($excludedFiles.Count -gt 0) {
        $robocopyArgs += "/XF"
        $robocopyArgs += $excludedFiles
    }

    Invoke-RoboCopy -sourceDir $sourcePythonRoot -destinationDir $pythonDir -extraArgs $robocopyArgs

    New-Item -ItemType Directory -Path (Join-Path $pythonDir "Lib\site-packages") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $pythonDir "Scripts") -Force | Out-Null
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
        throw "python.exe not found in bundled runtime: $pythonExe"
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
Bundled Python pip bootstrap failed.

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
Failed to install pip into bundled Python.

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
        throw "pip verification failed in bundled Python."
    }
}

function Ensure-FullPythonPip(
    [string]$pythonDir,
    [string]$getPipScriptPath,
    [string]$pipIndexUrl,
    [string[]]$pipArgs
) {
    $pythonExe = Join-Path $pythonDir "python.exe"
    if (-not (Test-Path $pythonExe)) {
        throw "python.exe not found in bundled runtime: $pythonExe"
    }

    if (Test-PythonModuleCommand -pythonExe $pythonExe -arguments @("-m", "pip", "--version")) {
        return
    }

    Write-Info "pip not found in copied Python runtime. Bootstrapping with ensurepip."

    $ensurepipExitCode = Invoke-PythonCommand -pythonExe $pythonExe -arguments @("-m", "ensurepip", "--upgrade")
    if ($ensurepipExitCode -ne 0) {
        Write-Warn "ensurepip exited with code $ensurepipExitCode. Falling back to get-pip.py."
    }

    if (Test-PythonModuleCommand -pythonExe $pythonExe -arguments @("-m", "pip", "--version")) {
        return
    }

    Install-PortablePip -pythonDir $pythonDir -getPipScriptPath $getPipScriptPath -pipIndexUrl $pipIndexUrl -pipArgs $pipArgs
}

function Invoke-PythonCommand([string]$pythonExe, [string[]]$arguments) {
    $hasNativePreference = $null -ne (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue)
    if ($hasNativePreference) {
        $previousPreference = $script:PSNativeCommandUseErrorActionPreference
        $script:PSNativeCommandUseErrorActionPreference = $false
    }

    try {
        & $pythonExe @arguments *> $null
        return $LASTEXITCODE
    }
    catch {
        return 1
    }
    finally {
        if ($hasNativePreference) {
            $script:PSNativeCommandUseErrorActionPreference = $previousPreference
        }
    }
}

function Test-PythonModuleCommand([string]$pythonExe, [string[]]$arguments) {
    $hasNativePreference = $null -ne (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue)
    if ($hasNativePreference) {
        $previousPreference = $script:PSNativeCommandUseErrorActionPreference
        $script:PSNativeCommandUseErrorActionPreference = $false
    }

    try {
        & $pythonExe @arguments *> $null
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
    finally {
        if ($hasNativePreference) {
            $script:PSNativeCommandUseErrorActionPreference = $previousPreference
        }
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

Write-Host ""
Write-Host "Publish completed." -ForegroundColor Green
Write-Host "Output: $outputDir"
Write-Host ""
Write-Host "Distribute the entire folder (exe + deps)." -ForegroundColor Yellow

if ($OpenOutput) {
    Start-Process explorer.exe $outputDir
}
