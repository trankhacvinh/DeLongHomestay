param(
    [Parameter(Mandatory = $true)] [string]$BackupDirectory,
    [switch]$Confirm
)

$ErrorActionPreference = "Stop"
if (-not $Confirm) { throw "Restore is destructive. Re-run with -Confirm and target a staging/empty database for rehearsal." }
if (-not $env:DATABASE_URL) { throw "Set DATABASE_URL to the TARGET PostgreSQL URI before restore." }

$dump = Join-Path $BackupDirectory "database.dump"
if (-not (Test-Path $dump)) { throw "Missing $dump" }

$checksumFile = Join-Path $BackupDirectory "SHA256SUMS.txt"
if (Test-Path $checksumFile) {
    Write-Host "Verifying checksums..."
    foreach ($line in Get-Content $checksumFile) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "\s+", 2
        if ($parts.Count -ne 2) { throw "Invalid checksum line: $line" }
        $expected = $parts[0].Trim().ToLowerInvariant()
        $name = $parts[1].Trim()
        $path = Join-Path $BackupDirectory $name
        if (-not (Test-Path $path)) { throw "Missing backup file listed in checksums: $name" }
        $actual = (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected) { throw "Checksum mismatch: $name" }
    }
}

Write-Host "Restoring PostgreSQL target. Existing objects may be dropped..."
& pg_restore --clean --if-exists --no-owner --no-privileges --dbname=$env:DATABASE_URL $dump
if ($LASTEXITCODE -ne 0) { throw "pg_restore failed with exit code $LASTEXITCODE" }

Write-Host "Database restore complete."
Write-Host "Runtime ZIP archives are intentionally NOT extracted automatically."
Write-Host "Restore data-root.zip and media-root.zip into dedicated staging paths, then point Storage settings to them."
