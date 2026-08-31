#!/usr/bin/env bash
# Push to GitHub + trigger production deploy (self-hosted runner on VPS).
#
# Usage (from Api_Vapp_Manually or anywhere):
#   bash devops/scripts/push-and-deploy.sh                 # prod: API+Admin+Public, push, watch
#   bash devops/scripts/push-and-deploy.sh --auto          # only repos ahead of origin
#   bash devops/scripts/push-and-deploy.sh --admin         # Admin only (~2 min)
#   bash devops/scripts/push-and-deploy.sh --api --no-watch
#   bash devops/scripts/push-and-deploy.sh --mac admin     # Mac hotfix (no GitHub)
#
# Note: does NOT commit — commit first, then run this.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GH_DEPLOY="$SCRIPT_DIR/gh-deploy-production.sh"
MAC_DEPLOY="$SCRIPT_DIR/deploy-from-mac.sh"

MONO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
API_DIR="${API_DIR:-$SCRIPT_DIR/../..}"
ADMIN_DIR="${ADMIN_DIR:-$MONO_ROOT/Admin_Vapp}"
PUBLIC_DIR="${PUBLIC_DIR:-$MONO_ROOT/Public_Vapp}"
SCRAPER_DIR="${SCRAPER_DIR:-$MONO_ROOT/scraping_Number_Vapp}"

DO_AUTO=0
DO_MAC=0
MAC_MODE=""
DO_WATCH=1
DO_PUSH=1
EXTRA_ARGS=()

log() { echo "[push-and-deploy] $*"; }

usage() {
  sed -n '3,12p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

repo_ahead() {
  local dir="$1"
  [[ -d "$dir/.git" ]] || return 1
  git -C "$dir" fetch origin -q 2>/dev/null || true
  local branch
  branch="$(git -C "$dir" rev-parse --abbrev-ref HEAD 2>/dev/null || echo main)"
  local ahead
  ahead="$(git -C "$dir" rev-list --count "origin/${branch}..HEAD" 2>/dev/null || echo 0)"
  [[ "${ahead:-0}" -gt 0 ]]
}

repo_dirty() {
  local dir="$1"
  [[ -d "$dir/.git" ]] || return 1
  ! git -C "$dir" diff-index --quiet HEAD -- 2>/dev/null || \
    [[ -n "$(git -C "$dir" ls-files --others --exclude-standard 2>/dev/null)" ]]
}

check_repo_status() {
  local label="$1" dir="$2"
  [[ -d "$dir/.git" ]] || return 0
  if repo_dirty "$dir"; then
    log "WARN: $label has uncommitted changes — commit before deploy ($dir)"
  elif repo_ahead "$dir"; then
    log "OK: $label has unpushed commit(s)"
  fi
}

has_mode_flag() {
  local arg
  for arg in "$@"; do
    case "$arg" in
      --api|--admin|--public|--scraper|--prod|--all) return 0 ;;
    esac
  done
  return 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --auto) DO_AUTO=1; shift ;;
    --no-watch) DO_WATCH=0; shift ;;
    --no-push) DO_PUSH=0; shift ;;
    --mac)
      DO_MAC=1
      MAC_MODE="${2:-}"
      [[ -n "$MAC_MODE" ]] || { echo "ERROR: --mac needs mode: admin|public|api|all" >&2; exit 1; }
      shift 2
      ;;
    -h|--help) usage 0 ;;
    *) EXTRA_ARGS+=("$1"); shift ;;
  esac
done

if [[ "$DO_MAC" -eq 1 ]]; then
  exec bash "$MAC_DEPLOY" "$MAC_MODE"
fi

if [[ "$DO_AUTO" -eq 1 ]]; then
  AUTO_ARGS=()
  repo_ahead "$API_DIR" && AUTO_ARGS+=(--api)
  repo_ahead "$ADMIN_DIR" && AUTO_ARGS+=(--admin)
  repo_ahead "$PUBLIC_DIR" && AUTO_ARGS+=(--public)
  repo_ahead "$SCRAPER_DIR" && AUTO_ARGS+=(--scraper)
  if [[ ${#AUTO_ARGS[@]} -eq 0 ]]; then
    log "No repo ahead of origin — nothing to push/deploy."
    log "Tip: commit first, or use --admin / --api / --prod explicitly."
    exit 0
  fi
  log "Auto-detected: ${AUTO_ARGS[*]}"
  EXTRA_ARGS=("${AUTO_ARGS[@]}")
fi

check_repo_status API "$API_DIR"
check_repo_status Admin "$ADMIN_DIR"
check_repo_status Public "$PUBLIC_DIR"
check_repo_status Scraper "$SCRAPER_DIR"

GH_ARGS=()
if ! has_mode_flag "${EXTRA_ARGS[@]}"; then
  GH_ARGS+=(--prod)
fi
GH_ARGS+=("${EXTRA_ARGS[@]}")
[[ "$DO_PUSH" -eq 1 ]] && GH_ARGS+=(--push)
[[ "$DO_WATCH" -eq 1 ]] && GH_ARGS+=(--watch)

exec bash "$GH_DEPLOY" "${GH_ARGS[@]}"
