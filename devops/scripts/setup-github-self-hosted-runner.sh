#!/usr/bin/env bash
# Install GitHub Actions self-hosted runner on Vapp VPS (Iran-only SSH workaround).
#
# One runner on the VPS serves all Vapp repos (org-level registration recommended).
# Deploy jobs use: runs-on: [self-hosted, vapp-prod]
#
# Usage (on VPS as root — copy script or pipe from Mac):
#   bash devops/scripts/setup-github-self-hosted-runner.sh --token 'XXXXX'
#
# Get token (valid ~1 hour):
#   Org (recommended — all 4 repos):
#     https://github.com/organizations/seyedWebpro/settings/actions/runners/new
#   Single repo fallback:
#     https://github.com/seyedWebpro/Api_Vapp_Manually/settings/actions/runners/new
#
# Options:
#   --token TOKEN     (required) registration token from GitHub UI
#   --org ORG         default: seyedWebpro
#   --repo REPO       use repo-level instead of org (e.g. Api_Vapp_Manually)
#   --name NAME       default: vapp-prod
#   --labels LABELS   default: vapp-prod
#   --dir PATH        default: /opt/actions-runner-vapp
#   --status          show service status
#   --uninstall       stop and remove runner service
#
# After install:
#   systemctl status actions.runner.seyedWebpro-vapp-prod.service
#   journalctl -u actions.runner.* -f
set -euo pipefail

ORG="${GITHUB_ORG:-seyedWebpro}"
REPO="${GITHUB_REPO:-}"
RUNNER_NAME="${RUNNER_NAME:-vapp-prod}"
RUNNER_LABELS="${RUNNER_LABELS:-vapp-prod}"
RUNNER_DIR="${RUNNER_DIR:-/opt/actions-runner-vapp}"
TOKEN=""
MODE="install"

usage() {
  sed -n '2,28p' "$0" | sed 's/^# \?//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help) usage 0 ;;
    --token) TOKEN="${2:-}"; shift 2 ;;
    --org) ORG="${2:-}"; shift 2 ;;
    --repo) REPO="${2:-}"; shift 2 ;;
    --name) RUNNER_NAME="${2:-}"; shift 2 ;;
    --labels) RUNNER_LABELS="${2:-}"; shift 2 ;;
    --dir) RUNNER_DIR="${2:-}"; shift 2 ;;
    --status) MODE=status; shift ;;
    --uninstall) MODE=uninstall; shift ;;
    *) echo "Unknown option: $1" >&2; usage 1 ;;
  esac
done

runner_service_glob() {
  systemctl list-units --type=service --all 'actions.runner.*' 2>/dev/null \
    | awk '/actions\.runner\./ {print $1}' | head -1 || true
}

if [[ "$MODE" == "status" ]]; then
  svc="$(runner_service_glob)"
  if [[ -n "$svc" ]]; then
    systemctl status "$svc" --no-pager || true
  else
    echo "No actions.runner.* service found."
    [[ -d "$RUNNER_DIR" ]] && "$RUNNER_DIR/run.sh" --check --once 2>/dev/null || true
  fi
  exit 0
fi

if [[ "$MODE" == "uninstall" ]]; then
  if [[ -d "$RUNNER_DIR" ]]; then
    cd "$RUNNER_DIR"
    if [[ -f ./svc.sh ]]; then
      ./svc.sh stop 2>/dev/null || true
      ./svc.sh uninstall 2>/dev/null || true
    fi
    if [[ -f ./config.sh ]] && [[ -f .runner ]]; then
      RUNNER_ALLOW_RUNASROOT=1 ./config.sh remove --token "${TOKEN:-dummy}" 2>/dev/null \
        || echo "WARN: remove needs fresh --token from GitHub UI if registration stuck"
    fi
  fi
  echo "Runner removed from $RUNNER_DIR (directory kept — delete manually if needed)."
  exit 0
fi

if [[ -z "$TOKEN" ]]; then
  echo "ERROR: --token required (GitHub → Settings → Actions → Runners → New)" >&2
  usage 1
fi

if [[ "$(id -u)" -ne 0 ]]; then
  echo "ERROR: run as root on the VPS" >&2
  exit 1
fi

# Prerequisites
for cmd in curl tar gzip; do
  command -v "$cmd" >/dev/null || { echo "ERROR: missing $cmd" >&2; exit 1; }
done
if ! command -v docker >/dev/null; then
  echo "WARN: docker not in PATH — API/scraper deploy jobs need docker"
fi

if [[ -n "$REPO" ]]; then
  RUNNER_URL="https://github.com/${ORG}/${REPO}"
else
  RUNNER_URL="https://github.com/${ORG}"
fi

RUNNER_VERSION="${RUNNER_VERSION:-}"
if [[ -z "$RUNNER_VERSION" ]]; then
  RUNNER_VERSION="$(curl -fsSL https://api.github.com/repos/actions/runner/releases/latest \
    | grep -Eo '"tag_name": "v[^"]+"' | head -1 | cut -d'"' -f4 | sed 's/^v//')"
fi
ARCH="x64"
TARBALL="actions-runner-linux-${ARCH}-${RUNNER_VERSION}.tar.gz"
DOWNLOAD_URL="https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/${TARBALL}"

echo "=== Vapp self-hosted runner ==="
echo "URL:     $RUNNER_URL"
echo "Name:    $RUNNER_NAME"
echo "Labels:  $RUNNER_LABELS"
echo "Dir:     $RUNNER_DIR"
echo "Version: $RUNNER_VERSION"

mkdir -p "$RUNNER_DIR"
cd "$RUNNER_DIR"

if [[ ! -f ./config.sh ]]; then
  echo "Downloading runner $RUNNER_VERSION..."
  curl -fsSL -o "$TARBALL" "$DOWNLOAD_URL"
  tar xzf "$TARBALL"
  rm -f "$TARBALL"
fi

if [[ -f .runner ]]; then
  echo "Runner already configured in $RUNNER_DIR — reinstall service only"
else
  RUNNER_ALLOW_RUNASROOT=1 ./config.sh \
    --url "$RUNNER_URL" \
    --token "$TOKEN" \
    --name "$RUNNER_NAME" \
    --labels "$RUNNER_LABELS" \
    --unattended \
    --replace
fi

./svc.sh install
./svc.sh start

echo ""
echo "✓ Self-hosted runner installed."
echo "  GitHub → Settings → Actions → Runners → should show '$RUNNER_NAME' (Idle)"
echo "  Logs: journalctl -u 'actions.runner.*' -f"
echo "  Workflows deploy job: runs-on: [self-hosted, vapp-prod]"
