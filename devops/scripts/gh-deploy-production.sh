#!/usr/bin/env bash
# Trigger GitHub Actions production deploy (CI + CD) for Vapp repos.
#
# Usage (from anywhere):
#   bash devops/scripts/gh-deploy-production.sh --prod
#   bash devops/scripts/gh-deploy-production.sh --all --watch
#   bash devops/scripts/gh-deploy-production.sh --api --push
#
# Modes:
#   --api       API only
#   --admin     Admin panel only
#   --public    Public form/wheel only
#   --scraper   Number scraper only
#   --prod      API + Admin + Public (default if no mode)
#   --all       API + Admin + Public + Scraper
#
# Options:
#   --push      git push before trigger (all selected repos @ main)
#   --watch     wait until workflow(s) finish (gh run watch)
#   --dry-run   print commands only
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MONO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
API_DIR="${API_DIR:-$SCRIPT_DIR/../..}"
ADMIN_DIR="${ADMIN_DIR:-$MONO_ROOT/Admin_Vapp}"
PUBLIC_DIR="${PUBLIC_DIR:-$MONO_ROOT/Public_Vapp}"
SCRAPER_DIR="${SCRAPER_DIR:-$MONO_ROOT/scraping_Number_Vapp}"

API_REPO="${API_REPO:-seyedWebpro/Api_Vapp_Manually}"
ADMIN_REPO="${ADMIN_REPO:-seyedWebpro/Admin_Pannel_Vapp}"
PUBLIC_REPO="${PUBLIC_REPO:-seyedWebpro/PublicWeb_Vapp}"
SCRAPER_REPO="${SCRAPER_REPO:-seyedWebpro/scraping_Number_Vapp}"

API_WORKFLOW="${API_WORKFLOW:-API CI/CD}"
ADMIN_WORKFLOW="${ADMIN_WORKFLOW:-Admin CI/CD}"
PUBLIC_WORKFLOW="${PUBLIC_WORKFLOW:-Public CI/CD}"
SCRAPER_WORKFLOW="${SCRAPER_WORKFLOW:-Scraper CI/CD}"

API_REF="${API_REF:-main}"
ADMIN_REF="${ADMIN_REF:-main}"
PUBLIC_REF="${PUBLIC_REF:-main}"
SCRAPER_REF="${SCRAPER_REF:-main}"

DO_API=0
DO_ADMIN=0
DO_PUBLIC=0
DO_SCRAPER=0
DO_PUSH=0
DO_WATCH=0
DRY_RUN=0
EXPLICIT_MODE=0

declare -a RUN_IDS=()

log() { echo "[$(date '+%H:%M:%S')] $*"; }

usage() {
  sed -n '3,22p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api) DO_API=1; EXPLICIT_MODE=1; shift ;;
    --admin) DO_ADMIN=1; EXPLICIT_MODE=1; shift ;;
    --public) DO_PUBLIC=1; EXPLICIT_MODE=1; shift ;;
    --scraper) DO_SCRAPER=1; EXPLICIT_MODE=1; shift ;;
    --prod) DO_API=1; DO_ADMIN=1; DO_PUBLIC=1; EXPLICIT_MODE=1; shift ;;
    --all) DO_API=1; DO_ADMIN=1; DO_PUBLIC=1; DO_SCRAPER=1; EXPLICIT_MODE=1; shift ;;
    --push) DO_PUSH=1; shift ;;
    --watch) DO_WATCH=1; shift ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) usage 0 ;;
    *) echo "ERROR: unknown option: $1" >&2; usage 1 ;;
  esac
done

if [[ "$EXPLICIT_MODE" -eq 0 ]]; then
  DO_API=1
  DO_ADMIN=1
  DO_PUBLIC=1
fi

if ! command -v gh >/dev/null 2>&1; then
  echo "ERROR: gh CLI not found. Install: brew install gh && gh auth login" >&2
  exit 1
fi

if [[ "$DRY_RUN" -eq 0 ]]; then
  gh auth status >/dev/null 2>&1 || {
    echo "ERROR: gh not logged in. Run: gh auth login" >&2
    exit 1
  }
