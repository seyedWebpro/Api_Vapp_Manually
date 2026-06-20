#!/usr/bin/env bash
BACKUP_KIND=weekly exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/backup-database.sh" "$@"
