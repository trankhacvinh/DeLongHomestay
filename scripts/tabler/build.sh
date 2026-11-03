#!/usr/bin/env bash
# Rebuild wwwroot/vendor/tabler/tabler.min.css from the pinned Tabler sources.
# Only needed when upgrading Tabler or changing delong.scss; the server needs no Node at runtime.
set -euo pipefail
TABLER_VERSION=1.4.0
BOOTSTRAP_VERSION=5.3.7
SASS_VERSION=1.89.0
here="$(cd "$(dirname "$0")" && pwd)"
root="$here/../.."
work="$(mktemp -d)"
cd "$work"
npm pack "@tabler/core@$TABLER_VERSION" >/dev/null
tar xzf tabler-core-*.tgz
cd package
npm i --no-audit --no-fund --no-save "bootstrap@$BOOTSTRAP_VERSION" "sass@$SASS_VERSION" >/dev/null
cp "$here/delong.scss" scss/delong.scss
npx sass --load-path=node_modules --load-path=scss --quiet --style=compressed --no-source-map \
  scss/delong.scss "$root/src/DeLong.Web/wwwroot/vendor/tabler/tabler.min.css"
echo "Built tabler.min.css (Tabler $TABLER_VERSION, Bootstrap $BOOTSTRAP_VERSION)"
