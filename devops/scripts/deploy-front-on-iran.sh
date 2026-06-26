#!/usr/bin/env bash
# Build + deploy Admin روی سرور ایران (mirror ایران‌سرور)
# Usage: bash deploy-front-on-iran.sh [--foreground]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "$(id -u)" -eq 0 ]]; then
  bash "$SCRIPT_DIR/apply-build-mirrors-iranserver.sh"
else
  sudo bash "$SCRIPT_DIR/apply-build-mirrors-iranserver.sh"
fi

export FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-host}"
export NPM_REGISTRY="${NPM_REGISTRY:-https://npm.iranserver.com/repository/npm/}"
export NPM_REGISTRY_FALLBACK="${NPM_REGISTRY_FALLBACK:-https://registry.npmmirror.com}"

exec bash "$SCRIPT_DIR/deploy-front.sh" "$@"
