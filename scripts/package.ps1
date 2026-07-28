[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [bool]$SelfContainedWorker = $true,
    [string]$Runtime = 'win-x64',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec'
$workerStage = Join-Path $stage 'Worker'
$sourceManifest = Join-Path $root 'src\GameSaveCenter.Playnite\extension.yaml'
$sourceVersionLine = Get-Content $sourceManifest | Where-Object { $_ -match '^Version\s*:\s*(.+?)\s*$' } | Select-Object -First 1
if (-not $sourceVersionLine -or $sourceVersionLine -notmatch '^Version\s*:\s*(.+?)\s*$') {
    throw "无法从 $sourceManifest 读取源码扩展版本。"
}
$sourceVersion = $Matches[1].Trim()


function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$StepName
    )

    Write-Host "`n==> $StepName" -ForegroundColor Cyan
    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$StepName 失败，dotnet 退出码：$exitCode"
    }
}

# 默认先完整构建；一键开发安装已单独完成构建时可显式跳过，避免重复编译。
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
}

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item $workerStage -ItemType Directory -Force | Out-Null

$publishArgs = @(
    'publish',
    (Join-Path $root 'src\GameSaveCenter.Worker\GameSaveCenter.Worker.csproj'),
    '-c', $Configuration,
    '-r', $Runtime,
    '-o', $workerStage,
    '--self-contained', $(if ($SelfContainedWorker) { 'true' } else { 'false' })
)
if ($SkipBuild) {
    $publishArgs += '--no-restore'
}
Invoke-DotNet -StepName "发布 Worker（$Runtime）" -Arguments $publishArgs

$pluginOutput = Join-Path $root "src\GameSaveCenter.Playnite\bin\$Configuration\net462"
$pluginDllPath = Join-Path $pluginOutput 'GameSaveCenter.Playnite.dll'
if (-not (Test-Path $pluginDllPath)) {
    throw "找不到已编译插件：$pluginDllPath"
}
$pluginFileVersion = (Get-Item $pluginDllPath).VersionInfo.FileVersion
if ($pluginFileVersion -and -not $pluginFileVersion.StartsWith("$sourceVersion.")) {
    throw "已编译 DLL 版本不一致：源码为 $sourceVersion，DLL 为 $pluginFileVersion。请删除 bin/obj 后重新构建。"
}
$required = @(
    'GameSaveCenter.Playnite.dll',
    'GameSaveCenter.Contracts.dll',
    'Newtonsoft.Json.dll',
    'extension.yaml',
    'icon.png'
)

foreach ($file in $required) {
    $source = Join-Path $pluginOutput $file
    if (-not (Test-Path $source)) {
        $source = Join-Path $root "src\GameSaveCenter.Playnite\$file"
    }
    if (-not (Test-Path $source)) {
        throw "打包缺少文件：$file。请检查前面的编译输出，不能跳过构建错误继续打包。"
    }
    Copy-Item $source $stage -Force
}


$manifestPath = Join-Path $stage 'extension.yaml'
$versionLine = Get-Content $manifestPath | Where-Object { $_ -match '^Version\s*:\s*(.+?)\s*$' } | Select-Object -First 1
if (-not $versionLine -or $versionLine -notmatch '^Version\s*:\s*(.+?)\s*$') {
    throw "无法从 $manifestPath 读取扩展版本。"
}
$packageVersion = $Matches[1].Trim()
if ($packageVersion -ne $sourceVersion) {
    throw "打包版本不一致：源码 extension.yaml 为 $sourceVersion，打包目录为 $packageVersion。请先清理并重新构建。"
}
$zip = Join-Path $artifacts "GameSaveCenter-$packageVersion-playnite.zip"
$pext = Join-Path $artifacts "GameSaveCenter-$packageVersion.pext"
Get-ChildItem $artifacts -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'GameSaveCenter-*-playnite.zip' -or $_.Name -like 'GameSaveCenter-*.pext' } |
    Remove-Item -Force -ErrorAction SilentlyContinue
Remove-Item $zip,$pext -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Copy-Item $zip $pext

Write-Host "`n打包成功：$zip" -ForegroundColor Green
Write-Host "Playnite 安装包：$pext" -ForegroundColor Green
Write-Host '若当前 Playnite 拒绝直接安装 .pext，请使用 scripts/install-dev.ps1。' -ForegroundColor Yellow
