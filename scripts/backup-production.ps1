param(
    [string]$BackupRoot = $env:DELONG_BACKUP_DIR,
    [string]$DataRoot = $env:DELONG_DATA_ROOT,
    [string]$MediaRoot = $env:DELONG_MEDIA_ROOT
)

$ErrorActionPreference = "Stop"
if (-not $env:DATABASE_URL) { throw "Set DATABASE_URL to a PostgreSQL URI before running backup." }

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $BackupRoot) { $BackupRoot = Join-Path $repoRoot "backups" }
if (-not $DataRoot) { $DataRoot = Join-Path $repoRoot "src\DeLong.Web\App_Data" }
if (-not $MediaRoot) { $MediaRoot = Join-Path $repoRoot "src\DeLong.Web\wwwroot\uploads" }

$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$target = Join-Path $BackupRoot $stamp
New-Item -ItemType Directory -Path $target -Force | Out-Null

Write-Host "[1/3] Backing up PostgreSQL..."
& pg_dump --format=custom --compress=9 --no-owner --no-privileges --file=(Join-Path $target "database.dump") $env:DATABASE_URL
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed with exit code $LASTEXITCODE" }

if (Test-Path $DataRoot) {
    Write-Host "[2/3] Backing up data root: $DataRoot"
    Compress-Archive -Path (Join-Path $DataRoot "*") -DestinationPath (Join-Path $target "data-root.zip") -Force
} else {
    Write-Host "[2/3] Data root missing ($DataRoot); skipping runtime data archive."
}

if (Test-Path $MediaRoot) {
    Write-Host "[3/3] Backing up public uploads (rooms + site assets): $MediaRoot"
    Compress-Archive -Path (Join-Path $MediaRoot "*") -DestinationPath (Join-Path $target "media-root.zip") -Force
} else {
    Write-Host "[3/3] Media root missing ($MediaRoot); skipping public media archive."
}

$files = Get-ChildItem -Path $target -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" }
$lines = foreach ($file in $files) {
    $hash = Get-FileHash -Path $file.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($file.Name)"
}
$lines | Set-Content -Path (Join-Path $target "SHA256SUMS.txt") -Encoding UTF8

Write-Host "Backup complete: $target"
