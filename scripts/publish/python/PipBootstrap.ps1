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
            $message = @(
                "Failed to download get-pip.py from:",
                "  $url",
                "",
                "Recommended retries:",
                "1. Re-run in a clean shell:",
                "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1",
                "2. Download get-pip.py manually and pass:",
                "   -GetPipScriptPath C:\path\to\get-pip.py",
                "3. If you can publish without bundled pip, skip it:",
                "   -NoBundlePortablePip"
            ) -join [Environment]::NewLine
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
        $message = @(
            "Bundled Python pip bootstrap failed.",
            "",
            "Recommended retries:",
            "1. Re-run in a clean shell:",
            "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1",
            "2. If your network to PyPI is unstable, pass a mirror explicitly:",
            "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -PortablePipIndexUrl https://pypi.tuna.tsinghua.edu.cn/simple",
            "3. If you only need the desktop app and can skip packaged pip:",
            "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoBundlePortablePip"
        ) -join [Environment]::NewLine
        throw "$message`n$($_.Exception.Message)"
    }

    if ($LASTEXITCODE -ne 0) {
        $mirrorHint = ""
        if ($pipArgs.Count -eq 0) {
            $mirrorHint = @(
                "2. If your network to PyPI is unstable, retry with a mirror:",
                "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -PortablePipIndexUrl https://pypi.tuna.tsinghua.edu.cn/simple"
            ) -join [Environment]::NewLine
        }

        $messageLines = @(
            "Failed to install pip into bundled Python.",
            "",
            "Recommended retries:",
            "1. Re-run in a clean shell:",
            "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1"
        )
        if (-not [string]::IsNullOrWhiteSpace($mirrorHint)) {
            $messageLines += $mirrorHint
        }
        $messageLines += @(
            "3. If you only need the desktop app and can skip packaged pip:",
            "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoBundlePortablePip"
        )
        $message = $messageLines -join [Environment]::NewLine
        throw $message
    }

    & $pythonExe -m pip --version
    if ($LASTEXITCODE -ne 0) {
        throw "pip verification failed in bundled Python."
    }
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
