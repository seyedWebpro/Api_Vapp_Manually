#!/usr/bin/env bash
# Build API Docker image on Mac and deploy to server with progress, resume and stall detection.
#
# Usage (from Api_Vapp_Manually root):
#   SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh
#   SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh --no-deploy
#
# Optimizations applied:
#   - Uses zstd (multi-threaded, fast) or pigz/gzip for image compression.
#   - Uses rsync --progress --partial for resumable upload with speed/ETA.
#   - Falls back to pv+ssh or plain ssh if advanced tools are missing.
#   - SSH keepalive options prevent silent hangs.
#   - A background watchdog kills the pipeline if no progress is seen for 3 minutes.
#   - Saves image to a temp file first so size is known and upload can resume.
#
# Recommended tools on Mac:
#   brew install zstd rsync pv pigz
#   (zstd and rsync are usually present; pv/pigz are optional)
#
# Prerequisites: ~/.ssh/config with Host vapp-prod (Port 3031) — see devops/MAC-SERVER.md
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="${LOCAL_API_DIR:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
SERVER="${SERVER:-vapp-prod}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-docker/.env}"
API_IMAGE="${API_IMAGE:-vapp-api}"
DEPLOY_AFTER_LOAD="${DEPLOY_AFTER_LOAD:-1}"
TMP_TAR=""
WATCHDOG_PID=""

if [[ "${1:-}" == "--no-deploy" ]]; then
  DEPLOY_AFTER_LOAD=0
fi

# shellcheck source=lib/deploy-progress.sh
source "$SCRIPT_DIR/lib/deploy-progress.sh"

cleanup() {
  [[ -n "$TMP_TAR" && -f "$TMP_TAR" ]] && rm -f "$TMP_TAR"
  [[ -n "$WATCHDOG_PID" ]] && kill "$WATCHDOG_PID" 2>/dev/null || true
  deploy_stop_heartbeat
  deploy_stop_npm_watch
}
trap cleanup EXIT

# SSH options to avoid silent hangs and detect dead connections quickly.
SSH_OPTS=(
  -o ServerAliveInterval=15
  -o ServerAliveCountMax=4
  -o TCPKeepAlive=yes
  -o ConnectTimeout=30
  -o BatchMode=no
)

# Detect available helper tools.
HAS_PV=$(command -v pv || true)
HAS_PIGZ=$(command -v pigz || true)
HAS_ZSTD=$(command -v zstd || true)
HAS_RSYNC=$(command -v rsync || true)

echo "=== deploy-api-upload-image ==="
echo "Build: $LOCAL_API_DIR"
echo "Server: $SERVER:$REMOTE_API_DIR"
echo "Image: $API_IMAGE"
echo "Tools: zstd=${HAS_ZSTD:-none}, pigz=${HAS_PIGZ:-none}, pv=${HAS_PV:-none}, rsync=${HAS_RSYNC:-none}"

cd "$LOCAL_API_DIR"

deploy_step "Build Docker image"
docker compose -f "$COMPOSE_FILE" build api

# Choose the fastest available compression method.
# Priority: zstd > pigz > gzip
if [[ -n "$HAS_ZSTD" ]]; then
  COMPRESS_CMD=(zstd -T0 -3)
  DECOMPRESS_CMD=(zstd -d)
  EXT="zst"
  COMPRESS_NAME="zstd"
elif [[ -n "$HAS_PIGZ" ]]; then
  COMPRESS_CMD=(pigz -1)
  DECOMPRESS_CMD=(pigz -d)
  EXT="gz"
  COMPRESS_NAME="pigz"
else
  COMPRESS_CMD=(gzip -1)
  DECOMPRESS_CMD=(gunzip)
  EXT="gz"
  COMPRESS_NAME="gzip"
fi

TMP_TAR=$(mktemp "/tmp/vapp-api-XXXXXX.tar.${EXT}")
REMOTE_TAR="/tmp/vapp-api-upload-$(date +%s).tar.${EXT}"

deploy_step "Save and compress image ($COMPRESS_NAME)"
save_start=$SECONDS
deploy_log "Saving image to temp file: $TMP_TAR"
docker save "$API_IMAGE" | "${COMPRESS_CMD[@]}" > "$TMP_TAR"
save_elapsed=$(_deploy_elapsed "$save_start")

SIZE=$(stat -f%z "$TMP_TAR" 2>/dev/null || stat -c%s "$TMP_TAR" 2>/dev/null || echo 0)
SIZE_HR=$(du -sh "$TMP_TAR" 2>/dev/null | cut -f1 || echo "${SIZE}B")
deploy_log "Saved $SIZE_HR in $save_elapsed"

