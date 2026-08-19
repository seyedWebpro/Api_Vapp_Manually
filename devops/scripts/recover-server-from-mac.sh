#!/usr/bin/env bash
# Auto-recover Vapp server from Mac — retry SSH until up, then run light recovery.
#
# Usage:
#   bash devops/scripts/recover-server-from-mac.sh
#   MAX_ATTEMPTS=60 RETRY_SECS=30 bash devops/scripts/recover-server-from-mac.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_REPO="$(cd "$SCRIPT_DIR/../.." && pwd)"
SERVER="${SERVER:-vapp-prod}"
SSH_HOST="${SSH_HOST:-195.24.237.132}"
SSH_PORT="${SSH_PORT:-22}"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/id_ed25519_vapp_server}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-120}"
RETRY_SECS="${RETRY_SECS:-30}"

SSH_OPTS=(-o ConnectTimeout=15 -o BatchMode=yes -o StrictHostKeyChecking=accept-new)
[[ -f "$SSH_KEY" ]] && SSH_OPTS+=(-i "$SSH_KEY" -p "$SSH_PORT")

log() { echo "[$(date '+%Y-%m-%dT%H:%M:%S%z')] $*"; }

log "=== recover-server-from-mac started ==="
log "Target: $SSH_HOST:$SSH_PORT (alias: $SERVER)"
log "Max attempts: $MAX_ATTEMPTS, retry every ${RETRY_SECS}s"

for attempt in $(seq 1 "$MAX_ATTEMPTS"); do
  log "SSH attempt $attempt/$MAX_ATTEMPTS..."
  if ssh "${SSH_OPTS[@]}" "root@$SSH_HOST" 'echo SSH_OK' >/dev/null 2>&1; then
    log "SSH is up — syncing recovery script and running recovery on server..."

    rsync -az -e "ssh ${SSH_OPTS[*]}" \
      "$API_REPO/devops/scripts/recover-server-light.sh" \
      "root@$SSH_HOST:~/Api_Vapp_Manually/devops/scripts/recover-server-light.sh"

    ssh "${SSH_OPTS[@]}" "root@$SSH_HOST" \
      'chmod +x ~/Api_Vapp_Manually/devops/scripts/recover-server-light.sh && bash ~/Api_Vapp_Manually/devops/scripts/recover-server-light.sh'

    log "Checking health from server..."
    ssh "${SSH_OPTS[@]}" "root@$SSH_HOST" \
      'curl -sS -m15 -o /dev/null -w "API:%{http_code}\n" http://127.0.0.1:8080/health; curl -sS -m15 -o /dev/null -w "SCRAPER:%{http_code}\n" http://127.0.0.1:8000/health; docker ps --format "table {{.Names}}\t{{.Status}}" | head -15'

    log "=== recover-server-from-mac SUCCESS ==="
    exit 0
  fi

  if (( attempt < MAX_ATTEMPTS )); then
    sleep "$RETRY_SECS"
  fi
done

log "ERROR: SSH still unreachable after $MAX_ATTEMPTS attempts."
log "VPS appears offline — hard reboot required from hosting panel."
exit 1