fi

trigger() {
  local repo="$1" workflow="$2" ref="$3"
  if [[ "$DRY_RUN" -eq 1 ]]; then
    echo "DRY: gh workflow run $(printf %q "$workflow") -R $repo --ref $ref -f deploy=true"
    return 0
  fi
  log "Trigger: $workflow ($repo @ $ref)"
  gh workflow run "$workflow" -R "$repo" --ref "$ref" -f deploy=true
}

maybe_push() {
  local dir="$1" branch="$2" label="$3"
  [[ -d "$dir/.git" ]] || { echo "WARN: skip push $label — not a git repo: $dir" >&2; return 0; }
  if [[ "$DRY_RUN" -eq 1 ]]; then
    echo "DRY: (cd $dir && git push origin $branch)"
    return 0
  fi
  log "git push $label → origin/$branch"
  git -C "$dir" push origin "$branch"
}

collect_run_id() {
  local repo="$1" workflow="$2"
  [[ "$DRY_RUN" -eq 1 ]] && return 0
  sleep 4
  local id
  id="$(gh run list -R "$repo" --workflow "$workflow" --limit 1 --json databaseId -q '.[0].databaseId' 2>/dev/null || true)"
  [[ -n "$id" && "$id" != "null" ]] && RUN_IDS+=("$repo:$id")
}

if [[ "$DO_PUSH" -eq 1 ]]; then
  [[ "$DO_API" -eq 1 ]] && maybe_push "$API_DIR" "$API_REF" "API"
  [[ "$DO_ADMIN" -eq 1 ]] && maybe_push "$ADMIN_DIR" "$ADMIN_REF" "Admin"
  [[ "$DO_PUBLIC" -eq 1 ]] && maybe_push "$PUBLIC_DIR" "$PUBLIC_REF" "Public"
  [[ "$DO_SCRAPER" -eq 1 ]] && maybe_push "$SCRAPER_DIR" "$SCRAPER_REF" "Scraper"
fi

[[ "$DO_API" -eq 1 ]] && { trigger "$API_REPO" "$API_WORKFLOW" "$API_REF"; collect_run_id "$API_REPO" "$API_WORKFLOW"; }
[[ "$DO_ADMIN" -eq 1 ]] && { trigger "$ADMIN_REPO" "$ADMIN_WORKFLOW" "$ADMIN_REF"; collect_run_id "$ADMIN_REPO" "$ADMIN_WORKFLOW"; }
[[ "$DO_PUBLIC" -eq 1 ]] && { trigger "$PUBLIC_REPO" "$PUBLIC_WORKFLOW" "$PUBLIC_REF"; collect_run_id "$PUBLIC_REPO" "$PUBLIC_WORKFLOW"; }
[[ "$DO_SCRAPER" -eq 1 ]] && { trigger "$SCRAPER_REPO" "$SCRAPER_WORKFLOW" "$SCRAPER_REF"; collect_run_id "$SCRAPER_REPO" "$SCRAPER_WORKFLOW"; }

echo ""
log "Actions:"
[[ "$DO_API" -eq 1 ]] && echo "  API:     https://github.com/$API_REPO/actions"
[[ "$DO_ADMIN" -eq 1 ]] && echo "  Admin:   https://github.com/$ADMIN_REPO/actions"
[[ "$DO_PUBLIC" -eq 1 ]] && echo "  Public:  https://github.com/$PUBLIC_REPO/actions"
[[ "$DO_SCRAPER" -eq 1 ]] && echo "  Scraper: https://github.com/$SCRAPER_REPO/actions"

if [[ "$DO_WATCH" -eq 1 && "$DRY_RUN" -eq 0 ]]; then
  ec=0
  for entry in "${RUN_IDS[@]}"; do
    repo="${entry%%:*}"
    id="${entry##*:}"
    log "Watching $repo run $id ..."
    gh run watch "$id" -R "$repo" --exit-status || ec=1
  done
  exit "$ec"
fi

log "OK — workflow(s) triggered. Open Actions links above; Approve production deploy if required."
