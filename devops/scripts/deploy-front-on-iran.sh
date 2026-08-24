#!/usr/bin/env bash
# Build + deploy Admin روی سرور ایران (registry رسمی — بدون mirror ایران‌سرور)
# Usage: bash deploy-front-on-iran.sh [--foreground]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

export FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-host}"
export NPM_REGISTRY="${NPM_REGISTRY:-https://registry.npmjs.org}"
export NPM_REGISTRY_FALLBACK="${NPM_REGISTRY_FALLBACK:-https://registry.npmmirror.com}"

exec bash "$SCRIPT_DIR/deploy-front.sh" "$@"
