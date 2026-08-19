#!/usr/bin/env bash
# source این فایل — بارگذاری devops/server.conf
_LOAD_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
_CONF="$(cd "$_LOAD_DIR/../.." && pwd)/server.conf"
if [[ -f "$_CONF" ]]; then
  # shellcheck disable=SC1090
  set -a
  # shellcheck source=/dev/null
  source "$_CONF"
  set +a
fi
: "${SERVER_IP:=195.24.237.132}"
: "${SSH_PORT:=22}"
: "${SSH_HOST:=vapp-prod}"
: "${SSH_USER:=root}"
: "${DOMAIN:=ok-sms.ir}"
: "${REMOTE_API_REPO:=/root/Api_Vapp_Manually}"
: "${REMOTE_FRONT_REPO:=/root/Admin_Vapp}"
: "${REMOTE_PUBLIC_REPO:=/root/Public_Vapp}"
: "${REMOTE_SCRAPER_REPO:=/root/scraping_Number_Vapp}"
: "${API_BRANCH:=main}"
: "${FRONT_GIT_BRANCH:=main}"
: "${PUBLIC_GIT_BRANCH:=main}"
: "${SCRAPER_GIT_BRANCH:=main}"
