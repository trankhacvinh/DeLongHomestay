#!/usr/bin/env bash
set -euo pipefail

: "${DATABASE_URL:?Set DATABASE_URL to a PostgreSQL URI before running backup.}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKUP_ROOT="${DELONG_BACKUP_DIR:-$REPO_ROOT/backups}"
DATA_ROOT="${DELONG_DATA_ROOT:-$REPO_ROOT/src/DeLong.Web/App_Data}"
MEDIA_ROOT="${DELONG_MEDIA_ROOT:-$REPO_ROOT/src/DeLong.Web/wwwroot/uploads}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
TARGET="${BACKUP_ROOT%/}/${STAMP}"

mkdir -p "$TARGET"

echo "[1/3] Backing up PostgreSQL..."
pg_dump --format=custom --compress=9 --no-owner --no-privileges --file="$TARGET/database.dump" "$DATABASE_URL"

if [[ -d "$DATA_ROOT" ]]; then
  echo "[2/3] Backing up data root: $DATA_ROOT"
  tar -czf "$TARGET/data-root.tar.gz" -C "$(dirname "$DATA_ROOT")" "$(basename "$DATA_ROOT")"
else
  echo "[2/3] Data root missing ($DATA_ROOT); skipping runtime data archive."
fi

if [[ -d "$MEDIA_ROOT" ]]; then
  echo "[3/3] Backing up public uploads (rooms + site assets): $MEDIA_ROOT"
  tar -czf "$TARGET/media-root.tar.gz" -C "$(dirname "$MEDIA_ROOT")" "$(basename "$MEDIA_ROOT")"
else
  echo "[3/3] Media root missing ($MEDIA_ROOT); skipping public media archive."
fi

(
  cd "$TARGET"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum ./* > SHA256SUMS
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 ./* > SHA256SUMS
  else
    echo "No SHA-256 command found; checksum file was not generated." >&2
  fi
)

printf 'Backup complete: %s\n' "$TARGET"
