Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$appBin = Join-Path $repoRoot 'ToolHub.App\bin\Debug\net8.0'
$runtimeConfig = Join-Path $appBin 'ToolHub.App.runtimeconfig.json'
$sourcePath = Join-Path $PSScriptRoot 'HostRegressionTests.cs'
$globalUsingsPath = Join-Path $PSScriptRoot 'ToolHub.GlobalUsings.cs'
$outputPath = Join-Path $appBin 'ToolHub.App.SourceTests.dll'
$outputRuntimeConfig = Join-Path $appBin 'ToolHub.App.SourceTests.runtimeconfig.json'

if (-not (Test-Path $runtimeConfig)) {
    throw "Runtime config not found: $runtimeConfig"
}

$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tmp_dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
[System.IO.Directory]::CreateDirectory($env:DOTNET_CLI_HOME) | Out-Null

$sdkRoot = 'C:\Program Files\dotnet\sdk'
$refRoot = 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref'
$sharedRoot = 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App'
$sdkPath = Get-ChildItem $sdkRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
$refPath = Get-ChildItem $refRoot -Directory | Where-Object { $_.Name -like '8.*' } | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
$sharedPath = Get-ChildItem $sharedRoot -Directory | Where-Object { $_.Name -like '8.*' } | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName

if (-not $sdkPath) {
    throw 'No .NET SDK was found.'
}

if (-not $refPath) {
    throw 'No .NET 8 reference pack was found.'
}

if (-not $sharedPath) {
    throw 'No .NET 8 shared runtime was found.'
}

$cscPath = Join-Path $sdkPath 'Roslyn\bincore\csc.dll'
$refNet8Path = Join-Path $refPath 'ref\net8.0'
$codePagesPath = Join-Path $sharedPath 'System.Text.Encoding.CodePages.dll'
$photinoPath = Join-Path $appBin 'Photino.NET.dll'
$ptyPath = Join-Path $appBin 'Pty.Net.dll'

if (-not (Test-Path $cscPath)) {
    throw "Roslyn compiler not found: $cscPath"
}

if (-not (Test-Path $refNet8Path)) {
    throw "Reference assemblies not found: $refNet8Path"
}

foreach ($requiredPath in @($codePagesPath, $photinoPath, $ptyPath)) {
    if (-not (Test-Path $requiredPath)) {
        throw "Required reference not found: $requiredPath"
    }
}

$appSourceFiles = Get-ChildItem (Join-Path $repoRoot 'ToolHub.App') -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' } |
    Sort-Object FullName |
    Select-Object -ExpandProperty FullName

$referenceArgs = Get-ChildItem $refNet8Path -Filter *.dll | ForEach-Object { "-r:$($_.FullName)" }
$compileArgs = @(
    $cscPath,
    '/nologo',
    '/target:exe',
    '/langversion:latest',
    '/nullable:enable',
    '/main:HostRegressionTests',
    "/out:$outputPath",
    "-r:$codePagesPath",
    "-r:$photinoPath",
    "-r:$ptyPath"
) + $referenceArgs + $appSourceFiles + @(
    $globalUsingsPath,
    $sourcePath
)

& dotnet @compileArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Copy-Item $runtimeConfig $outputRuntimeConfig -Force

Push-Location $appBin
try {
    & dotnet exec '.\ToolHub.App.SourceTests.dll'
    if ($LASTEXITCODE -ne 0) {
        throw "Host regression tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
