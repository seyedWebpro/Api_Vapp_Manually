#!/usr/bin/env bash
# دانلود آخرین بکاپ از سرور به Mac (نسخهٔ offsite محلی)
# از Mac:
#   bash devops/scripts/download-backup-to-mac.sh
#   bash devops/scripts/download-backup-to-mac.sh --dest ~/Backups/vapp
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
if [[ -f "$SCRIPT_DIR/lib/load-server-conf.sh" ]]; then
  # shellcheck disable=SC1091
  source "$SCRIPT_DIR/lib/load-server-conf.sh"
fi

SERVER="${SERVER:-${SSH_HOST:-vapp-prod}}"
DEST="${DEST:-$HOME/Downloads/vapp-db-backup-$(date +%Y%m%d)}"
REMOTE_API="${REMOTE_API_REPO:-/root/Api_Vapp_Manually}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dest) DEST="$2"; shift 2 ;;
    --server) SERVER="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: $0 [--dest DIR] [--server HOST]"
      exit 0
      ;;
    *) echo "Unknown: $1" >&2; exit 1 ;;
  esac
done

echo "SSH: $SERVER"
BAK="$(ssh -o BatchMode=yes -o ConnectTimeout=15 "$SERVER" "readlink -f $REMOTE_API/backups/latest.bak")"
[[ -n "$BAK" ]] || { echo "ERROR: no latest.bak on server" >&2; exit 1; }

mkdir -p "$DEST"
echo "Downloading: $BAK → $DEST/"
scp -o BatchMode=yes "$SERVER:$BAK" "$SERVER:${BAK}.sha256" "$DEST/" 2>/dev/null || {
  scp "$SERVER:$BAK" "$DEST/"
  scp "$SERVER:${BAK}.sha256" "$DEST/" || true
}
# manifest اختیاری
scp -o BatchMode=yes "$SERVER:${BAK%.bak}.json" "$DEST/" 2>/dev/null || true

cd "$DEST"
if ls ./*.sha256 >/dev/null 2>&1; then
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum -c ./*.sha256
  else
    shasum -a 256 -c ./*.sha256
  fi
fi
ls -lh "$DEST"
echo "OK: offsite copy at $DEST"
echo "$DEST"
