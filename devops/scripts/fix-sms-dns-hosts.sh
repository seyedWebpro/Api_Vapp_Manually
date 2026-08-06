#!/usr/bin/env bash
# Fix intermittent DNS for irannovinsms.ir when VPN breaks resolution on macOS.
# Usage: bash devops/scripts/fix-sms-dns-hosts.sh

set -euo pipefail

DOMAIN="irannovinsms.ir"
IPS=("185.143.233.238" "185.143.234.238")

TMP="$(mktemp)"
grep -v "${DOMAIN}" /etc/hosts > "$TMP" || true
{
  echo ""
  echo "# Iran Novin SMS — bypass VPN DNS issues ($(date +%Y-%m-%d))"
  for ip in "${IPS[@]}"; do
    echo "${ip} ${DOMAIN}"
  done
} >> "$TMP"

echo "Will write these lines into /etc/hosts:"
grep "${DOMAIN}" "$TMP" || true
echo
sudo cp "$TMP" /etc/hosts
rm -f "$TMP"
sudo dscacheutil -flushcache
sudo killall -HUP mDNSResponder 2>/dev/null || true

echo "OK — verifying:"
python3 -c "import socket; print(socket.gethostbyname('${DOMAIN}'))"
curl -sS --connect-timeout 5 -o /dev/null -w "HTTPS %{http_code} in %{time_total}s\n" "https://${DOMAIN}/" || true
echo "Restart dotnet watch after this."
