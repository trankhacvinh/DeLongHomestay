#!/usr/bin/env bash
set -euo pipefail

: "${DATABASE_URL:?Set DATABASE_URL to a PostgreSQL URI before running backup.}"

BACKUP_ROOT="${DELONG_BACKUP_DIR:-./backups}"
DATA_ROOT="${DELONG_DATA_ROOT:-}"
MEDIA_ROOT="${DELONG_MEDIA_ROOT:-}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
TARGET="${BACKUP_ROOT%/}/${STAMP}"

mkdir -p "$TARGET"

echo "[1/3] Backing up PostgreSQL..."
pg_dump --format=custom --compress=9 --no-owner --no-privileges --file="$TARGET/database.dump" "$DATABASE_URL"

if [[ -n "$DATA_ROOT" && -d "$DATA_ROOT" ]]; then
  echo "[2/3] Backing up persistent data root..."
  tar -czf "$TARGET/data-root.tar.gz" -C "$(dirname "$DATA_ROOT")" "$(basename "$DATA_ROOT")"
else
  echo "[2/3] DELONG_DATA_ROOT not set or directory missing; skipping runtime data archive."
fi

if [[ -n "$MEDIA_ROOT" && -d "$MEDIA_ROOT" ]]; then
  echo "[3/3] Backing up public media root..."
  tar -czf "$TARGET/media-root.tar.gz" -C "$(dirname "$MEDIA_ROOT")" "$(basename "$MEDIA_ROOT")"
else
  echo "[3/3] DELONG_MEDIA_ROOT not set or directory missing; skipping public media archive."
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
