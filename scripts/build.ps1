[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

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

Push-Location $root
try {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw '未找到 dotnet。请安装 .NET 8 或更高版本的稳定版 SDK，并确认 dotnet 在 PATH 中。'
    }

    $sdkLines = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0) {
        throw "读取 .NET SDK 列表失败，退出码：$LASTEXITCODE"
    }

    $sdkVersions = @($sdkLines | ForEach-Object {
        if ($_ -match '^([0-9]+)\.([0-9]+)\.([0-9]+)') {
            [version]("{0}.{1}.{2}" -f $Matches[1], $Matches[2], $Matches[3])
        }
    })

    if (-not ($sdkVersions | Where-Object { $_.Major -ge 8 })) {
        $installed = if ($sdkLines) { $sdkLines -join [Environment]::NewLine } else { '未检测到任何 SDK' }
        throw "需要 .NET 8 或更高版本的稳定版 SDK。当前检测结果：`n$installed"
    }

    Write-Host '当前可用 SDK：' -ForegroundColor DarkCyan
    $sdkLines | ForEach-Object { Write-Host "  $_" }

    Write-Host "`n==> 检查 XAML 结构" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'check-xaml.ps1') -ProjectRoot $root

    Invoke-DotNet -StepName '显示当前 SDK 信息' -Arguments @('--info')
    Invoke-DotNet -StepName '还原 NuGet 依赖' -Arguments @('restore', '.\GameSaveCenter.sln')
    Invoke-DotNet -StepName "编译解决方案（$Configuration）" -Arguments @('build', '.\GameSaveCenter.sln', '-c', $Configuration, '--no-restore')

    if (-not $SkipTests) {
        Invoke-DotNet -StepName '运行核心单元测试' -Arguments @(
            'test',
            '.\tests\GameSaveCenter.Core.Tests\GameSaveCenter.Core.Tests.csproj',
            '-c', $Configuration,
            '--no-build'
        )
        Invoke-DotNet -StepName '运行 Worker 集成测试' -Arguments @(
            'test',
            '.\tests\GameSaveCenter.Worker.Tests\GameSaveCenter.Worker.Tests.csproj',
            '-c', $Configuration,
            '--no-build'
        )
    }

    Write-Host "`n构建与测试全部成功。下一步可运行 scripts/package.ps1" -ForegroundColor Green
}
finally {
    Pop-Location
}
