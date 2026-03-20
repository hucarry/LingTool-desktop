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

    $messageLines = @(
        "Failed to locate a usable full Python installation for bundling.",
        "",
        "Requirements:",
        "- a real Python installation directory, not an embed package or a venv",
        "- tkinter available",
        "- ensurepip available",
        "- venv available",
        "",
        "Checked candidates:",
        $checkedSummary
    )

    if (-not [string]::IsNullOrWhiteSpace($ignoredCondaSummary)) {
        $messageLines += @(
            "",
            "Ignored conda candidates:",
            $ignoredCondaSummary
        )
    }

    $messageLines += @(
        "",
        "Recommended retries:",
        "1. Install the official full Python for Windows from python.org",
        "2. Re-run with an explicit source path:",
        "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -BundledPythonSourcePath C:\Path\To\Python312",
        "3. If you intentionally want to bundle a conda base environment, allow it explicitly:",
        "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -AllowCondaPythonSource",
        "4. If you do not need bundled Python, skip it:",
        "   powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1 -NoBundlePortablePython"
    )
    $message = $messageLines -join [Environment]::NewLine
    throw $message
}
