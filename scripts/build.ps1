[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 8 SDK 未安装或不在 PATH。' }
    dotnet --info
    dotnet restore .\GameSaveCenter.sln
    dotnet build .\GameSaveCenter.sln -c $Configuration --no-restore
    if (-not $SkipTests) { dotnet test .\tests\GameSaveCenter.Core.Tests\GameSaveCenter.Core.Tests.csproj -c $Configuration --no-build }
    Write-Host "构建完成。下一步运行 scripts/package.ps1" -ForegroundColor Green
}
finally { Pop-Location }
