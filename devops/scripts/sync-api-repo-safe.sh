#!/usr/bin/env bash
# Safe sync of Api_Vapp_Manually on the server to origin/<branch>.
# Keeps runtime/local files that must never be overwritten by git:
#   - docker/.env
#   - secrets/ (Firebase, etc.)
#   - wwwroot/uploads/
#   - log/
#   - docker/.env.bak-*
#
# Usage (on server):
#   bash devops/scripts/sync-api-repo-safe.sh
#   API_BRANCH=main bash devops/scripts/sync-api-repo-safe.sh
set -euo pipefail

# When piped via `bash -s` (Mac → server deploy), BASH_SOURCE is unset.
# Prefer explicit API_REPO_DIR from the caller in that case.
if [[ -z "${API_REPO_DIR:-}" ]]; then
  if [[ -n "${BASH_SOURCE[0]:-}" ]]; then
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    API_REPO_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
  else
    API_REPO_DIR="$(pwd)"
  fi
fi
API_BRANCH="${API_BRANCH:-main}"
ENV_FILE="${ENV_FILE:-$API_REPO_DIR/docker/.env}"

cd "$API_REPO_DIR"

if [[ ! -d .git ]]; then
  echo "WARN: $API_REPO_DIR is not a git repo — skip sync" >&2
  exit 0
fi

KEEP_ENV=""
if [[ -f "$ENV_FILE" ]]; then
  KEEP_ENV="$(mktemp /tmp/vapp-docker.env.XXXXXX)"
  cp -a "$ENV_FILE" "$KEEP_ENV"
fi

echo "Sync API repo → origin/$API_BRANCH (preserving .env, secrets, uploads, log, backups)"
git fetch origin "$API_BRANCH"
git reset --hard "origin/$API_BRANCH"
git clean -fd \
  --exclude=docker/.env \
  --exclude='docker/.env.bak-*' \
  --exclude=secrets \
  --exclude=wwwroot/uploads \
  --exclude=log \
  --exclude=backups \
  --exclude='backups/**'

if [[ ! -f "$ENV_FILE" && -n "$KEEP_ENV" && -f "$KEEP_ENV" ]]; then
  mkdir -p "$(dirname "$ENV_FILE")"
  cp -a "$KEEP_ENV" "$ENV_FILE"
  echo "Restored $ENV_FILE"
fi
[[ -n "$KEEP_ENV" ]] && rm -f "$KEEP_ENV"

echo "OK: $(git rev-parse --short HEAD) $(git log -1 --pretty=%s)"
git status -sb
