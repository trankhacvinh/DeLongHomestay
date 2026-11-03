#!/usr/bin/env bash
# Rebuild wwwroot/vendor/tabler/tabler.min.css from the pinned Tabler sources.
# Only needed when upgrading Tabler or changing delong.scss; the server needs no Node at runtime.
set -euo pipefail
TABLER_VERSION=1.4.0
BOOTSTRAP_VERSION=5.3.7
SASS_VERSION=1.89.0
PURGECSS_VERSION=7.0.2
here="$(cd "$(dirname "$0")" && pwd)"
root="$here/../.."
work="$(mktemp -d)"
cd "$work"
npm pack "@tabler/core@$TABLER_VERSION" >/dev/null
tar xzf tabler-core-*.tgz
cd package
npm i --no-audit --no-fund --no-save "bootstrap@$BOOTSTRAP_VERSION" "sass@$SASS_VERSION" "purgecss@$PURGECSS_VERSION" >/dev/null
cp "$here/delong.scss" scss/delong.scss
npx sass --load-path=node_modules --load-path=scss --quiet --style=compressed --no-source-map \
  scss/delong.scss full.css
# Drop rules whose classes appear nowhere in pages, scripts or C# (class names built in code are caught by Features/**/*.cs).
web="$root/src/DeLong.Web"
npx purgecss --css full.css \
  --content "$web/Pages/**/*.cshtml" "$web/wwwroot/js/**/*.js" "$web/**/*.cs" \
  --safelist standard '/^(show|fade|collapsing|collapse|collapsed|active|disabled|open|is-|has-|was-validated|modal|toast|dropdown|tooltip|bs-|placeholder|spinner|offcanvas)/' \
  --keyframes --variables --output "$root/src/DeLong.Web/wwwroot/vendor/tabler/tabler.min.css" >/dev/null
echo "Built tabler.min.css (Tabler $TABLER_VERSION, Bootstrap $BOOTSTRAP_VERSION)"
