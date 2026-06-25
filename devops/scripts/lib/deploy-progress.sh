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
  local total
  total="$(deploy_npm_lockfile_packages "$lockfile")"
  local start=$SECONDS

  (
    while true; do
      sleep 15
      local installed="0"
      if [[ -d node_modules ]]; then
        installed="$(find node_modules -mindepth 1 -maxdepth 1 -type d 2>/dev/null | wc -l | tr -d ' ')"
      fi
      if [[ "$total" != "?" && "$total" -gt 0 ]]; then
        local pct=$((installed * 100 / total))
        [[ "$pct" -gt 100 ]] && pct=100
        deploy_log "📦 npm progress ~${pct}% (${installed}/${total} top-level dirs) — elapsed $(_deploy_elapsed "$start")"
      else
        deploy_log "📦 npm progress — ${installed} dirs in node_modules — elapsed $(_deploy_elapsed "$start")"
      fi
    done
  ) &
  _NPM_WATCH_PID=$!
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

deploy_run_npm_deps() {
  local start=$SECONDS
  local total rc=0
  total="$(deploy_npm_lockfile_packages package-lock.json)"

  deploy_log "npm install — packages in lockfile: ${total}"
  deploy_log "استراتژی: npmmirror → fallback npmjs (مثل vamyab)"

  export npm_config_progress=true
  export npm_config_loglevel="${NPM_LOGLEVEL:-verbose}"
  export npm_config_fetch_timeout="${NPM_FETCH_TIMEOUT:-600000}"
  export npm_config_fetch_retries="${NPM_FETCH_RETRIES:-5}"

  deploy_start_heartbeat "npm install" 20 "node_modules"
  deploy_start_npm_watch "package-lock.json"

  set +e
  npm config set registry "${NPM_REGISTRY:-https://registry.npmmirror.com}"
  npm install --no-audit --no-fund --loglevel="${NPM_LOGLEVEL:-verbose}" --timing 2>&1 | while IFS= read -r line || [[ -n "$line" ]]; do
    printf '[npm] %s\n' "$line"
  done
  rc=${PIPESTATUS[0]}

  if [[ "$rc" -ne 0 ]]; then
    deploy_log "WARN: npmmirror failed — retry with registry.npmjs.org"
    npm config set registry https://registry.npmjs.org
    npm install --no-audit --no-fund --loglevel="${NPM_LOGLEVEL:-verbose}" --timing 2>&1 | while IFS= read -r line || [[ -n "$line" ]]; do
      printf '[npm] %s\n' "$line"
    done
    rc=${PIPESTATUS[0]}
  fi
  set -e

  deploy_stop_npm_watch
  deploy_stop_heartbeat
  deploy_log "✓ npm install finished in $(_deploy_elapsed "$start") (exit ${rc})"
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
