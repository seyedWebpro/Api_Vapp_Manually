#!/usr/bin/env bash
# منتظر اتمام deploy-front در پس‌زمینه → سپس health-check
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

echo "Waiting for deploy-front to finish → $LOG"
echo "(Ctrl+C only stops this watcher — build continues in background)"

deadline=$((SECONDS + 1200))
while [[ $SECONDS -lt $deadline ]]; do
  if grep -q "=== deploy-front done" "$LOG" 2>/dev/null; then
    echo ""
    tail -15 "$LOG"
    break
  fi
  if ! pgrep -f "deploy-front.sh" >/dev/null 2>&1 && ! pgrep -f "docker build.*vapp-admin" >/dev/null 2>&1; then
    if grep -q "ERROR:" "$LOG" 2>/dev/null && ! grep -q "=== deploy-front done" "$LOG" 2>/dev/null; then
      echo "ERROR: deploy-front failed — last lines:" >&2
      tail -30 "$LOG" >&2
      exit 1
    fi
  fi
  sleep 5
  printf '.'
done

if ! grep -q "=== deploy-front done" "$LOG" 2>/dev/null; then
  echo "" >&2
  echo "WARN: timeout (20 min) — check log: tail -f $LOG" >&2
  exit 1
fi

echo ""
bash "$SCRIPT_DIR/health-check.sh" || true
