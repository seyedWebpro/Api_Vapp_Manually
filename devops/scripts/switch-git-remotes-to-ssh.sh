#!/usr/bin/env bash
# HTTPS → SSH برای git remote
# reuse: URL هر repo (API، front، …)
#
# Usage:
#   bash switch-git-remotes-to-ssh.sh
#   API_REPO_DIR=~/Api_Vapp_Manually FRONT_DIR=~/Admin_Vapp bash switch-git-remotes-to-ssh.sh
set -euo pipefail

API_REPO_DIR="${API_REPO_DIR:-$HOME/Api_Vapp_Manually}"
FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"

to_ssh() {
  local dir="$1"
  local ssh_url="$2"
  [[ -d "$dir/.git" ]] || { echo "SKIP: not a git repo: $dir"; return 0; }
  cd "$dir"
  local current
  current="$(git remote get-url origin 2>/dev/null || true)"
  if [[ "$current" == "$ssh_url" ]]; then
    echo "OK: $dir already SSH"
    return 0
  fi
  git remote set-url origin "$ssh_url"
  echo "OK: $dir → $ssh_url (was: $current)"
}

to_ssh "$API_REPO_DIR" "git@github.com:seyedWebpro/Api_Vapp_Manually.git"
to_ssh "$FRONT_DIR" "git@github.com:seyedWebpro/Admin_Pannel_Vapp.git"

echo ""
echo "Test pull:"
echo "  cd $API_REPO_DIR && git pull origin main"
echo "  cd $FRONT_DIR && git pull origin main"
