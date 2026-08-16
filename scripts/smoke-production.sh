#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${1:-${BASE_URL:-}}"
SITE_SLUG="${2:-${SITE_SLUG:-}}"
if [[ -z "$BASE_URL" ]]; then echo "Usage: smoke-production.sh https://example.com [site-slug]" >&2; exit 2; fi
BASE_URL="${BASE_URL%/}"
check() {
  local path="$1"
  echo "[smoke] GET $path"
  curl --fail --silent --show-error --location --max-time 15 -o /dev/null "$BASE_URL$path"
}
check "/health/live"
check "/health/ready"
check "/"
check "/rooms"
check "/blog"
check "/sitemap.xml"
check "/robots.txt"
if [[ -n "$SITE_SLUG" ]]; then
  check "/h/$SITE_SLUG"
  check "/h/$SITE_SLUG/rooms"
  check "/h/$SITE_SLUG/blog"
fi
echo "[smoke] PASS"
