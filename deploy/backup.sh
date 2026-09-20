#!/bin/bash
set -euo pipefail
exec 9>/run/lock/hall-backup.lock
flock -n 9 || exit 0
set -a
source /etc/hall/platform.env
set +a
target="/var/backups/hall/$(date -u +%Y%m%dT%H%M%SZ)"
install -d -m 0700 /var/backups/hall
systemctl stop hall-platform
trap 'systemctl start hall-platform' EXIT
/usr/bin/node /opt/hall/current/scripts/backup.cjs "$target"
chmod -R go-rwx "$target"
# No automatic deletion: establish retention after measuring actual usage.
