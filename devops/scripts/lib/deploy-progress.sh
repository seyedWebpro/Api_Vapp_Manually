#!/usr/bin/env bash
# Progress helpers for deploy scripts (source, do not execute directly)
# Usage: source "$(dirname "$0")/lib/deploy-progress.sh"

_deploy_step=0
_deploy_step_total="${DEPLOY_STEP_TOTAL:-5}"
_HEARTBEAT_PID=""
_NPM_WATCH_PID=""

deploy_log() {
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"
}

deploy_step() {
  _deploy_step=$((_deploy_step + 1))
  deploy_log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  deploy_log "STEP ${_deploy_step}/${_deploy_step_total}: $*"
  deploy_log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

_deploy_elapsed() {
  local start="${1:-0}"
  local elapsed=$((SECONDS - start))
  printf '%dm %02ds' $((elapsed / 60)) $((elapsed % 60))
}

deploy_start_heartbeat() {
  local label="${1:-working}"
  local interval="${2:-20}"
  local watch_dir="${3:-}"
  local start=$SECONDS

  (
    while true; do
      sleep "$interval"
      local extra=""
      if [[ -n "$watch_dir" && -d "$watch_dir" ]]; then
        extra=" | size: $(du -sh "$watch_dir" 2>/dev/null | cut -f1)"
      fi
      deploy_log "⏳ ${label} — elapsed $(_deploy_elapsed "$start") (still running...)${extra}"
    done
  ) &
  _HEARTBEAT_PID=$!
}

deploy_stop_heartbeat() {
  if [[ -n "${_HEARTBEAT_PID:-}" ]] && kill -0 "$_HEARTBEAT_PID" 2>/dev/null; then
    kill "$_HEARTBEAT_PID" 2>/dev/null || true
    wait "$_HEARTBEAT_PID" 2>/dev/null || true
  fi
  _HEARTBEAT_PID=""
}

deploy_stop_npm_watch() {
  if [[ -n "${_NPM_WATCH_PID:-}" ]] && kill -0 "$_NPM_WATCH_PID" 2>/dev/null; then
    kill "$_NPM_WATCH_PID" 2>/dev/null || true
    wait "$_NPM_WATCH_PID" 2>/dev/null || true
  fi
  _NPM_WATCH_PID=""
}

deploy_npm_lockfile_packages() {
  node -e "
    const fs = require('fs');
    const p = process.argv[1];
    try {
      const lock = JSON.parse(fs.readFileSync(p, 'utf8'));
      const keys = Object.keys(lock.packages || {}).filter(Boolean);
      console.log(keys.length);
    } catch {
      console.log('?');
    }
  " "${1:-package-lock.json}" 2>/dev/null || echo "?"
}

deploy_start_npm_watch() {
  local lockfile="${1:-package-lock.json}"
  local total stall_file last_size="" last_change=$SECONDS
  total="$(deploy_npm_lockfile_packages "$lockfile")"
  local start=$SECONDS
  stall_file="${NPM_STALL_FILE:-/tmp/vapp-npm-stall-$$}"

  (
    while true; do
      sleep 15
      local installed="0" size="" size_mb=0
      if [[ -d node_modules ]]; then
        installed="$(find node_modules -mindepth 1 -maxdepth 1 -type d 2>/dev/null | wc -l | tr -d ' ')"
        size="$(du -sh node_modules 2>/dev/null | cut -f1)"
        size_mb="$(du -sm node_modules 2>/dev/null | cut -f1)"
      fi
      if [[ "$size" == "$last_size" ]]; then
        if (( SECONDS - last_change >= 120 )); then
          deploy_log "⚠ STUCK: node_modules=${size} unchanged 2+ min — switch registry"
          echo "stuck" >"$stall_file"
        fi
      else
        last_size="$size"
        last_change=$SECONDS
      fi
      if [[ "$total" != "?" && "$total" -gt 0 ]]; then
        local pct=$((installed * 100 / total))
        [[ "$pct" -gt 100 ]] && pct=100
        deploy_log "📦 npm ~${pct}% dirs=${installed}/${total} size=${size:-0} (${size_mb:-0}MB) — $(_deploy_elapsed "$start")"
      else
        deploy_log "📦 npm size=${size:-0} (${size_mb:-0}MB) — $(_deploy_elapsed "$start")"
      fi
      [[ "${size_mb:-0}" -ge 150 ]] && deploy_log "✓ node_modules size OK (~${size_mb}MB)"
    done
  ) &
  _NPM_WATCH_PID=$!
}

deploy_run_npm_install_once() {
  local registry="$1"
  npm config set registry "$registry"
  if [[ "$registry" == *iranserver* ]]; then
    npm config set strict-ssl false 2>/dev/null || true
  fi
  deploy_log "try registry: $registry"
  npm install --no-audit --no-fund --loglevel="${NPM_LOGLEVEL:-info}" --timing \
    --maxsockets="${NPM_MAX_SOCKETS:-5}" \
    --fetch-timeout="${NPM_FETCH_TIMEOUT:-300000}" \
    --fetch-retries="${NPM_FETCH_RETRIES:-3}" 2>&1 | while IFS= read -r line || [[ -n "$line" ]]; do
    printf '[npm] %s\n' "$line"
  done
  return "${PIPESTATUS[0]}"
}

deploy_run_npm_deps() {
  local start=$SECONDS total rc=0 stall_file="${NPM_STALL_FILE:-/tmp/vapp-npm-stall-$$}"
  local iranserver="https://npm.iranserver.com/repository/npm/"
  local primary="${NPM_REGISTRY:-$iranserver}"
  local fallback="${NPM_REGISTRY_FALLBACK:-https://registry.npmmirror.com}"
  local last_resort="${NPM_REGISTRY_LAST:-https://registry.npmjs.org}"
  rm -f "$stall_file"
  total="$(deploy_npm_lockfile_packages package-lock.json)"

  deploy_log "npm install — lockfile packages: ${total}"
  deploy_log "registries: iranserver → npmmirror → npmjs (mirror.iranserver.com)"
  deploy_log "node_modules باید به ~200MB+ برسد — اگر 2 دقیقه روی 1.4M ماند = گیر کرده"

  npm cache clean --force 2>/dev/null || true
  export npm_config_progress=true
  export npm_config_loglevel="${NPM_LOGLEVEL:-info}"

  deploy_start_heartbeat "npm install" 20 "node_modules"
  deploy_start_npm_watch "package-lock.json"

  set +e
  deploy_run_npm_install_once "$primary"
  rc=$?

  if [[ "$rc" -ne 0 ]] || [[ -f "$stall_file" ]] || [[ "$(du -sm node_modules 2>/dev/null | cut -f1)" -lt 100 ]]; then
    deploy_log "WARN: retry — clean node_modules + cache + $fallback"
    rm -f "$stall_file"
    rm -rf node_modules
    npm cache clean --force 2>/dev/null || true
    deploy_run_npm_install_once "$fallback"
    rc=$?
  fi

  if [[ "$rc" -ne 0 ]] || [[ -f "$stall_file" ]] || [[ "$(du -sm node_modules 2>/dev/null | cut -f1)" -lt 100 ]]; then
    deploy_log "WARN: last resort — $last_resort (ممکن است روی ایران کند باشد)"
    rm -f "$stall_file"
    rm -rf node_modules
    npm cache clean --force 2>/dev/null || true
    deploy_run_npm_install_once "$last_resort"
    rc=$?
  fi

  if [[ "$rc" -ne 0 ]] || [[ "$(du -sm node_modules 2>/dev/null | cut -f1)" -lt 100 ]]; then
    deploy_log "ERROR: npm install failed — node_modules too small"
    deploy_log "راه‌حل: bash devops/scripts/deploy-front-upload-node-modules.sh (روی Mac)"
    rc=1
  fi
  set -e

  deploy_stop_npm_watch
  deploy_stop_heartbeat
  rm -f "$stall_file"
  deploy_log "✓ npm install finished in $(_deploy_elapsed "$start") (exit ${rc}) size=$(du -sh node_modules 2>/dev/null | cut -f1)"
  return "$rc"
}

deploy_run_stream() {
  local label="$1"
  shift
  local start=$SECONDS

  deploy_log "▶ ${label} started"
  set +e
  if command -v stdbuf >/dev/null 2>&1; then
    stdbuf -oL -eL "$@" 2>&1 | while IFS= read -r line || [[ -n "$line" ]]; do
      printf '[%s] %s\n' "$label" "$line"
    done
  else
    "$@" 2>&1 | while IFS= read -r line || [[ -n "$line" ]]; do
      printf '[%s] %s\n' "$label" "$line"
    done
  fi
  local rc=${PIPESTATUS[0]}
  set -e
  deploy_log "✓ ${label} finished in $(_deploy_elapsed "$start") (exit ${rc})"
  return "$rc"
}

deploy_run_vite_build() {
  local vite_api_url="${1:-}"
  deploy_start_heartbeat "vite build" 15 "dist"
  deploy_run_stream "vite" env VITE_API_URL="$vite_api_url" npx vite build --logLevel info
  local rc=$?
  deploy_stop_heartbeat
  return "$rc"
}
