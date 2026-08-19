#!/usr/bin/env bash
# SSH key روی Mac → ورود به سرور Vapp
#
# Usage:
#   bash devops/scripts/setup-local-ssh-to-server.sh
#   bash devops/scripts/setup-local-ssh-to-server.sh --force   # بازنویسی Host در ~/.ssh/config
#
# Env overrides:
#   SERVER=root@195.24.237.132 SSH_PORT=22
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"

SERVER="${SERVER:-${SSH_USER}@${SERVER_IP}}"
SSH_PORT="${SSH_PORT:-22}"
KEY_PATH="${KEY_PATH:-$HOME/.ssh/id_ed25519_vapp_server}"
HOST_ALIAS="${HOST_ALIAS:-$SSH_HOST}"
FORCE=0
[[ "${1:-}" == "--force" ]] && FORCE=1

mkdir -p "$HOME/.ssh"
chmod 700 "$HOME/.ssh"
mkdir -p "$HOME/.ssh/sockets" 2>/dev/null || true

if [[ -f "$KEY_PATH" ]]; then
  echo "OK: key exists: $KEY_PATH"
else
  echo "Creating ED25519 key..."
  ssh-keygen -t ed25519 -C "vapp-mac@$(whoami)-$(date +%Y%m%d)" -f "$KEY_PATH" -N ""
  chmod 600 "$KEY_PATH"
fi

PUB="$(cat "${KEY_PATH}.pub")"
HOST_IP="${SERVER#*@}"
HOST_USER="${SERVER%@*}"

MARKER="# vapp-server-ssh"
CONFIG="$HOME/.ssh/config"
touch "$CONFIG"
chmod 600 "$CONFIG"

NEW_BLOCK=$(cat <<EOF
Host $HOST_ALIAS
  HostName $HOST_IP
  Port $SSH_PORT
  User $HOST_USER
  IdentityFile $KEY_PATH
  IdentitiesOnly yes
  ServerAliveInterval 30
  ServerAliveCountMax 6
  ConnectTimeout 20
  $MARKER
EOF
)

python3 - "$CONFIG" "$MARKER" "$NEW_BLOCK" "$HOST_ALIAS" "$FORCE" "$HOST_IP" "$SSH_PORT" <<'PY'
import re, sys
from pathlib import Path
path, marker, block, alias, force, host_ip, ssh_port = Path(sys.argv[1]), sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6], sys.argv[7]
text = path.read_text() if path.exists() else ""
pat = re.compile(rf"(?ms)^Host {re.escape(alias)}\n.*?(?=^Host |\Z)")
matches = list(pat.finditer(text))
need = force == "1" or f"HostName {host_ip}" not in text or f"Port {ssh_port}" not in text or not matches
if matches:
    if need:
        # keep first match replacement, drop duplicate Host blocks
        first = True
        def repl(m):
            global first
            if first:
                first = False
                return block.rstrip() + "\n\n"
            return ""
        text = pat.sub(repl, text)
        path.write_text(text)
        print(f"OK: updated Host {alias} in", path)
    else:
        print(f"OK: Host {alias} already points to {host_ip}:{ssh_port}")
else:
    path.write_text(text.rstrip() + "\n\n" + block + "\n")
    print(f"OK: added Host {alias} → {host_ip}:{ssh_port}")
PY

echo ""
echo "================================================================================"
echo "  PUBLIC KEY — روی سرور در authorized_keys (از کنسول وب اگر SSH timeout)"
echo "================================================================================"
echo "$PUB"
echo ""
echo "روی سرور یک‌خطی:"
echo "  mkdir -p ~/.ssh && chmod 700 ~/.ssh && echo '$PUB' >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys && echo KEY_ADDED"
echo ""
echo "از Mac (بدون فیلترشکن):"
echo "  ssh-copy-id -p $SSH_PORT -i ${KEY_PATH}.pub $SERVER"
echo "  ssh $HOST_ALIAS 'echo SSH_OK'"
echo ""
echo "Deploy از Mac:"
echo "  SERVER=$HOST_ALIAS bash devops/scripts/deploy-from-mac.sh all"
