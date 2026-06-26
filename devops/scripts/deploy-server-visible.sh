#!/usr/bin/env bash
# Deploy سرور Vapp با نمایش پیشرفت + درصد 0–100 (الگو: vamyab deploy-server-visible.sh)
#
# Usage (روی سرور):
#   bash devops/scripts/deploy-server-visible.sh --front-only
#   bash devops/scripts/deploy-server-visible.sh --fast
#   bash devops/scripts/deploy-server-visible.sh --api-only
#
# Env:
#   FRONT_DEPLOY_MODE=host|docker   (پیش‌فرض: docker — مثل vamyab)
#   PROGRESS_INTERVAL=8
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=deploy-progress-lib.sh
source "$SCRIPT_DIR/deploy-progress-lib.sh"

API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"
LAST_FRONT_LOG="${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}"
DEPLOY_LOG="${DEPLOY_LOG:-/root/vapp-deploy-visible-$(date +%F_%H%M%S).log}"
PROGRESS_WATCH="$SCRIPT_DIR/deploy-progress-watch.sh"

MODE="--fast"
WATCHER_PID=""

usage() {
  cat <<'EOF'
Deploy Vapp با درصد پیشرفت 0–100

  bash deploy-server-visible.sh [--fast|--api-only|--front-only|--full] [--pull-only]

  • نوار پیشرفت هر ۸ ثانیه
  • 100% = API + Admin + health OK
  • فقط فرانت: --front-only

EOF
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --fast|--api-only|--front-only|--full|--pull-only) MODE="$1" ;;
    -h|--help) usage 0 ;;
    *)
      echo "ERROR: unknown option: $1" >&2
      usage 1
      ;;
  esac
  shift
done

stop_watcher() {
  if [[ -n "$WATCHER_PID" ]] && kill -0 "$WATCHER_PID" 2>/dev/null; then
    kill "$WATCHER_PID" 2>/dev/null || true
    wait "$WATCHER_PID" 2>/dev/null || true
  fi
  WATCHER_PID=""
}

trap stop_watcher EXIT INT TERM

progress_mark() {
  local pct="$1"
  local label="$2"
  echo "PROGRESS:${pct} ${label}" | tee -a "$DEPLOY_LOG"
  render_progress_bar "$pct" | tee -a "$DEPLOY_LOG"
}

start_watcher() {
  local marker="$1"
  local title="$2"
  local front_log="${3:-}"
  stop_watcher
  bash "$PROGRESS_WATCH" "$DEPLOY_LOG" "$marker" "$title" "$front_log" &
  WATCHER_PID=$!
}

phase_banner() {
  local num="$1" total="$2" title="$3"
  local pct="${4:-}"
  {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    if [[ -n "$pct" ]]; then
      echo "  [$num/$total] $title"
      render_progress_bar "$pct"
    else
      echo "  [$num/$total] $title"
    fi
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  } | tee -a "$DEPLOY_LOG"
}

log() {
  echo "$@" | tee -a "$DEPLOY_LOG"
}

run_api_visible() {
  local reload_nginx="${1:-0}" allow_slow="${2:-0}"
  progress_mark 8 "api-start"
  ALLOW_SLOW_START="$allow_slow" RELOAD_NGINX="$reload_nginx" \
    bash "$SCRIPT_DIR/deploy-api.sh" 2>&1 | tee -a "$DEPLOY_LOG"
  progress_mark 55 "api-done"
}

run_front_foreground() {
  progress_mark 58 "front-start"
  FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-docker}" \
    bash "$SCRIPT_DIR/deploy-front.sh" --foreground 2>&1 | tee -a "$DEPLOY_LOG"
  progress_mark 90 "front-done"
}

apply_nginx_front() {
  local env_args=()
  if [[ "${FRONT_DEPLOY_MODE:-docker}" == "host" ]]; then
    env_args=(FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}")
  fi
  env "${env_args[@]}" SERVER_IP="${SERVER_IP:-185.116.162.233}" \
    bash "$SCRIPT_DIR/apply-nginx.sh" 2>&1 | tee -a "$DEPLOY_LOG" || true
}

