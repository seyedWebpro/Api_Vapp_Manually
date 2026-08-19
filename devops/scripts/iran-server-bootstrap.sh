#!/usr/bin/env bash
# نصب اولیه سرور Vapp — یک‌بار (الگوی CaspianEdu روی همین دیتاسنتر)
# میرور ایران‌سرور اعمال نمی‌شود.
# Usage: bash devops/scripts/iran-server-bootstrap.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"

API_REPO_DIR="${API_REPO_DIR:-$REMOTE_API_REPO}"
FRONT_DIR="${FRONT_DIR:-$REMOTE_FRONT_REPO}"
PUBLIC_DIR="${PUBLIC_DIR:-$REMOTE_PUBLIC_REPO}"

echo "=== iran-server-bootstrap $(date -Is) ==="
echo "SERVER_IP=$SERVER_IP"

export DEBIAN_FRONTEND=noninteractive
# میرور آلمانی پیش‌فرض بعضی VPS از ایران timeout است — مثل CaspianEdu از archive.ubuntu.com
sed -i 's|http://de.archive.ubuntu.com/ubuntu|http://archive.ubuntu.com/ubuntu|g' /etc/apt/sources.list 2>/dev/null || true
find /etc/apt/sources.list.d -name '*.list' -exec sed -i 's|http://de.archive.ubuntu.com/ubuntu|http://archive.ubuntu.com/ubuntu|g' {} + 2>/dev/null || true
apt-get update
apt-get install -y ca-certificates curl gnupg git nginx ufw

if ! command -v docker >/dev/null 2>&1; then
  apt-get install -y docker.io docker-compose
  systemctl enable --now docker
fi

# اگر leftover میرور ایران‌سرور باشد پاک شود
if [[ -f /etc/docker/daemon.json ]] && grep -q 'docker.iranserver.com' /etc/docker/daemon.json 2>/dev/null; then
  bash "$SCRIPT_DIR/clear-iranserver-mirrors.sh"
fi

mkdir -p "$API_REPO_DIR/wwwroot/uploads" "$API_REPO_DIR/log" \
  "$API_REPO_DIR/backups/daily" "$API_REPO_DIR/backups/weekly" "$API_REPO_DIR/backups/logs" \
  "$API_REPO_DIR/secrets"
chmod -R 755 "$API_REPO_DIR/wwwroot/uploads" "$API_REPO_DIR/backups" "$API_REPO_DIR/log" 2>/dev/null || true

ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable || true

echo ""
echo "=== GitHub SSH (روی سرور) ==="
echo "bash $API_REPO_DIR/devops/scripts/setup-github-deploy-key.sh"
echo "سپس کلید public را در GitHub → Settings → SSH keys بگذارید"
echo ""
echo "git clone git@github.com:seyedWebpro/Api_Vapp_Manually.git $API_REPO_DIR"
echo "git clone git@github.com:seyedWebpro/Admin_Pannel_Vapp.git $FRONT_DIR"
echo "git clone git@github.com:seyedWebpro/PublicWeb_Vapp.git $PUBLIC_DIR"
echo ""
echo "سپس:"
echo "  SERVER_IP=$SERVER_IP bash $API_REPO_DIR/devops/scripts/bootstrap-first-run.sh"
echo "  bash $API_REPO_DIR/vapp-iran-update.sh --test"
echo "  bash $API_REPO_DIR/vapp-iran-update.sh --full"
