#!/usr/bin/env bash
# ساخت SSH Deploy Key روی سرور برای git pull از GitHub
#
# Usage (روی سرور):
#   bash ~/Api_Vapp_Manually/devops/scripts/setup-github-deploy-key.sh
#
# بعد از اجرا: محتوای ~/.ssh/id_ed25519_vapp_github.pub را در GitHub اضافه کنید
# (راهنما: devops/GITHUB_SSH.md)
set -euo pipefail

KEY_NAME="${KEY_NAME:-id_ed25519_vapp_github}"
KEY_PATH="$HOME/.ssh/$KEY_NAME"
SSH_CONFIG="$HOME/.ssh/config"
GITHUB_HOST="${GITHUB_HOST:-github.com}"

mkdir -p "$HOME/.ssh"
chmod 700 "$HOME/.ssh"

if [[ -f "$KEY_PATH" ]]; then
  echo "OK: key already exists: $KEY_PATH"
else
  echo "Creating ED25519 key (no passphrase — for automated deploy on server)..."
  ssh-keygen -t ed25519 -C "vapp-server@$(hostname)-$(date +%Y%m%d)" -f "$KEY_PATH" -N ""
  chmod 600 "$KEY_PATH"
  chmod 644 "${KEY_PATH}.pub"
  echo "OK: created $KEY_PATH"
fi

# ssh config block for GitHub
MARKER="# vapp-github-deploy-key"
if ! grep -q "$MARKER" "$SSH_CONFIG" 2>/dev/null; then
  cat >>"$SSH_CONFIG" <<EOF

Host $GITHUB_HOST
  HostName $GITHUB_HOST
  User git
  IdentityFile $KEY_PATH
  IdentitiesOnly yes
  $MARKER
EOF
  chmod 600 "$SSH_CONFIG"
  echo "OK: appended GitHub block to $SSH_CONFIG"
else
  echo "OK: GitHub block already in $SSH_CONFIG"
fi

echo ""
echo "================================================================================"
echo "  PUBLIC KEY — copy everything below and add to GitHub"
echo "================================================================================"
echo ""
cat "${KEY_PATH}.pub"
echo ""
echo "================================================================================"
echo "  GitHub → Settings → SSH and GPG keys → New SSH key"
echo "  Title: vapp-server-$(hostname)"
echo "  Key type: Authentication Key"
echo "  Paste the line above"
echo "================================================================================"
echo ""
echo "Test (after adding to GitHub):"
echo "  ssh -T git@github.com"
echo ""
echo "Clone repos (SSH — after key is added):"
echo "  git clone git@github.com:seyedWebpro/Api_Vapp_Manually.git ~/Api_Vapp_Manually"
echo "  git clone git@github.com:seyedWebpro/Admin_Pannel_Vapp.git ~/Admin_Vapp"
echo ""
echo "Switch existing HTTPS remotes to SSH:"
echo "  bash $(dirname "$0")/switch-git-remotes-to-ssh.sh"
