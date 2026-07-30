[CmdletBinding()]
param(
    [string]$Output = '',
    [switch]$IncludeIgnoredBuildOutput
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$parent = Split-Path -Parent $root
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $parent 'GameSaveCenter-source-with-git.zip'
}
$Output = [System.IO.Path]::GetFullPath($Output)
if (Test-Path $Output) { Remove-Item $Output -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($Output, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $excludedDirectories = @('bin', 'obj', 'artifacts', 'runtime')
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse -Force) {
        $relative = $file.FullName.Substring($root.Length).TrimStart('\\', '/')
        $parts = $relative -split '[\\/]'
        if (-not $IncludeIgnoredBuildOutput -and ($parts | Where-Object { $excludedDirectories -contains $_ })) { continue }
        if ($relative -match '\.db(-shm|-wal)?$|\.log$|rclone\.conf$|secrets\.json$|appsettings\.local\.json$') { continue }
        $entryName = ('GameSaveCenter/' + ($relative -replace '\\', '/'))
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}
Write-Host "已生成含完整 .git 历史的源码包：$Output" -ForegroundColor Green
