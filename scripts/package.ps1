[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$SelfContainedWorker = $true,
    [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec'
$workerStage = Join-Path $stage 'Worker'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item $workerStage -ItemType Directory -Force | Out-Null

& (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration

$publishArgs = @('publish', (Join-Path $root 'src\GameSaveCenter.Worker\GameSaveCenter.Worker.csproj'), '-c', $Configuration, '-r', $Runtime, '-o', $workerStage)
if ($SelfContainedWorker) { $publishArgs += @('--self-contained','true') } else { $publishArgs += @('--self-contained','false') }
& dotnet @publishArgs

$pluginOutput = Join-Path $root "src\GameSaveCenter.Playnite\bin\$Configuration\net462"
$required = @('GameSaveCenter.Playnite.dll','GameSaveCenter.Contracts.dll','Newtonsoft.Json.dll','extension.yaml','icon.png')
foreach ($file in $required) {
    $source = Join-Path $pluginOutput $file
    if (-not (Test-Path $source)) {
        $source = Join-Path $root "src\GameSaveCenter.Playnite\$file"
    }
    if (-not (Test-Path $source)) { throw "打包缺少文件：$file" }
    Copy-Item $source $stage -Force
}

$zip = Join-Path $artifacts 'GameSaveCenter-0.1.0-playnite.zip'
$pext = Join-Path $artifacts 'GameSaveCenter-0.1.0.pext'
Remove-Item $zip,$pext -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Copy-Item $zip $pext
Write-Host "已生成：$zip" -ForegroundColor Green
Write-Host "已生成：$pext（Playnite 安装包；若当前 Playnite 拒绝直接安装，请使用 install-dev.ps1）" -ForegroundColor Green
