# بکاپ خودکار DbVapp (SQL Server / Docker)

سطح نسبت به Microless: همان پایهٔ حرفه‌ای + **health-check روزانه** + **status.json** + **webhook اختیاری**.

## چه کار می‌کند

| مرحله | توضیح |
|--------|--------|
| FULL backup | `BACKUP DATABASE` با `COMPRESSION` + `CHECKSUM` |
| Verify | `RESTORE VERIFYONLY` روی همان فایل |
| Integrity | فایل `SHA256` روی host |
| Manifest | JSON با اندازه، مدت، نسخه SQL |
| Status | `backups/status.json` بعد از هر اجرا |
| Retention | روزانه ۱۴ روز + هفتگی ۸ نسخه |
| Cloud (offsite) | آپلود `rclone` + verify اندازه + retention ابری |
| Lock | جلوگیری از اجرای همزمان |
| Disk check | فضای آزاد ≥ ۲× اندازه DB |
| Permissions | پوشه‌ها `700`، فایل‌ها `600` |
| Health cron | سن latest + checksum + status |
| Notify | وب‌هوک اختیاری روی failure (و success اگر فعال باشد) |

## مسیر فایل‌ها روی سرور

```
~/Api_Vapp_Manually/backups/
  daily/     DbVapp_full_YYYYMMDD_HHMMSS.bak (+ .sha256 + .json)
  weekly/    ...
  logs/      backup-*.log , cron.log , health-check.log
  latest.bak -> آخرین بکاپ
  status.json
  health-status.json
```

Volume در `docker/docker-compose.production.yml`: `../backups:/backups`

## Disaster Recovery

راهنمای کامل: **[DISASTER_RECOVERY.md](./DISASTER_RECOVERY.md)**

```bash
# صحت کامل آخرین بکاپ (SHA + health + restore تست)
bash ~/Api_Vapp_Manually/devops/scripts/verify-backup-set.sh

# کپی offsite به Mac
bash devops/scripts/download-backup-to-mac.sh
```

## نصب سریع (روی سرور)

```bash
cd ~/Api_Vapp_Manually && \
  chmod +x devops/scripts/*.sh && \
  bash devops/scripts/install-db-backup-cron.sh && \
  bash devops/scripts/backup-database.sh && \
  bash devops/scripts/backup-health-check.sh
```

از Mac (بعد از sync اسکریپت‌ها):

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
rsync -avz devops/scripts/backup*.sh devops/scripts/install-db-backup-cron.sh \
  devops/scripts/restore-database.sh devops/scripts/test-restore-database.sh \
  devops/scripts/verify-rclone-offsite.sh devops/scripts/list-offsite-backups.sh \
  devops/backup/ \
  vapp-prod:/root/Api_Vapp_Manually/devops/
```

## زمان cron (UTC)

| job | زمان | تقریبی تهران |
|-----|------|--------------|
| روزانه | `03:00` | ~`06:30` |
| هفتگی (یکشنبه) | `03:15` | ~`06:45` |
| health-check | `05:30` | ~`09:00` |

```bash
crontab -l | grep vapp-db-backup
```

## چک سریع

```bash
BAK=$(readlink -f ~/Api_Vapp_Manually/backups/latest.bak)
ls -lh "$BAK" "${BAK}.sha256" "${BAK%.bak}.json"
(cd "$(dirname "$BAK")" && sha256sum -c "$(basename "$BAK").sha256")
bash ~/Api_Vapp_Manually/devops/scripts/backup-health-check.sh
cat ~/Api_Vapp_Manually/backups/status.json
```

## تست restore واقعی (DB اصلی دست نمی‌خورد)

```bash
BAK="$(readlink -f ~/Api_Vapp_Manually/backups/latest.bak)" && \
bash ~/Api_Vapp_Manually/devops/scripts/test-restore-database.sh --file "$BAK"
```

## بازیابی اضطراری (جایگزین DbVapp)

```bash
bash ~/Api_Vapp_Manually/devops/scripts/restore-database.sh \
  --file ~/Api_Vapp_Manually/backups/daily/DbVapp_full_YYYYMMDD_HHMMSS.bak \
  --confirm RESTORE
```

## Offsite با rclone (اختیاری)

```bash
sudo apt-get install -y rclone
rclone config   # مثلاً remote: vapp-backups
cp devops/backup/backup.env.example devops/backup/backup.env
chmod 600 devops/backup/backup.env
# RCLONE_REMOTE=vapp-backups:DbVapp
bash devops/scripts/verify-rclone-offsite.sh
bash devops/scripts/backup-database.sh
bash devops/scripts/list-offsite-backups.sh
```

## دانلود بکاپ به Mac

```bash
BAK=$(ssh vapp-prod 'readlink -f ~/Api_Vapp_Manually/backups/latest.bak')
DEST=~/Downloads/vapp-db-backup-$(date +%Y%m%d)
mkdir -p "$DEST"
scp "vapp-prod:$BAK" "vapp-prod:${BAK}.sha256" "$DEST/"
cd "$DEST" && sha256sum -c *.sha256
```

## عیب‌یابی

| خطا | راه‌حل |
|-----|--------|
| `BACKUP_ROOT missing` / bind stale | `mkdir -p ~/Api_Vapp_Manually/backups/{daily,weekly,logs}` سپس recreate sqlserver |
| `Access is denied` / OS error 5 | `bash install-db-backup-cron.sh` (chown mssql) |
| `Container not running` | `cd ~/Api_Vapp_Manually/docker && docker compose -f docker-compose.production.yml --env-file .env up -d sqlserver` |
| cron بدون SUCCESS | `tail -80 ~/Api_Vapp_Manually/backups/logs/cron.log` |
