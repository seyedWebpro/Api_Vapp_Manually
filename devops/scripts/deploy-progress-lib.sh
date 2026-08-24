#!/usr/bin/env bash
# کتابخانه درصد پیشرفت deploy — Vapp (الگو: vamyab deploy-progress-lib.sh)
#
# Usage:
#   source deploy-progress-lib.sh
#   pct=$(compute_deploy_percent "$log" "$front_log")
#   render_progress_bar "$pct"

compute_api_docker_percent() {
  local log="$1"
  local step_line step_n step_t base

  if step_line="$(grep -oE 'Step [0-9]+/[0-9]+' "$log" 2>/dev/null | tail -1)"; then
    step_n="$(echo "$step_line" | sed -n 's/Step \([0-9]*\)\/.*/\1/p')"
    step_t="$(echo "$step_line" | sed -n 's/Step .*\/\([0-9]*\)/\1/p')"
    if [[ -n "$step_n" && -n "$step_t" && "$step_t" -gt 0 ]]; then
      base=$((step_n * 100 / step_t))
      echo "$base"
      return
    fi
  fi

  if grep -q 'Successfully built' "$log" 2>/dev/null && ! grep -q '=== deploy-api done' "$log" 2>/dev/null; then
    echo 90
    return
  fi
  if grep -q 'API health attempt' "$log" 2>/dev/null; then
    echo 96
    return
  fi
  if grep -qE 'Building api|=== deploy-api started' "$log" 2>/dev/null; then
    echo 8
    return
  fi
  echo 0
}

compute_front_percent() {
  local log="$1"
  local step_line step_n step_t

  if [[ ! -f "$log" ]]; then
    echo 0
    return
  fi

  if grep -qE '=== deploy-front(-host)? done ===' "$log" 2>/dev/null; then
    echo 100
    return
  fi
  if grep -q 'FRONT:200\|FRONT (nginx static): HTTP 200' "$log" 2>/dev/null; then
    echo 95
    return
  fi
  if grep -q 'nginx reload\|Applying nginx\|copy static' "$log" 2>/dev/null; then
    echo 88
    return
  fi
  if grep -qE 'vite build|built in|vite v' "$log" 2>/dev/null; then
    echo 75
    return
  fi
  if grep -qE 'npm (ci|install)|added [0-9]+ packages|📦 npm progress' "$log" 2>/dev/null; then
    echo 45
    return
  fi
  if grep -q 'docker run started' "$log" 2>/dev/null; then
    echo 88
    return
  fi
  if grep -q 'Successfully built' "$log" 2>/dev/null; then
    echo 82
    return
  fi
  if step_line="$(grep -oE 'Step [0-9]+/[0-9]+' "$log" 2>/dev/null | tail -1)"; then
    step_n="$(echo "$step_line" | sed -n 's/Step \([0-9]*\)\/.*/\1/p')"
    step_t="$(echo "$step_line" | sed -n 's/Step .*\/\([0-9]*\)/\1/p')"
    if [[ -n "$step_n" && -n "$step_t" && "$step_t" -gt 0 ]]; then
      echo $((step_n * 55 / step_t))
      return
    fi
  fi
  if grep -q 'docker build started' "$log" 2>/dev/null; then
    echo 20
    return
  fi
  if grep -qE 'STEP [0-9]+/|Already up to date|Fast-forward|Updating ' "$log" 2>/dev/null; then
    echo 10
    return
  fi
  if grep -qE '=== deploy-front(-host)? started ===' "$log" 2>/dev/null; then
    echo 5
    return
  fi
  echo 0
}

