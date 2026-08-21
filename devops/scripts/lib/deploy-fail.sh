#!/usr/bin/env bash
# Structured failure helper for deploy scripts (source, do not execute).
# Usage:
#   source lib/deploy-fail.sh
#   deploy_fail "DbVapp missing" "bash devops/scripts/ensure-dbvapp.sh --restart-api --wait"
#
# Or multi-line next steps:
#   deploy_fail_box "API AppVersion=500" <<'EOF'
#   1) bash devops/scripts/ensure-dbvapp.sh --restart-api --wait
#   2) bash devops/scripts/diagnose-deploy.sh
#   EOF

deploy_fail() {
  local reason="$1"
  shift || true
  {
    echo ""
    echo "╔══════════════════════════════════════════════════════════════╗"
    echo "║  DEPLOY FAILED                                               ║"
    echo "╚══════════════════════════════════════════════════════════════╝"
    echo "REASON: $reason"
    if [[ $# -gt 0 ]]; then
      echo ""
      echo "NEXT:"
      local i=1
      for step in "$@"; do
        echo "  $i) $step"
        i=$((i + 1))
      done
    fi
    echo ""
    echo "Full diagnose: bash ~/Api_Vapp_Manually/devops/scripts/diagnose-deploy.sh"
    echo ""
  } >&2
  return 1
}

deploy_fail_box() {
  local reason="$1"
  {
    echo ""
    echo "╔══════════════════════════════════════════════════════════════╗"
    echo "║  DEPLOY FAILED                                               ║"
    echo "╚══════════════════════════════════════════════════════════════╝"
    echo "REASON: $reason"
    echo ""
    echo "NEXT:"
    sed 's/^/  /'
    echo ""
    echo "Full diagnose: bash ~/Api_Vapp_Manually/devops/scripts/diagnose-deploy.sh"
    echo ""
  } >&2
  return 1
}

deploy_ok_box() {
  {
    echo ""
    echo "✓ OK: $*"
    echo ""
  }
}
