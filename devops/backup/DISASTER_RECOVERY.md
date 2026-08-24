# Disaster Recovery — DbVapp

هدف: اگر سرور بسوزد / دیسک خراب شود / دیتا پاک شود، بدون از دست رفتن دیتای حیاتی برگردیم.

## لایه‌های محافظت

| لایه | کجا | وضعیت |
|------|-----|--------|
| ۱ On-site daily | `~/Api_Vapp_Manually/backups/daily/` | خودکار (cron 03:00 UTC) |
| ۲ On-site weekly | `~/Api_Vapp_Manually/backups/weekly/` | خودکار (یکشنبه) |
| ۳ Verify | VERIFYONLY + SHA256 + health-check | خودکار |
| ۴ Restore drill | `test-restore` روی DB جانبی | دستی / `verify-backup-set.sh` |
| ۵ Offsite Mac | `~/Downloads/vapp-db-backup-YYYYMMDD/` | اسکریپت download |
| ۶ Offsite Google Drive | rclone → `vapp-db-backups/DbVapp/` | **نیاز به اکانت کارفرما** — [GOOGLE_DRIVE_OFFSITE.md](./GOOGLE_DRIVE_OFFSITE.md) |

> **هشدار:** تا وقتی Google Drive (یا cloud دیگر) فعال نباشد، خراب شدن دیسک سرور + از دست رفتن Mac = ریسک از دست رفتن بکاپ. Mac alone کافی نیست برای اطمینان ۱۰۰٪.

## هر هفته روی Mac (توصیه)

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/download-backup-to-mac.sh
```

## چک سلامت روی سرور

```bash
bash ~/Api_Vapp_Manually/devops/scripts/verify-backup-set.sh
```

شامل: SHA256 + health-check + test-restore (DbVapp اصلی دست نمی‌خورد).

## بازیابی اضطراری روی همین سرور

```bash
# ۱) آخرین بکاپ سالم
BAK=$(readlink -f ~/Api_Vapp_Manually/backups/latest.bak)

# ۲) (اختیاری) تست امن اول
bash ~/Api_Vapp_Manually/devops/scripts/test-restore-database.sh --file "$BAK"

# ۳) restore واقعی — DbVapp جایگزین می‌شود
bash ~/Api_Vapp_Manually/devops/scripts/restore-database.sh --file "$BAK" --confirm RESTORE
```

اسکریپت restore قبل از REPLACE: SHA256 + VERIFYONLY؛ بعد از REPLACE: DB ONLINE + شمارش Users + health API.

## بازیابی روی سرور جدید (از فایل Mac)

```bash
# روی Mac
scp ~/Downloads/vapp-db-backup-*/DbVapp_full_*.bak \
    vapp-prod:/root/Api_Vapp_Manually/backups/restore/

# روی سرور
bash ~/Api_Vapp_Manually/devops/scripts/restore-database.sh \
  --file ~/Api_Vapp_Manually/backups/restore/DbVapp_full_YYYYMMDD_HHMMSS.bak \
  --confirm RESTORE
```

## Offsite ابری (rclone)

```bash
sudo apt-get install -y rclone && rclone config
# در backup.env:
# RCLONE_REMOTE=vapp-backups:DbVapp
bash ~/Api_Vapp_Manually/devops/scripts/verify-rclone-offsite.sh
bash ~/Api_Vapp_Manually/devops/scripts/backup-database.sh
```

## RPO / RTO تقریبی

| معیار | مقدار |
|-------|--------|
| RPO (حداکثر دیتای ازدست‌رفته) | تا ~۲۴ ساعت (بین دو بکاپ روزانه) |
| RTO (زمان برگشت) | معمولاً چند دقیقه (restore + API up) |
