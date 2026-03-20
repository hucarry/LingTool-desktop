$pythonRuntimeScriptRoot = Join-Path $PSScriptRoot "python"

. (Join-Path $pythonRuntimeScriptRoot "Discovery.ps1")
. (Join-Path $pythonRuntimeScriptRoot "Bundling.ps1")
. (Join-Path $pythonRuntimeScriptRoot "PipBootstrap.ps1")
