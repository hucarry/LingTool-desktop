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

        $message = @(
            "Frontend dependency installation failed.",
            "",
            "Common causes:",
            "- frontend/node_modules contains files locked by Vite, VS Code, antivirus, or another Node process",
            "- the current shell profile prints unrelated errors before/after npm runs",
            "",
            "Recommended retries:",
            "1. Close running frontend dev servers and editors using this repo",
            "2. Re-run with a clean shell:",
            "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1",
            "3. If dependencies are already installed, skip reinstall:",
            "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoFrontendInstall"
        ) -join [Environment]::NewLine
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
        $message = @(
            "Frontend dependencies look incomplete.",
            "",
            "Missing local tools:",
            ($missing -join "`n"),
            "",
            "You used -NoFrontendInstall, but the frontend toolchain is not fully available.",
            "Re-run one of these commands:",
            "  powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1",
            "  powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoFrontendBuild"
        ) -join [Environment]::NewLine
        throw $message
    }
}
