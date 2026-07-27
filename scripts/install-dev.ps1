[CmdletBinding()]
param(
    [string]$PlayniteExtensionsPath = (Join-Path $env:APPDATA 'Playnite\Extensions'),
    [switch]$BuildFirst
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$extensionId = 'GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec'

function Read-ManifestVersion {
    param([Parameter(Mandatory = $true)][string]$ManifestPath)
    $line = Get-Content $ManifestPath | Where-Object { $_ -match '^Version\s*:\s*(.+?)\s*$' } | Select-Object -First 1
    if (-not $line -or $line -notmatch '^Version\s*:\s*(.+?)\s*$') {
        throw "无法读取扩展版本：$ManifestPath"
    }
    return $Matches[1].Trim()
}

if ($BuildFirst) {
    & (Join-Path $PSScriptRoot 'package.ps1')
}

$source = Join-Path $root "artifacts\$extensionId"
if (-not (Test-Path $source)) {
    throw '未找到打包目录，请先运行 scripts/package.ps1。'
}

$expectedVersion = Read-ManifestVersion (Join-Path $source 'extension.yaml')
$target = Join-Path $PlayniteExtensionsPath $extensionId
$temporary = "$target.__new"

$running = @(Get-Process -Name @('Playnite.DesktopApp', 'Playnite.FullscreenApp', 'GameSaveCenter.Worker') -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw "检测到仍在运行的进程：$($running.ProcessName -join ', ')。请完全退出 Playnite 和 Worker，或直接双击仓库根目录的 GameSaveCenter-一键构建安装运行.cmd。"
}

New-Item $PlayniteExtensionsPath -ItemType Directory -Force | Out-Null
Remove-Item $temporary -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item $source $temporary -Recurse -Force

if (Test-Path $target) {
    Remove-Item $target -Recurse -Force -ErrorAction Stop
    if (Test-Path $target) {
        throw "旧扩展目录未能删除：$target"
    }
}

Move-Item $temporary $target

$installedVersion = Read-ManifestVersion (Join-Path $target 'extension.yaml')
$dllPath = Join-Path $target 'GameSaveCenter.Playnite.dll'
$fileVersion = (Get-Item $dllPath).VersionInfo.FileVersion
if ($installedVersion -ne $expectedVersion) {
    throw "安装验证失败：期望 $expectedVersion，实际 $installedVersion。"
}
if ($fileVersion -and -not $fileVersion.StartsWith("$expectedVersion.")) {
    throw "DLL 版本不一致：期望 $expectedVersion.x，实际 $fileVersion。"
}

Write-Host "已安装到：$target" -ForegroundColor Green
Write-Host "清单版本：$installedVersion" -ForegroundColor Green
Write-Host "DLL 文件版本：$fileVersion" -ForegroundColor Green
Write-Host '请完全退出并重新启动 Playnite。' -ForegroundColor Yellow
