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
| API + Admin هر دو | `deploy-from-mac.sh both` | ۵–۱۰ دقیقه |
| فقط چک سلامت | `deploy-from-mac.sh health` | چند ثانیه |

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

# تأیید
bash devops/scripts/deploy-from-mac.sh health
```

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
| `MAC-SERVER.md` | SSH و تنظیم اولیه |
