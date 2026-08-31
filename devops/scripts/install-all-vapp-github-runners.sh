#!/usr/bin/env bash
# Install self-hosted runners for all four Vapp repos on this VPS.
# Run from Mac (needs gh auth + ssh vapp-prod) OR on VPS after gh login.
#
# Usage:
#   bash devops/scripts/install-all-vapp-github-runners.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SETUP="${SCRIPT_DIR}/setup-github-self-hosted-runner.sh"
REMOTE="${REMOTE:-vapp-prod}"
REMOTE_SETUP="/root/Api_Vapp_Manually/devops/scripts/setup-github-self-hosted-runner.sh"

install_one() {
  local repo="$1" short="$2"
  echo "=== $repo → vapp-prod-${short} ==="
  local token
  token=$(gh api -X POST "/repos/seyedWebpro/${repo}/actions/runners/registration-token" --jq .token)
  if [[ "${RUN_LOCAL:-0}" == "1" ]]; then
    bash "$SETUP" --repo "$repo" --token "$token" --name "vapp-prod-${short}" \
      --dir "/opt/actions-runner-vapp-${short}" --labels vapp-prod
  else
    ssh "$REMOTE" "bash ${REMOTE_SETUP} --repo ${repo} --token '${token}' --name vapp-prod-${short} \
      --dir /opt/actions-runner-vapp-${short} --labels vapp-prod"
  fi
}

for pair in \
  "Api_Vapp_Manually:api" \
  "Admin_Pannel_Vapp:admin" \
  "PublicWeb_Vapp:public" \
  "scraping_Number_Vapp:scraper"; do
  install_one "${pair%%:*}" "${pair##*:}"
done

echo "✓ All runners installed. Check GitHub → each repo → Settings → Actions → Runners"
