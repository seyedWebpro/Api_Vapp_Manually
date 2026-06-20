# بکاپ DbVapp (SQL Server / Docker)

## نصب سریع

```bash
cd ~/Api_Vapp_Manually && git pull origin main && \
  chmod +x devops/scripts/*.sh && \
  bash devops/scripts/install-db-backup-cron.sh && \
  bash devops/scripts/backup-database.sh
```

## مسیر فایل‌ها

```
~/Api_Vapp_Manually/backups/
  daily/     DbVapp_full_YYYYMMDD_HHMMSS.bak
  weekly/    ...
  logs/      backup-*.log , cron.log
  latest.bak -> آخرین بکاپ
```

Volume در `docker/docker-compose.production.yml`: `../backups:/backups`

## تست دستی

```bash
bash ~/Api_Vapp_Manually/devops/scripts/backup-database.sh
ls -lh ~/Api_Vapp_Manually/backups/daily/ | tail -5
```

## offsite (اختیاری)

در `devops/backup/backup.env` مقدار `RCLONE_REMOTE` را ست کنید (مثل AmazonShop).
