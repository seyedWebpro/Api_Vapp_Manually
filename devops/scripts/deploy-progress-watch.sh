#!/usr/bin/env bash
# نمایش پیشرفت deploy — درصد + نوار + health (Vapp)
#
# Usage:
#   bash deploy-progress-watch.sh /path/to/deploy.log "DONE_MARKER" "عنوان فاز" [front-log]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=deploy-progress-lib.sh
source "$SCRIPT_DIR/deploy-progress-lib.sh"

LOG_FILE="${1:-}"
DONE_MARKER="${2:-=== deploy-server-visible finished}"
PHASE_TITLE="${3:-Deploy Vapp}"
FRONT_LOG="${4:-}"

if [[ -z "$LOG_FILE" ]]; then
  echo "Usage: $0 <log-file> [done-marker] [phase-title] [front-log]" >&2
  exit 1
fi

INTERVAL="${PROGRESS_INTERVAL:-8}"
START_TS=$(date +%s)

format_elapsed() {
  local elapsed="$1"
  printf '%02d:%02d' $((elapsed / 60)) $((elapsed % 60))
}

detect_detail() {
  local log="$1"
  local detail="" step="" proc=""

  if [[ -f "$log" ]]; then
    step="$(grep -oE 'Step [0-9]+/[0-9]+' "$log" 2>/dev/null | tail -1 || true)"
    if grep -q "Successfully built" "$log" 2>/dev/null && ! grep -q "=== deploy-api done" "$log" 2>/dev/null; then
      detail="Docker image ساخته شد — start کانتینر..."
    elif grep -qE 'vite build|npm run build' "$log" 2>/dev/null; then
      detail="vite build (React Admin)"
    elif grep -qE 'npm install|npm ci|added [0-9]+ packages' "$log" 2>/dev/null; then
      detail="npm install dependencies"
    elif grep -q "API health attempt" "$log" 2>/dev/null; then
      detail="صبر health API"
    elif [[ -n "$step" ]]; then
      detail="Docker $step"
    fi

    local last_line
    last_line="$(grep -v '^[[:space:]]*$' "$log" 2>/dev/null | tail -1 || true)"
    if [[ -z "$detail" && -n "$last_line" ]]; then
      detail="${last_line:0:70}"
    fi
  fi

  if pgrep -f 'docker build.*vapp-admin' >/dev/null 2>&1; then
    proc="docker build فرانت فعال"
  elif pgrep -f 'docker build' >/dev/null 2>&1; then
    proc="docker build فعال"
  elif pgrep -f 'vite build' >/dev/null 2>&1; then
    proc="vite build فعال"
  elif pgrep -f 'npm (ci|install)' >/dev/null 2>&1; then
    proc="npm install فعال"
  fi

  if [[ -n "$proc" ]]; then
    echo "${detail:+$detail | }$proc"
  else
    echo "${detail:-در حال اجرا...}"
  fi
}

health_snapshot() {
  local api front pub
  api="$(curl -sS -m 5 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo '---')"
  if [[ -f /var/www/vapp-admin/index.html ]]; then
    front="$(curl -sS -m 5 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo '---')"
  else
    front="$(curl -sS -m 5 -o /dev/null -w '%{http_code}' http://127.0.0.1:3005/ 2>/dev/null || echo '---')"
  fi
  pub="$(curl -sS -m 5 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo '---')"
  echo "health → API:$api FRONT:$front NGINX:$pub"
}

resolve_front_log() {
  local flog="$FRONT_LOG"
  if [[ -z "$flog" && -f "${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}" ]]; then
    flog="$(cat "${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}" 2>/dev/null || true)"
  fi
  echo "$flog"
}

echo "▶ progress watch: $PHASE_TITLE"
echo "  log: $LOG_FILE"
echo "  done: $DONE_MARKER  |  100% = آپدیت کامل"
echo ""

while true; do
  elapsed=$(($(date +%s) - START_TS))
  flog="$(resolve_front_log)"
  pct="$(compute_deploy_percent "$LOG_FILE" "$flog")"
  detail="$(detect_detail "$LOG_FILE")"
  if [[ -n "$flog" && -f "$flog" ]]; then
    local_detail="$(detect_detail "$flog")"
    [[ -n "$local_detail" && "$local_detail" != "در حال اجرا..." ]] && detail="$local_detail"
  fi
  label="$(progress_status_label "$pct" "$LOG_FILE" "$flog")"

  printf '\033[2K[%s] elapsed %s\n' "$PHASE_TITLE" "$(format_elapsed "$elapsed")"
  render_progress_bar "$pct" | while read -r line; do printf '\033[2K  %s\n' "$line"; done
  printf '\033[2K  فاز: %s\n' "$label"
  printf '\033[2K  → %s\n' "$detail"

  if [[ "$pct" -ge 100 ]] || { [[ -f "$LOG_FILE" ]] && grep -qF "$DONE_MARKER" "$LOG_FILE" 2>/dev/null; }; then
    render_progress_bar 100 | while read -r line; do printf '\033[2K  %s\n' "$line"; done
    printf '\033[2K  ✓ 100%% — deploy تمام شد\n'
    break
  fi

  if grep -qE '^ERROR:|docker build.*failed|npm ERR!|ERROR: dist/' "$LOG_FILE" 2>/dev/null; then
    if ! grep -qF "$DONE_MARKER" "$LOG_FILE" 2>/dev/null; then
      printf '\033[2K  ✗ خطا — tail -30 %s\n' "$LOG_FILE" >&2
      tail -30 "$LOG_FILE" >&2 || true
      exit 1
    fi
  fi

  health_snapshot | while read -r line; do printf '\033[2K  %s\n' "$line"; done

  sleep "$INTERVAL"
done

echo ""
echo "✓ progress watch finished — 100%"
