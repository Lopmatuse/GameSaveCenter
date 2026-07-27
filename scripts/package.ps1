[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [bool]$SelfContainedWorker = $true,
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec'
$workerStage = Join-Path $stage 'Worker'

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

# 必须先完整构建。build.ps1 失败时会直接终止，不再继续制造缺文件的假象。
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration

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
Invoke-DotNet -StepName "发布 Worker（$Runtime）" -Arguments $publishArgs

$pluginOutput = Join-Path $root "src\GameSaveCenter.Playnite\bin\$Configuration\net462"
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

$zip = Join-Path $artifacts 'GameSaveCenter-0.2.0-playnite.zip'
$pext = Join-Path $artifacts 'GameSaveCenter-0.2.0.pext'
Remove-Item $zip,$pext -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Copy-Item $zip $pext

Write-Host "`n打包成功：$zip" -ForegroundColor Green
Write-Host "Playnite 安装包：$pext" -ForegroundColor Green
Write-Host '若当前 Playnite 拒绝直接安装 .pext，请使用 scripts/install-dev.ps1。' -ForegroundColor Yellow