run_front_background_and_wait() {
  FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-docker}" \
    bash "$SCRIPT_DIR/deploy-front.sh" --background 2>&1 | tee -a "$DEPLOY_LOG"
  progress_mark 58 "front-start"

  local front_log=""
  [[ -f "$LAST_FRONT_LOG" ]] && front_log="$(cat "$LAST_FRONT_LOG")"
  if [[ -z "$front_log" || ! -f "$front_log" ]]; then
    log "WARN: front log not found"
    return 0
  fi

  log "Front log: $front_log"
  local i elapsed start pct label bar_line
  start=$(date +%s)
  for i in $(seq 1 180); do
    elapsed=$(($(date +%s) - start))
    if grep -qE "=== deploy-front(-host)? done ===" "$front_log" 2>/dev/null; then
      progress_mark 90 "front-done"
      log "✓ Front deploy finished (${elapsed}s)"
      tail -12 "$front_log" | tee -a "$DEPLOY_LOG" || true
      return 0
    fi
    if grep -qE "^ERROR:|docker build.*failed|npm ERR!|ERROR: dist/" "$front_log" 2>/dev/null; then
      if ! grep -qE "=== deploy-front(-host)? done ===" "$front_log" 2>/dev/null; then
        log "ERROR: front deploy failed"
        tail -30 "$front_log" | tee -a "$DEPLOY_LOG" >&2 || true
        return 1
      fi
    fi

    pct="$(compute_deploy_percent "$DEPLOY_LOG" "$front_log")"
    label="$(progress_status_label "$pct" "$DEPLOY_LOG" "$front_log")"
    bar_line="$(render_progress_bar "$pct")"
    printf '[Front] %s  elapsed %02d:%02d  |  %s\n' \
      "$bar_line" $((elapsed / 60)) $((elapsed % 60)) "$label" | tee -a "$DEPLOY_LOG"
    sleep 10
  done
  log "WARN: front timeout (30 min) — tail -f $front_log"
  return 0
}

total_phases=5
case "$MODE" in
  --api-only) total_phases=3 ;;
  --front-only) total_phases=3 ;;
  --pull-only) total_phases=2 ;;
  --full) total_phases=6 ;;
esac

: >"$DEPLOY_LOG"
log "=== deploy-server-visible mode=$MODE $(date '+%Y-%m-%dT%H:%M:%S') ==="
log "FRONT_DEPLOY_MODE=${FRONT_DEPLOY_MODE:-docker}"
log "Log file: $DEPLOY_LOG"
progress_mark 0 "start"

start_watcher "=== deploy-server-visible finished" "Deploy Vapp"

phase_banner 1 "$total_phases" "Git pull (API + Admin)" 5
cd "$API_REPO_DIR"
git pull origin "${API_BRANCH:-main}" 2>&1 | tee -a "$DEPLOY_LOG"
if [[ -d "$FRONT_DIR/.git" ]]; then
  cd "$FRONT_DIR"
  git pull origin "${FRONT_BRANCH:-main}" 2>&1 | tee -a "$DEPLOY_LOG"
fi
printf 'VITE_API_URL=\n' > "$FRONT_DIR/.env.production" 2>/dev/null || true
progress_mark 5 "git-pull-done"

case "$MODE" in
  --pull-only)
    progress_mark 100 "pull-only-done"
    log "OK: git pull done — 100%"
    ;;
  --api-only)
    phase_banner 2 "$total_phases" "Deploy API" 8
    run_api_visible 0 0
    ;;
  --front-only)
    phase_banner 2 "$total_phases" "Deploy Admin (React)" 58
    run_front_foreground
    apply_nginx_front
    ;;
  --fast)
    phase_banner 2 "$total_phases" "Deploy API" 8
    run_api_visible 0 1
    phase_banner 3 "$total_phases" "Deploy Admin (React)" 58
    run_front_background_and_wait
    apply_nginx_front
    ;;
  --full)
    phase_banner 2 "$total_phases" "Deploy API + Nginx" 8
    run_api_visible 1 1
    phase_banner 3 "$total_phases" "Deploy Admin (React)" 58
    run_front_background_and_wait
    apply_nginx_front
    ;;
esac

if [[ "$MODE" != "--pull-only" ]]; then
  phase_banner "$((total_phases - 1))" "$total_phases" "Health check" 93
  progress_mark 93 "health-start"
  if bash "$SCRIPT_DIR/health-check.sh" 2>&1 | tee -a "$DEPLOY_LOG"; then
    progress_mark 100 "health-ok"
    log "OK: all services healthy"
  else
    progress_mark 98 "health-warn"
    log "WARN: health-check failed — ممکن است هنوز بالا بیاید"
  fi
fi

phase_banner "$total_phases" "$total_phases" "تمام" 100
log "=== deploy-server-visible finished $(date '+%Y-%m-%dT%H:%M:%S') ==="
log ""
render_progress_bar 100 | tee -a "$DEPLOY_LOG"
log ""
log "✓ 100% — Admin: http://185.116.162.233/auth"
log "  Log: $DEPLOY_LOG"

stop_watcher
exit 0
