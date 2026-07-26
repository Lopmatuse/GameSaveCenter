[CmdletBinding()]
param(
    [string]$PlayniteExtensionsPath = (Join-Path $env:APPDATA 'Playnite\Extensions'),
    [switch]$BuildFirst
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($BuildFirst) { & (Join-Path $PSScriptRoot 'package.ps1') }
$source = Join-Path $root 'artifacts\GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec'
if (-not (Test-Path $source)) { throw '未找到打包目录，请先运行 scripts/package.ps1。' }
$target = Join-Path $PlayniteExtensionsPath 'GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec'
New-Item $PlayniteExtensionsPath -ItemType Directory -Force | Out-Null
Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item $source $target -Recurse -Force
Write-Host "已安装到：$target" -ForegroundColor Green
Write-Host '请完全退出并重新启动 Playnite。' -ForegroundColor Yellow
