#!/usr/bin/env bash
# بکاپ هفتگی (همان اسکریپت روزانه با BACKUP_KIND=weekly)
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export BACKUP_KIND=weekly
exec bash "$SCRIPT_DIR/backup-database.sh" "$@"