# Background watchdog: if the upload process does not advance for 3 minutes, kill it.
# With rsync this is usually not needed, but acts as a safety net for plain ssh fallbacks.
start_watchdog() {
  local target_pid="$1"
  local last_size=0
  local last_change=$SECONDS
  local stall_limit=180

  (
    while kill -0 "$target_pid" 2>/dev/null; do
      sleep 30
      local current_size=0
      if [[ -f "$TMP_TAR" ]]; then
        current_size=$(stat -f%z "$TMP_TAR" 2>/dev/null || stat -c%s "$TMP_TAR" 2>/dev/null || echo 0)
      fi

      # Check remote partial file size to detect upload progress.
      local remote_size=0
      remote_size=$(ssh "${SSH_OPTS[@]}" "$SERVER" "stat -c%s '$REMOTE_TAR' 2>/dev/null || stat -f%z '$REMOTE_TAR' 2>/dev/null || echo 0" 2>/dev/null || echo 0)

      if [[ "$current_size" -eq "$last_size" && "$remote_size" -eq "$last_size" ]]; then
        if (( SECONDS - last_change >= stall_limit )); then
          deploy_log "⚠ STUCK: no upload progress for $((stall_limit / 60)) min — killing transfer"
          kill "$target_pid" 2>/dev/null || true
          exit 1
        fi
      else
        last_size="$remote_size"
        last_change=$SECONDS
      fi
    done
  ) &
  WATCHDOG_PID=$!
}

deploy_step "Upload image to server ($SIZE_HR)"
upload_start=$SECONDS

if [[ -n "$HAS_RSYNC" ]]; then
  deploy_log "Using rsync --progress --partial (resumable)"
  rsync -ah --progress --partial \
    --timeout=300 \
    "$TMP_TAR" \
    "$SERVER:$REMOTE_TAR" &
  RSYNC_PID=$!
  start_watchdog "$RSYNC_PID"

  if ! wait "$RSYNC_PID"; then
    deploy_log "ERROR: rsync upload failed"
    exit 1
  fi
  deploy_log "Upload finished in $(_deploy_elapsed "$upload_start")"

  deploy_log "Loading image on server..."
  ssh "${SSH_OPTS[@]}" "$SERVER" "${DECOMPRESS_CMD[@]} < '$REMOTE_TAR' | docker load" || {
    deploy_log "ERROR: docker load on server failed"
    exit 1
  }
  ssh "${SSH_OPTS[@]}" "$SERVER" "rm -f '$REMOTE_TAR'" || true

elif [[ -n "$HAS_PV" ]]; then
  deploy_log "Using pv + ssh (install rsync for resumable uploads)"
  pv -pteab -s "$SIZE" "$TMP_TAR" | ssh "${SSH_OPTS[@]}" "$SERVER" "${DECOMPRESS_CMD[@]} | docker load" &
  SSH_PID=$!
  start_watchdog "$SSH_PID"

  if ! wait "$SSH_PID"; then
    deploy_log "ERROR: ssh upload failed"
    exit 1
  fi
  deploy_log "Upload finished in $(_deploy_elapsed "$upload_start")"

else
  deploy_log "Using plain ssh (install rsync or pv for progress)"
  deploy_start_heartbeat "upload image" 15
  ssh "${SSH_OPTS[@]}" "$SERVER" "${DECOMPRESS_CMD[@]} | docker load" < "$TMP_TAR" &
  SSH_PID=$!
  start_watchdog "$SSH_PID"

  if ! wait "$SSH_PID"; then
    deploy_log "ERROR: ssh upload failed"
    exit 1
  fi
  deploy_stop_heartbeat
  deploy_log "Upload finished in $(_deploy_elapsed "$upload_start")"
fi

[[ -n "$WATCHDOG_PID" ]] && kill "$WATCHDOG_PID" 2>/dev/null || true
WATCHDOG_PID=""

if [[ "$DEPLOY_AFTER_LOAD" == "1" ]]; then
  deploy_step "Restart API container on server"
  deploy_start_heartbeat "deploy/restart API" 15
  ssh "${SSH_OPTS[@]}" "$SERVER" "cd $REMOTE_API_DIR && (git pull origin main || echo 'WARN: git pull failed — continuing with uploaded image') && docker compose -f $COMPOSE_FILE --env-file $ENV_FILE up -d --no-deps --force-recreate --no-build api" || {
    deploy_log "ERROR: container restart failed"
    exit 1
  }
  deploy_stop_heartbeat

  deploy_step "Health check on server"
  ssh "${SSH_OPTS[@]}" "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/health-check.sh" || true
fi

deploy_log "✓ API image uploaded${DEPLOY_AFTER_LOAD:+ and deployed} successfully in $(_deploy_elapsed "$upload_start")"
