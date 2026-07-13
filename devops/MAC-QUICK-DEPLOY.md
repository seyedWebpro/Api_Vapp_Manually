# Deploy سریع از Mac

یک اسکریپت برای انتخاب دستور مناسب — بدون پیچیدگی اضافه.

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/deploy-from-mac.sh <mode>
```

پیش‌نیاز: `ssh vapp-prod` کار کند — [`MAC-SERVER.md`](MAC-SERVER.md)

---

## چه تغییری دادید؟ → چه بزنید؟

| تغییر | دستور | زمان تقریبی |
|--------|--------|-------------|
| چند خط C# / سرویس / DTO | `deploy-from-mac.sh api` | ۳–۷ دقیقه (با cache) |
| image از Mac قبلاً upload شده، فقط restart | `deploy-from-mac.sh api-restart` | ~۱ دقیقه |
| کامپوننت / استایل / ترجمه ادمین | `deploy-from-mac.sh admin` | ۲–۴ دقیقه |
| `npm run build` زدید، فقط بفرستید | `deploy-from-mac.sh admin-fast` | ~۳۰ ثانیه |
| تغییر Public_Vapp (فرم/گردونه SMS) | `deploy-from-mac.sh public` | ۲–۴ دقیقه |
| dist Public آماده — فقط upload | `deploy-from-mac.sh public-fast` | ~۳۰ ثانیه |
| Admin + Public هر دو | `deploy-from-mac.sh all-fronts` | ۳–۵ دقیقه |
| API + Admin هر دو | `deploy-from-mac.sh both` | ۵–۱۰ دقیقه |
| API + Admin + Public | `deploy-from-mac.sh all` | ۷–۱۲ دقیقه |
| فقط چک سلامت | `deploy-from-mac.sh health` | چند ثانیه |

### ربات شماره‌جو (repo جدا — `scraping_Number_Vapp`)

راهنمای کامل: [`NUMBER-SCRAPER.md`](NUMBER-SCRAPER.md)

| تغییر | دستور | زمان تقریبی |
|--------|--------|-------------|
| کد Python / scraper | `cd scraping_Number_Vapp && bash devops/scripts/deploy-from-mac.sh api` | ۵–۱۵ دقیقه (Chromium) |
| upload قطع شد | `bash devops/scripts/deploy-api-upload-rsync.sh` | resume از همان نقطه |
| image آماده — فقط deploy | `bash devops/scripts/deploy-from-mac.sh api-upload` | ۲–۵ دقیقه |
| فقط restart ربات | `bash devops/scripts/deploy-from-mac.sh api-restart` | ~۱ دقیقه |
| توکن divar/sheypoor | `bash devops/scripts/deploy-from-mac.sh sync-data` | چند ثانیه |
| build روی سرور (بدون upload) | `ssh vapp-prod 'cd ~/scraping_Number_Vapp && docker compose -f docker-compose.production.yml --env-file .env build api && docker compose -f docker-compose.production.yml --env-file .env up -d --force-recreate --no-build'` | ۱۵–۳۰ دقیقه |

---

## قانون ساده

- **تغییر کد** → باید build شود (ولی Docker/npm **cache** کمک می‌کند).
- **تغییر `appsettings` یا `.env` روی سرور** → فقط restart؛ build لازم نیست.
- **image یا dist از قبل روی سرور** → `api-restart` یا `admin-fast`.

### کی build کند می‌شود؟

- اولین بار یا بعد از `Api_Vapp.csproj` / `package.json`
- اینترنت ضعیف (NuGet / npm timeout)
- پاک شدن cache Docker (`docker builder prune`)

---

## جریان روزانه

```bash
git push origin main

# فقط بکند
bash devops/scripts/deploy-from-mac.sh api

# فقط پنل
bash devops/scripts/deploy-from-mac.sh admin

# فقط Public (لینک SMS فرم/گردونه)
bash devops/scripts/deploy-from-mac.sh public

# تأیید
bash devops/scripts/deploy-from-mac.sh health

# ربات (اگر تغییر داده‌اید)
cd ~/Documents/javad_project/vapp/scraping_Number_Vapp
bash devops/scripts/deploy-from-mac.sh api
```

راهنمای کامل Public: [`PUBLIC-VAPP.md`](PUBLIC-VAPP.md)

---

## نکات

**بکند:** کد داخل Docker image است — `git pull` روی سرور برای اجرا کافی نیست؛ باید image جدید برود یا از `api-restart` بعد از upload استفاده کنید.

**ادمین:** `admin` / `admin-fast` فایل‌های `dist` را می‌فرستد و nginx را روی static تنظیم می‌کند (سریع‌تر از build Docker روی سرور ایران).

**اگر `git pull` روی سرور conflict داد** (مثلاً `docker-compose`):
```bash
ssh vapp-prod 'cd ~/Api_Vapp_Manually && git stash push -m server-local -- docker/docker-compose.production.yml && git pull origin main'
```
برای deploy فعلی اگر image از Mac آمده، `api-restart` کافی است.

---

## فایل‌های مرتبط

| فایل | کار |
|------|-----|
| `scripts/deploy-from-mac.sh` | ورودی اصلی |
| `scripts/deploy-api-upload-image.sh` | build API روی Mac |
| `scripts/deploy-front-upload-dist.sh` | build Admin روی Mac |
| `scripts/deploy-public-front-upload-dist.sh` | build Public_Vapp روی Mac |
| `PUBLIC-VAPP.md` | راهنمای کامل فرم/گردونه عمومی |
| `NUMBER-SCRAPER.md` | ربات شماره‌جو — deploy، env، تست |
| `MAC-SERVER.md` | SSH و تنظیم اولیه |
