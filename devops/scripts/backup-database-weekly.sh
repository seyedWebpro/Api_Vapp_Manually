#!/usr/bin/env bash
# wrapper هفتگی — همان backup-database.sh با BACKUP_KIND=weekly
BACKUP_KIND=weekly exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/backup-database.sh" "$@"
