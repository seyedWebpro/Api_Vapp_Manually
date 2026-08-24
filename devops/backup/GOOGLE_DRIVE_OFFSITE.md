# بکاپ offsite روی Google Drive (Vapp / ok-sms.ir)

همان الگوی Microless — برای اطمینان ۱۰۰٪ وقتی سرور از بین برود.

## خلاصه

```
سرور → بکاپ .bak → rclone → Google Drive/vapp-db-backups/DbVapp/daily/
```

هر بکاپ: `.bak` + `.sha256` + `.json` — با verify اندازه + retention ابری.

> **وضعیت فعلی:** کد و cron on-site آماده‌اند. rclone روی سرور نصب می‌شود.  
> فعال‌سازی Drive نیاز به اکانت گوگل کارفرما + Client ID/Secret + توکن OAuth دارد (یک‌بار).

---

## بخش ۱ — کارفرما / Google (یک‌بار، روی مرورگر)

### ۱) اکانت جدا برای بکاپ

- یک Gmail **فقط برای بکاپ** (نه اکانت شخصی شلوغ)
- 2FA فعال شود

### ۲) Google Cloud Console

1. https://console.cloud.google.com/ با همان اکانت
2. **New Project** → مثلاً `vapp-backups`
3. **APIs & Services → Library** → `Google Drive API` → **Enable**
4. **OAuth consent screen**
   - User Type: **External**
   - App name: `vapp-backups`
   - Support/Developer email: همان ایمیل
   - **Test users**: ایمیل اکانت Drive را اضافه کن
5. **Credentials → Create Credentials → OAuth client ID**
   - Application type: **Desktop app**
   - Name: `rclone-vapp-server`
6. **Client ID** و **Client Secret** را برای ما بفرست (یا خودت نگه دار)

### ۳) پوشه در Drive

در https://drive.google.com بساز:

`vapp-db-backups`

rclone خودش `DbVapp/daily` و `weekly` را می‌سازد.

---

## بخش ۲ — توکن OAuth (روی Mac شما — سرور مرورگر ندارد)

```bash
# اگر rclone نداری:
brew install rclone

rclone authorize "drive" "CLIENT_ID" "CLIENT_SECRET"
```

- مرورگر → Allow
- توکن JSON بلند (`{ ... }`) را کامل کپی کن

---

## بخش ۳ — سرور (یک‌بار)

### ۱) rclone (اگر نیست)

```bash
sudo apt-get update && sudo apt-get install -y rclone
```

### ۲) rclone config

```bash
rclone config
```

| سوال | جواب |
|------|------|
| n) New remote | `n` |
| name | `vapp-gdrive` |
| Storage | `drive` |
| client_id | از Google Cloud |
| client_secret | از Google Cloud |
| scope | `1` (Full access) |
| service_account_file | Enter |
| Edit advanced? | `n` |
| Use auto config? | **`n`** |
| config_token | توکن از Mac |
| Shared Drive? | `n` |
| Keep / Quit | `y` / `q` |

تست:

```bash
rclone lsd vapp-gdrive:
```

### ۳) فعال‌سازی backup.env

```bash
bash ~/Api_Vapp_Manually/devops/scripts/setup-gdrive-offsite.sh
```

یا دستی در `devops/backup/backup.env`:

```env
RCLONE_REMOTE=vapp-gdrive:vapp-db-backups/DbVapp
```

### ۴) تست + اولین آپلود

```bash
bash ~/Api_Vapp_Manually/devops/scripts/verify-rclone-offsite.sh && \
bash ~/Api_Vapp_Manually/devops/scripts/backup-database.sh && \
bash ~/Api_Vapp_Manually/devops/scripts/list-offsite-backups.sh
```

در لاگ: `offsite OK`  
در Drive: `vapp-db-backups/DbVapp/daily/DbVapp_full_....bak`

---

## cron

بعد از ست کردن `RCLONE_REMOTE`، cron شبانه **خودکار** on-site + Drive می‌فرستد — نیازی به نصب دوباره cron نیست.

---

## بازیابی از Google Drive

```bash
mkdir -p ~/restore_from_gdrive
rclone copyto "vapp-gdrive:vapp-db-backups/DbVapp/daily/DbVapp_full_YYYYMMDD_HHMMSS.bak" \
  ~/restore_from_gdrive/DbVapp_full.bak
# بهتر: .sha256 را هم بگیر و چک کن
bash ~/Api_Vapp_Manually/devops/scripts/restore-database.sh \
  --file ~/restore_from_gdrive/DbVapp_full.bak --confirm RESTORE
```

---

## عیب‌یابی

| خطا | کار |
|-----|-----|
| `access_denied` | Test user در OAuth consent |
| `token expired` | دوباره `rclone authorize` + update config |
| `403` | Drive API Enable |
| `Cannot access remote` | `rclone lsd vapp-gdrive:` |
