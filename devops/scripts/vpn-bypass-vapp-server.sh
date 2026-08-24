#!/usr/bin/env bash
# Split tunnel: Vapp server IP مستقیم از اینترنت (بدون VPN) — Cursor + SSH همزمان
#
# Usage:
#   bash devops/scripts/vpn-bypass-vapp-server.sh          # اعمال route + تست SSH
#   bash devops/scripts/vpn-bypass-vapp-server.sh --test   # فقط تست (بدون sudo)
#   bash devops/scripts/vpn-bypass-vapp-server.sh --status # وضعیت route و VPN
#
# بعد از هر reconnect OpenVPN اگر route از profile اعمال نشد، این اسکریپت را بزنید.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"

SERVER_IP="${SERVER_IP:-195.24.237.132}"
SSH_HOST="${SSH_HOST:-vapp-prod}"
MODE="${1:-apply}"

local_gateway() {
  route -n get default 2>/dev/null | awk '/gateway:/{print $2; exit}'
}

vpn_interface() {
  ifconfig 2>/dev/null | awk '/^utun[0-9]+:/{iface=$1; sub(/:$/,"",iface)} /inet .* --> / && iface {print iface; exit}'
}

route_status() {
  route -n get "$SERVER_IP" 2>/dev/null || true
}

is_bypassed() {
  route -n get "$SERVER_IP" 2>/dev/null | grep -q 'interface: en0'
}

apply_routes() {
  local gw
  gw="$(local_gateway)"
  if [[ -z "$gw" ]]; then
    echo "ERROR: local gateway (en0) not found — Wi-Fi/Ethernet connected?" >&2
    return 1
  fi

  local cmd="route delete -host ${SERVER_IP} 2>/dev/null; route add -host ${SERVER_IP} ${gw}"
  echo "Applying bypass: ${SERVER_IP} → ${gw} (en0, not VPN)"

  if [[ "$(id -u)" -eq 0 ]]; then
    eval "$cmd"
  elif command -v osascript >/dev/null 2>&1; then
    osascript -e "do shell script \"${cmd}\" with administrator privileges"
  else
    echo "Run with sudo:" >&2
    echo "  sudo $cmd" >&2
    return 1
  fi
}

test_ssh() {
  echo "Testing SSH → ${SSH_HOST}..."
  if ssh -o BatchMode=yes -o ConnectTimeout=12 -o StrictHostKeyChecking=accept-new "$SSH_HOST" 'echo SSH_OK'; then
    echo "OK: SSH works with VPN on"
    return 0
  fi
  echo "FAIL: SSH still unreachable" >&2
  return 1
}

test_health() {
  echo "Testing health → http://${SERVER_IP}/health ..."
  curl -sS -m10 -o /dev/null -w 'health HTTP %{http_code}\n' "http://${SERVER_IP}/health" || true
}

case "$MODE" in
  --status)
    echo "Server IP: $SERVER_IP"
    echo "VPN interface: $(vpn_interface || echo none)"
    echo "Local gateway: $(local_gateway || echo unknown)"
    echo "--- route ---"
    route_status
    if is_bypassed; then
      echo "Status: BYPASSED (direct via en0)"
    else
      echo "Status: THROUGH VPN (SSH will likely timeout)"
    fi
    ;;
  --test)
    route_status
    test_ssh
    test_health
    ;;
  apply|"")
    apply_routes
    sleep 1
    route_status
    test_ssh
    test_health
    ;;
  *)
    echo "Usage: $0 [--status|--test|apply]" >&2
    exit 1
    ;;
esac
