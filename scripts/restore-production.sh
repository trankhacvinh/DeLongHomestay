#!/usr/bin/env bash
set -euo pipefail

if [[ "${1:-}" != "--confirm" || -z "${2:-}" ]]; then
  echo "Usage: DATABASE_URL=postgresql://... $0 --confirm <backup-directory>" >&2
  echo "Restore is destructive to the target database. Use a staging/empty database for rehearsal." >&2
  exit 2
fi

: "${DATABASE_URL:?Set DATABASE_URL to the TARGET PostgreSQL URI before restore.}"
BACKUP_DIR="$2"
DUMP="$BACKUP_DIR/database.dump"

if [[ ! -f "$DUMP" ]]; then
  echo "Missing $DUMP" >&2
  exit 3
fi

if [[ -f "$BACKUP_DIR/SHA256SUMS" ]]; then
  echo "Verifying checksums..."
  (
    cd "$BACKUP_DIR"
    if command -v sha256sum >/dev/null 2>&1; then
      sha256sum -c SHA256SUMS
    elif command -v shasum >/dev/null 2>&1; then
      shasum -a 256 -c SHA256SUMS
    else
      echo "No SHA-256 command found; checksum verification skipped." >&2
    fi
  )
fi

echo "Restoring PostgreSQL target. Existing objects may be dropped..."
pg_restore --clean --if-exists --no-owner --no-privileges --dbname="$DATABASE_URL" "$DUMP"

echo "Database restore complete."
echo "Runtime file archives are intentionally NOT extracted automatically."
echo "Restore data-root.tar.gz and media-root.tar.gz into dedicated staging paths, then point Storage settings to them."
