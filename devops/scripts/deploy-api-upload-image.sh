#!/usr/bin/env bash
# Build API Docker image on Mac and deploy to server with progress, resume and stall detection.
#
# Usage (from Api_Vapp_Manually root):
#   SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh
#   SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh --no-deploy
#   SKIP_BUILD=1              — use existing local image (no docker compose build)
#   STREAM_UPLOAD=1           — (default) pipe save|compress|ssh like microless — no temp file
#   USE_RSYNC=1               — resumable file upload (slower setup, good if uplink drops)
#   SKIP_GIT_SYNC=1           — skip sync-api-repo-safe after load (image-only hotfix)
#   ZSTD_LEVEL=1              — faster compression (default 1; was -3)
#
# Speed tips: see devops/DEPLOY-TIMING.md — only deploy the layer you changed.
#
# Optimizations:
#   - Default STREAM_UPLOAD=1: docker save | zstd | ssh (microless-style, no temp file)
#   - USE_RSYNC=1: resumable file upload if uplink drops
#   - zstd level 1 (fast) by default; pigz/gzip fallback
#   - SSH keepalive + stall watchdog on rsync path
#   - Before docker load: stop nonessential containers + 2G swap (tiny VPS OOM guard)
#   - wait-db-ready after restart (not brittle one-shot health)
#
# Recommended tools on Mac:
#   brew install zstd rsync pv pigz
#   (zstd and rsync are usually present; pv/pigz are optional)
#
# Prerequisites: ~/.ssh/config with Host vapp-prod (Port 22) — see devops/MAC-SERVER.md
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_API_DIR="${LOCAL_API_DIR:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
SERVER="${SERVER:-vapp-prod}"
REMOTE_API_DIR="${REMOTE_API_DIR:-/root/Api_Vapp_Manually}"
COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-docker/.env}"
API_IMAGE="${API_IMAGE:-vapp-api}"
DEPLOY_AFTER_LOAD="${DEPLOY_AFTER_LOAD:-1}"
SKIP_BUILD="${SKIP_BUILD:-0}"
STREAM_UPLOAD="${STREAM_UPLOAD:-1}"
USE_RSYNC="${USE_RSYNC:-0}"
SKIP_GIT_SYNC="${SKIP_GIT_SYNC:-0}"
ZSTD_LEVEL="${ZSTD_LEVEL:-1}"
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
echo "Mode: STREAM_UPLOAD=${STREAM_UPLOAD} USE_RSYNC=${USE_RSYNC} SKIP_BUILD=${SKIP_BUILD}"

cd "$LOCAL_API_DIR"

if [[ "$SKIP_BUILD" == "1" ]]; then
  deploy_step "Validate existing image (SKIP_BUILD=1)"
  docker image inspect "$API_IMAGE" >/dev/null
else
  deploy_step "Build Docker image"
  docker compose -f "$COMPOSE_FILE" build api
fi

# Choose the fastest available compression method.
# Priority: zstd > pigz > gzip — level 1 default (fast; see DEPLOY-TIMING.md)
if [[ -n "$HAS_ZSTD" ]]; then
  COMPRESS_CMD=(zstd -T0 "-${ZSTD_LEVEL}")
  DECOMPRESS_CMD=(zstd -d)
  EXT="zst"
  COMPRESS_NAME="zstd-${ZSTD_LEVEL}"
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

upload_start=$SECONDS

# ─── Fast path: stream to server (microless-style) ─────────────────────────
if [[ "$STREAM_UPLOAD" == "1" && "$USE_RSYNC" != "1" ]]; then
  deploy_step "Stream upload + load on server ($COMPRESS_NAME)"
  prepare_server_for_image_load
  deploy_start_heartbeat "stream upload image" 15
  if ! run_with_timeout "${DOCKER_LOAD_TIMEOUT_SECS:-900}" \
    docker save "$API_IMAGE" | "${COMPRESS_CMD[@]}" | \
    ssh "${SSH_OPTS[@]}" "$SERVER" "${DECOMPRESS_CMD[*]} | docker load"; then
    deploy_log "ERROR: stream upload/load failed or timed out"
    deploy_log "HINT: retry with USE_RSYNC=1 for resumable upload"
    exit 1
  fi
  deploy_stop_heartbeat
  deploy_log "Stream upload+load finished in $(_deploy_elapsed "$upload_start")"
