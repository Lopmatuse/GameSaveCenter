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

function Assert-PackageContents {
    param(
        [Parameter(Mandatory = $true)][string]$PackagePath,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        # Compress-Archive writes backslashes on Windows. Normalize names so
        # this verification has the same result on Windows and PowerShell 7.
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $requiredEntries = @(
            'extension.yaml',
            'GameSaveCenter.Playnite.dll',
            'GameSaveCenter.Contracts.dll',
            'Worker/GameSaveCenter.Worker.dll',
            'Worker/GameSaveCenter.Worker.runtimeconfig.json'
        )
        $missing = @($requiredEntries | Where-Object { $_ -notin $entries })
        if ($missing.Count -gt 0) {
            throw "安装包缺少必需文件：$($missing -join ', ')"
        }

        $manifestEntry = $archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq 'extension.yaml' } | Select-Object -First 1
        $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
        try {
            $manifestContent = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        if ($manifestContent -notmatch '(?m)^Version\s*:\s*(.+?)\s*$' -or $Matches[1].Trim() -ne $ExpectedVersion) {
            throw "安装包 extension.yaml 版本与预期不一致：预期 $ExpectedVersion。"
        }
    }
    finally {
        $archive.Dispose()
    }
}

# 默认先完整构建；一键开发安装已单独完成构建时可显式跳过，避免重复编译。
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
}

# A normal solution restore does not necessarily contain the runtime-specific
# assets needed by a self-contained Worker publish. Restore this target here
# so package.ps1 remains reproducible after either build path.
$workerProject = Join-Path $root 'src\GameSaveCenter.Worker\GameSaveCenter.Worker.csproj'
Invoke-DotNet -StepName "还原 Worker 发布运行时（$Runtime）" -Arguments @('restore', $workerProject, '-r', $Runtime)

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item $workerStage -ItemType Directory -Force | Out-Null

$publishArgs = @(
    'publish',
    $workerProject,
    '-c', $Configuration,
    '-r', $Runtime,
    '-o', $workerStage,
    '--no-restore',
    '--self-contained', $(if ($SelfContainedWorker) { 'true' } else { 'false' })
)
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

# These assemblies were previously emitted beside the .NET Framework plugin
# when the old WPF-UI dependency was present. They are not plugin-level
# dependencies in the native-WPF build, and some are already supplied by the
# target framework. Keep them optional so packaging follows the actual build
# output instead of failing on stale dependency assumptions. Worker runtime
# dependencies remain under the published Worker directory.
$optionalCompatibilityDependencies = @(
    'System.Memory.dll',
    'System.Buffers.dll',
    'System.Runtime.CompilerServices.Unsafe.dll',
    'System.ValueTuple.dll'
)

foreach ($file in $required) {
    # extension.yaml is source-of-truth.  The copy emitted in bin/ can be
    # stale when a previous build was interrupted or when packaging is run
    # with SkipBuild; never let that stale file overwrite the current version.
    if ($file -eq 'extension.yaml') {
        $source = $sourceManifest
    }
    else {
        $source = Join-Path $pluginOutput $file
        if (-not (Test-Path $source)) {
            $source = Join-Path $root "src\GameSaveCenter.Playnite\$file"
        }
    }
    if (-not (Test-Path $source)) {
        throw "打包缺少文件：$file。请检查前面的编译输出，不能跳过构建错误继续打包。"
    }
    Copy-Item $source $stage -Force
}

foreach ($file in $optionalCompatibilityDependencies) {
    $source = Join-Path $pluginOutput $file
    if (Test-Path $source) {
        Copy-Item $source $stage -Force
    }
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
Assert-PackageContents -PackagePath $pext -ExpectedVersion $packageVersion

Write-Host "`n打包成功：$zip" -ForegroundColor Green
Write-Host "Playnite 安装包：$pext" -ForegroundColor Green
Write-Host '若当前 Playnite 拒绝直接安装 .pext，请使用 scripts/install-dev.ps1。' -ForegroundColor Yellow
