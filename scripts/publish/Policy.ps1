function Write-PublishPolicySummary(
    [bool]$bundlePortablePython,
    [bool]$bundlePortablePip
) {
    Write-Policy "Distribute the entire publish folder. Do not ship only the .exe."
    Write-Policy "Prefer relative tool path/cwd values so published builds remain portable across drives."

    if ($bundlePortablePython) {
        Write-Policy "Bundled Python tools should use runtimePath = 'python\\python.exe' in tools.json."
    }
    else {
        Write-Policy "Python tools will depend on a machine Python unless runtimePath points to another valid interpreter."
    }

    if ($bundlePortablePip) {
        Write-Policy "Bundled pip is included only for the packaged Python under .\\python."
    }

    Write-Policy "Node.js is not bundled by publish.ps1. Node tools still require node.exe on the target machine or an explicit runtimePath."
}

function Write-ReleaseManifest(
    [string]$outputDir,
    [string]$runtime,
    [string]$configuration,
    [bool]$bundlePortablePython,
    [bool]$bundlePortablePip,
    [string]$pythonSourceExe
) {
    $pythonMode = if ($bundlePortablePython) { "Bundled runtime (.\\python\\python.exe)" } else { "Target machine managed" }
    $pipMode = if ($bundlePortablePip) { "Bundled into .\\python" } else { "Not bundled" }
    $pythonSourceSummary = if ([string]::IsNullOrWhiteSpace($pythonSourceExe)) { "(not bundled)" } else { $pythonSourceExe }
    $content = @(
        "ToolHub publish manifest",
        "GeneratedAtUtc: $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')",
        "Runtime: $runtime",
        "Configuration: $configuration",
        "PortablePython: $bundlePortablePython",
        "PortablePip: $bundlePortablePip",
        "PortablePythonMode: $pythonMode",
        "PortablePipMode: $pipMode",
        "PortablePythonSource: $pythonSourceSummary",
        "",
        "Distribution requirements:",
        "- Distribute the entire folder. Do not copy only the executable.",
        "- Keep tools.json beside the application so relative paths resolve from the publish root.",
        "- Prefer relative path and cwd values in tools.json for any packaged scripts or assets.",
        "",
        "Runtime policy:",
        "- Python tools should use runtimePath = python\\python.exe when you bundle Python.",
        "- If Python is not bundled, leave runtimePath empty or point it to a valid interpreter on the target machine.",
        "- Node.js is not bundled by publish.ps1. Node tools require node.exe on PATH or an explicit runtimePath.",
        "",
        "Reference samples:",
        "- Local development sample: tools.example.json",
        "- Published-build sample: tools.publish.example.json"
    ) -join [Environment]::NewLine

    $manifestPath = Join-Path $outputDir "release-manifest.txt"
    Set-Content -Path $manifestPath -Value $content -Encoding UTF8
    Write-Info "Wrote release manifest: $manifestPath"
}
