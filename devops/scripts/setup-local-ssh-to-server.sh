#!/usr/bin/env bash
# SSH key روی Mac → ورود به VPS
# reuse: SERVER، KEY_PATH، Host alias در ~/.ssh/config
#
# Usage (روی Mac لوکال):
#   SERVER=root@185.116.162.233 bash devops/scripts/setup-local-ssh-to-server.sh
#
# Env:
#   SERVER, KEY_PATH
set -euo pipefail

SERVER="${SERVER:-root@185.116.162.233}"
KEY_PATH="${KEY_PATH:-$HOME/.ssh/id_ed25519_vapp_server}"

mkdir -p "$HOME/.ssh"
chmod 700 "$HOME/.ssh"

if [[ -f "$KEY_PATH" ]]; then
  echo "OK: key exists: $KEY_PATH"
else
  echo "Creating ED25519 key for server login..."
  ssh-keygen -t ed25519 -C "vapp-mac@$(whoami)-$(date +%Y%m%d)" -f "$KEY_PATH" -N ""
  chmod 600 "$KEY_PATH"
  echo "OK: created $KEY_PATH"
fi

PUB="$(cat "${KEY_PATH}.pub")"
HOST_ALIAS="vapp-prod"
HOST_IP="${SERVER#*@}"
HOST_USER="${SERVER%@*}"

# ~/.ssh/config entry
MARKER="# vapp-server-ssh"
CONFIG="$HOME/.ssh/config"
if ! grep -q "$MARKER" "$CONFIG" 2>/dev/null; then
  cat >>"$CONFIG" <<EOF

Host $HOST_ALIAS
  HostName $HOST_IP
  User $HOST_USER
  IdentityFile $KEY_PATH
  IdentitiesOnly yes
  $MARKER
EOF
  chmod 600 "$CONFIG"
  echo "OK: added Host $HOST_ALIAS to ~/.ssh/config"
  echo "     Connect with: ssh $HOST_ALIAS"
fi

echo ""
echo "================================================================================"
echo "  PUBLIC KEY (Mac → Server)"
echo "================================================================================"
echo "$PUB"
echo ""
echo "Option A — ssh-copy-id (if password login works):"
echo "  ssh-copy-id -i ${KEY_PATH}.pub $SERVER"
echo ""
echo "Option B — paste manually on server (VPS console):"
echo "  mkdir -p ~/.ssh && chmod 700 ~/.ssh"
echo "  echo '$PUB' >> ~/.ssh/authorized_keys"
echo "  chmod 600 ~/.ssh/authorized_keys"
echo ""
echo "Test:"
echo "  ssh -i $KEY_PATH $SERVER 'echo OK'"
echo "  ssh $HOST_ALIAS 'echo OK'"