compute_deploy_percent() {
  local log="$1"
  local front_log="${2:-}"
  local pct=0 marker api_inner front_inner api_pct front_pct flog

  if [[ ! -f "$log" ]]; then
    echo 0
    return
  fi

  if grep -q '=== deploy-server-visible finished' "$log" 2>/dev/null; then
    echo 100
    return
  fi

  if grep -q 'OK: all services healthy' "$log" 2>/dev/null; then
    echo 100
    return
  fi

  marker="$(grep -oE 'PROGRESS:[0-9]+' "$log" 2>/dev/null | tail -1 | sed 's/PROGRESS://' || true)"
  [[ -n "$marker" && "$marker" =~ ^[0-9]+$ ]] && pct="$marker"

  if grep -qE 'Already up to date|Fast-forward|Updating [a-f0-9]|PROGRESS:5' "$log" 2>/dev/null; then
    [[ "$pct" -lt 5 ]] && pct=5
  fi

  if grep -qE 'Building api|=== deploy-api started|PROGRESS:8' "$log" 2>/dev/null; then
    api_inner="$(compute_api_docker_percent "$log")"
    api_pct=$((8 + api_inner * 47 / 100))
    [[ "$api_pct" -gt "$pct" ]] && pct="$api_pct"
  fi

  if grep -q '=== deploy-api done' "$log" 2>/dev/null; then
    [[ "$pct" -lt 55 ]] && pct=55
  fi

  flog="$front_log"
  if [[ -z "$flog" && -f "${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}" ]]; then
    flog="$(cat "${LAST_FRONT_DEPLOY_LOG:-$HOME/.vapp-last-front-deploy.log}" 2>/dev/null || true)"
  fi

  if [[ -n "$flog" && -f "$flog" ]]; then
    front_inner="$(compute_front_percent "$flog")"
    front_pct=$((55 + front_inner * 35 / 100))
    [[ "$front_pct" -gt "$pct" ]] && pct="$front_pct"
  fi

  if grep -qE '=== deploy-front(-host)? done|Front deploy finished|PROGRESS:90' "$log" "$flog" 2>/dev/null; then
    [[ "$pct" -lt 90 ]] && pct=90
  fi

  if grep -qE 'Health check|health-check\.sh|PROGRESS:93' "$log" 2>/dev/null; then
    [[ "$pct" -lt 93 ]] && pct=93
  fi

  if grep -q 'OK: all services healthy' "$log" 2>/dev/null; then
    pct=100
  fi

  [[ "$pct" -gt 100 ]] && pct=100
  echo "$pct"
}

render_progress_bar() {
  local pct="${1:-0}"
  local width=28
  local filled empty bar="" i

  [[ ! "$pct" =~ ^[0-9]+$ ]] && pct=0
  [[ "$pct" -gt 100 ]] && pct=100

  filled=$((pct * width / 100))
  empty=$((width - filled))
  for ((i = 0; i < filled; i++)); do bar+='█'; done
  for ((i = 0; i < empty; i++)); do bar+='░'; done

  if [[ "$pct" -eq 100 ]]; then
    printf '[%s] %3d%% ✓ آپدیت کامل شد\n' "$bar" "$pct"
  else
    printf '[%s] %3d%%\n' "$bar" "$pct"
  fi
}

progress_status_label() {
  local pct="$1"
  local log="${2:-}"
  local flog="${3:-}"

  if [[ "$pct" -ge 100 ]]; then
    echo "تمام — سورس روی سرور آپدیت شد"
    return
  fi
  if [[ "$pct" -ge 93 ]]; then
    echo "تست سلامت API + Front"
    return
  fi
  if [[ "$pct" -ge 55 ]]; then
    if [[ -n "$flog" && -f "$flog" ]] && grep -qE 'vite build|npm run build' "$flog" 2>/dev/null; then
      echo "build فرانت (Vite)"
    elif [[ -n "$flog" && -f "$flog" ]] && grep -qE 'npm install|npm ci' "$flog" 2>/dev/null; then
      echo "نصب npm dependencies"
    else
      echo "deploy فرانت Admin"
    fi
    return
  fi
  if [[ "$pct" -ge 8 ]]; then
    if grep -q 'Successfully built' "$log" 2>/dev/null; then
      echo "start کانتینر API"
    else
      echo "build API — Docker"
    fi
    return
  fi
  if [[ "$pct" -ge 5 ]]; then
    echo "git pull"
    return
  fi
  echo "شروع deploy"
}
