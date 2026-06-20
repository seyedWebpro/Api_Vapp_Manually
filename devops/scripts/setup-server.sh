#!/usr/bin/env bash
# نصب پایه سرور — Docker، Nginx، UFW (بدون build پروژه)
# reuse: پورت ufw، مسیر nginx example، API_REPO_DIR
# Usage: sudo bash setup-server.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
SERVER_IP="${SERVER_IP:-185.116.162.233}"

if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  echo "Run with sudo: sudo bash $0" >&2
  exit 1
fi

echo "=== Vapp server setup $(date -Is) ==="

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq curl git nginx ufw ca-certificates gnupg lsb-release python3

if ! command -v docker >/dev/null 2>&1; then
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -qq
  apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  systemctl enable --now docker
fi

ufw allow OpenSSH 2>/dev/null || ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
echo "y" | ufw enable 2>/dev/null || true

SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/apply-nginx.sh"

mkdir -p "$API_REPO_DIR/wwwroot/uploads"
mkdir -p "$API_REPO_DIR/log"
mkdir -p "$API_REPO_DIR/backups/daily" "$API_REPO_DIR/backups/weekly" "$API_REPO_DIR/backups/logs"
chmod +x "$API_REPO_DIR/devops/scripts/"*.sh 2>/dev/null || true

echo ""
echo "=== Setup complete ==="
echo "Server IP: $SERVER_IP"
echo "Next steps:"
echo "  1. Clone API to $API_REPO_DIR and Admin to ~/Admin_Vapp"
echo "  2. cp $API_REPO_DIR/devops/.env.server.example $API_REPO_DIR/docker/.env"
echo "  3. Edit docker/.env (SA_PASSWORD, Jwt__Secret)"
echo "  4. bash $API_REPO_DIR/devops/scripts/deploy-server.sh --full --wait"
echo "  5. Open http://$SERVER_IP/admin"
