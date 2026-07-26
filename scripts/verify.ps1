[CmdletBinding()]
param(
    [string]$Ludusavi,
    [string]$Rclone,
    [string]$Worker = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\GameSaveCenter_66e9f2d7-67bb-43ef-b62a-b8e60734fcec\Worker\GameSaveCenter.Worker.exe')
)
$ErrorActionPreference = 'Stop'
function Check-Executable([string]$Name,[string]$Path,[string[]]$Arguments) {
    if ([string]::IsNullOrWhiteSpace($Path)) { Write-Warning "$Name 未配置"; return }
    if (-not (Test-Path $Path)) { throw "$Name 不存在：$Path" }
    Write-Host "[$Name] $Path"
    & $Path @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Name 健康检查失败，退出码 $LASTEXITCODE" }
}
if ([string]::IsNullOrWhiteSpace($Worker)) { Write-Warning 'Worker 未配置' }
elseif (-not (Test-Path $Worker)) { throw "Worker 不存在：$Worker" }
else {
    $workerInfo = (Get-Item $Worker).VersionInfo
    Write-Host "[Worker] $Worker"
    Write-Host "  文件版本：$($workerInfo.FileVersion)"
}
Check-Executable 'Ludusavi' $Ludusavi @('--version')
Check-Executable 'Rclone' $Rclone @('version')
Write-Host '基础可执行文件检查完成。实际备份/恢复请按 docs/WINDOWS_TEST_PLAN.md。' -ForegroundColor Green