else
  # ─── Resumable path: temp file + rsync ───────────────────────────────────
_tmp_base=$(mktemp "${TMPDIR:-/tmp}/vapp-api.XXXXXX")
TMP_TAR="${_tmp_base}.tar.${EXT}"
mv "$_tmp_base" "$TMP_TAR"
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

# Wait for an existing PID, or run a command, with a wall-clock timeout.
# Usage: run_with_timeout SECS PID
#        run_with_timeout SECS command args...
run_with_timeout() {
  local max_secs="$1"
  shift
  local cmd_pid=""
  if [[ $# -eq 1 && "$1" =~ ^[0-9]+$ ]]; then
    cmd_pid="$1"
  else
    "$@" &
    cmd_pid=$!
  fi
  local start=$SECONDS
  while kill -0 "$cmd_pid" 2>/dev/null; do
    if (( SECONDS - start >= max_secs )); then
      deploy_log "⚠ TIMEOUT after ${max_secs}s — killing pid $cmd_pid"
      kill "$cmd_pid" 2>/dev/null || true
      sleep 2
      kill -9 "$cmd_pid" 2>/dev/null || true
      wait "$cmd_pid" 2>/dev/null || true
      return 124
    fi
    sleep 5
  done
  wait "$cmd_pid"
}

# Free RAM before docker load on tiny VPS (SQL + scraper + load = OOM lock).
prepare_server_for_image_load() {
  deploy_log "Preparing server RAM (stop nonessential containers + ensure swap)..."
  ssh "${SSH_OPTS[@]}" "$SERVER" 'set +e
    # Optional scraper / leftover admin container — keep SQL + API running if possible
    docker stop phonescraper_api_prod vapp-admin 2>/dev/null
    if [ ! -f /swapfile ]; then
      fallocate -l 2G /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
      chmod 600 /swapfile
      mkswap /swapfile >/dev/null
      swapon /swapfile
      grep -q "/swapfile" /etc/fstab || echo "/swapfile none swap sw 0 0" >> /etc/fstab
    else
      swapon /swapfile 2>/dev/null
    fi
    free -h
    # Abort early if essentially no memory left for docker load
    avail_mb=$(awk "/Mem:/ {print \$7}" <(free -m) 2>/dev/null || echo 0)
    swap_mb=$(awk "/Swap:/ {print \$4}" <(free -m) 2>/dev/null || echo 0)
    echo "avail_mb=${avail_mb:-0} swap_free_mb=${swap_mb:-0}"
    if [ "${avail_mb:-0}" -lt 80 ] && [ "${swap_mb:-0}" -lt 200 ]; then
      echo "ERROR: server too low on memory for docker load (need reboot or more RAM)"
      exit 42
    fi
  ' || {
    local rc=$?
    if [[ "$rc" -eq 42 ]]; then
      deploy_log "ERROR: server OOM — hard-reboot from VPS panel, then re-run this script"
      exit 1
    fi
    deploy_log "WARN: prepare step returned $rc — continuing cautiously"
  }
}

load_image_on_server() {
  local load_timeout="${DOCKER_LOAD_TIMEOUT_SECS:-600}"
  prepare_server_for_image_load
  deploy_log "Loading image on server (timeout ${load_timeout}s)..."
  if ! run_with_timeout "$load_timeout" \
    ssh "${SSH_OPTS[@]}" "$SERVER" "${DECOMPRESS_CMD[*]} < '$REMOTE_TAR' | docker load && rm -f '$REMOTE_TAR'"; then
    deploy_log "ERROR: docker load on server failed or timed out"
    deploy_log "HINT: if SSH hangs, hard-reboot VPS; uploaded file may still be at $REMOTE_TAR"
    exit 1
  fi
  deploy_log "Image loaded on server"
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
  load_image_on_server

elif [[ -n "$HAS_PV" ]]; then
  deploy_log "Using pv + ssh (install rsync for resumable uploads)"
  # Stream upload has no separate load step — still prepare RAM first.
  prepare_server_for_image_load
  pv -pteab -s "$SIZE" "$TMP_TAR" | ssh "${SSH_OPTS[@]}" "$SERVER" "${DECOMPRESS_CMD[@]} | docker load" &
  SSH_PID=$!
  start_watchdog "$SSH_PID"

  if ! run_with_timeout "${DOCKER_LOAD_TIMEOUT_SECS:-900}" "$SSH_PID"; then
    deploy_log "ERROR: ssh upload/load failed or timed out"
    exit 1
  fi
  deploy_log "Upload+load finished in $(_deploy_elapsed "$upload_start")"

else
  deploy_log "Using plain ssh (install rsync or pv for progress)"
  prepare_server_for_image_load
  deploy_start_heartbeat "upload image" 15
  ssh "${SSH_OPTS[@]}" "$SERVER" "${DECOMPRESS_CMD[@]} | docker load" < "$TMP_TAR" &
  SSH_PID=$!
  start_watchdog "$SSH_PID"

  if ! run_with_timeout "${DOCKER_LOAD_TIMEOUT_SECS:-900}" "$SSH_PID"; then
    deploy_log "ERROR: ssh upload/load failed or timed out"
    exit 1
  fi
  deploy_stop_heartbeat
  deploy_log "Upload+load finished in $(_deploy_elapsed "$upload_start")"
fi

fi  # STREAM_UPLOAD vs rsync path

[[ -n "$WATCHDOG_PID" ]] && kill "$WATCHDOG_PID" 2>/dev/null || true
WATCHDOG_PID=""

if [[ "$DEPLOY_AFTER_LOAD" == "1" ]]; then
  if [[ "$SKIP_GIT_SYNC" != "1" ]]; then
    deploy_step "Sync API repo on server (safe reset)"
    API_REPO_DIR="$REMOTE_API_DIR" API_BRANCH="${API_BRANCH:-main}" \
      ssh "${SSH_OPTS[@]}" "$SERVER" \
      "API_REPO_DIR=$REMOTE_API_DIR API_BRANCH=${API_BRANCH:-main} bash -s" \
      < "$SCRIPT_DIR/sync-api-repo-safe.sh" || {
        deploy_log "ERROR: safe git sync on server failed"
        exit 1
      }
  else
    deploy_log "SKIP_GIT_SYNC=1 — server git unchanged"
  fi

  deploy_step "Restart API container on server"
  deploy_start_heartbeat "deploy/restart API" 15
  ssh "${SSH_OPTS[@]}" "$SERVER" \
    "cd $REMOTE_API_DIR && docker compose -f $COMPOSE_FILE --env-file $ENV_FILE up -d --no-deps --force-recreate --no-build api" || {
    deploy_log "ERROR: container restart failed"
    exit 1
  }
  deploy_stop_heartbeat

  deploy_step "Wait DB + AppVersion ready"
  ssh "${SSH_OPTS[@]}" "$SERVER" "bash $REMOTE_API_DIR/devops/scripts/wait-db-ready.sh" || {
    deploy_log "WARN: wait-db-ready failed — try: deploy-from-mac.sh db-fix"
    exit 1
  }
fi

deploy_log "✓ API image uploaded${DEPLOY_AFTER_LOAD:+ and deployed} successfully in $(_deploy_elapsed "$upload_start")"
