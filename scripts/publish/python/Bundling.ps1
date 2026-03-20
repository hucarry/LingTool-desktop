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
