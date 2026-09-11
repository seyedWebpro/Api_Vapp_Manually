#!/usr/bin/env bash
# Wrapper — منطق کامل در crawl-forbidden-words.py
#
# Usage:
#   BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-forbidden-words.sh
#   SKIP_API_RESTART=1 SKIP_BUILD=1 bash devops/scripts/crawl-forbidden-words.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
exec python3 "$ROOT/devops/scripts/crawl-forbidden-words.py" "$@"
