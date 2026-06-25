#!/usr/bin/env bash
# صبر تا تمام شدن build front (پس‌زمینه) + health-check
# reuse: الگوی لاگ «deploy-front done»، timeout
#
# Usage:
#   bash wait-front-deploy.sh
#   bash wait-front-deploy.sh /root/vapp-front-deploy-2026-06-20.log
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG="${1:-}"

if [[ -z "$LOG" && -f "$HOME/.vapp-last-front-deploy.log" ]]; then
  LOG="$(cat "$HOME/.vapp-last-front-deploy.log" | tr -d '\r')"
fi
if [[ -z "$LOG" || ! -f "$LOG" ]]; then
  LOG="$(ls -t /root/vapp-front-deploy-*.log 2>/dev/null | head -1 || true)"
fi

if [[ -z "$LOG" || ! -f "$LOG" ]]; then
  echo "ERROR: no front deploy log found. Start deploy first:" >&2
  echo "  bash $SCRIPT_DIR/deploy-server.sh --front-only" >&2
  exit 1
fi

_deploy_done() {
  grep -qE '=== deploy-front(-host)? done ===' "$LOG" 2>/dev/null
}

_show_progress() {
  local line=""
  line="$(grep -E 'STEP |npm progress|npm ci finished|vite build|deploy-front-host (started|done)|ERROR:' "$LOG" 2>/dev/null | tail -1 || true)"
  if [[ -n "$line" ]]; then
    echo ""
    echo "  latest: $line"
  fi
  if [[ -d "$HOME/Admin_Vapp/node_modules" ]]; then
    echo "  node_modules: $(du -sh "$HOME/Admin_Vapp/node_modules" 2>/dev/null | cut -f1)"
  fi
}

echo "Waiting for front deploy → $LOG"
echo "Live: tail -f $LOG"
echo "(Ctrl+C only stops this watcher — build continues in background)"

deadline=$((SECONDS + 2400))
last_progress=$SECONDS
while [[ $SECONDS -lt $deadline ]]; do
  if _deploy_done; then
    echo ""
    echo "=== deploy finished ==="
    tail -20 "$LOG"
    break
  fi

  if ! pgrep -f "deploy-front" >/dev/null 2>&1 \
    && ! pgrep -f "npm ci" >/dev/null 2>&1 \
    && ! pgrep -f "vite build" >/dev/null 2>&1; then
    if grep -q "ERROR:" "$LOG" 2>/dev/null && ! _deploy_done; then
      echo "ERROR: deploy failed — last lines:" >&2
      tail -40 "$LOG" >&2
      exit 1
    fi
  fi

  if (( SECONDS - last_progress >= 30 )); then
    _show_progress
    last_progress=$SECONDS
  fi

  sleep 5
  printf '.'
done

if ! _deploy_done; then
  echo "" >&2
  echo "WARN: timeout (40 min) — check log: tail -f $LOG" >&2
  tail -30 "$LOG" >&2 || true
  exit 1
fi

echo ""
bash "$SCRIPT_DIR/health-check.sh" || true
