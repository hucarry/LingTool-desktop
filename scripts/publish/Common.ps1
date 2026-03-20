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

function Write-Policy([string]$message) {
    Write-Host "[POLICY] $message" -ForegroundColor Magenta
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
