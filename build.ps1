param(
    [switch]$NoRun
)

$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot

Write-Host '==> 安装前端依赖并构建...'
Push-Location frontend
npm install
npm run build
Pop-Location

Write-Host '==> 构建后端...'
dotnet build ToolHub.App/ToolHub.App.csproj

if (-not $NoRun) {
    Write-Host '==> 启动桌面应用...'
    dotnet run --project ToolHub.App/ToolHub.App.csproj
}
