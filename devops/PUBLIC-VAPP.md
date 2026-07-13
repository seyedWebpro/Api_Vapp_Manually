# Public_Vapp — Deploy (فرم و گردونه عمومی / لینک SMS)

صفحات عمومی برای بازدیدکننده‌ای که از **SMS** روی لینک فرم یا گردونه کلیک می‌کند.

| مسیر | کاربرد |
|------|--------|
| `/form/{slug}` | پر کردن فرم + نام و موبایل |
| `/wheel/{slug}` | چرخاندن گردونه + نام و موبایل |

API از همان سرور (`/api/FormPublic` و `/api/LuckyWheelPublic`) سرو می‌شود — نیازی به دامنه جدا نیست.

---

## معماری روی سرور

```
                    ┌─────────────────────────────────────┐
  کاربر (SMS)  ──►  │  Nginx :80                          │
                    │  /api/*        → API :8080          │
                    │  /form/*       → Public_Vapp        │
                    │  /wheel/*      → Public_Vapp        │
                    │  /             → Admin_Vapp         │
                    └─────────────────────────────────────┘

  Static (پیشنهادی — سریع):
    Admin  → /var/www/vapp-admin
    Public → /var/www/vapp-public

  Docker (جایگزین):
    Admin  → 127.0.0.1:3005
    Public → 127.0.0.1:3006
```

**لینک‌های SMS** در `appsettings` ساخته می‌شوند:

```json
"FormBuilder": { "PublicBaseUrl": "http://185.116.162.233/form" }
"LuckyWheel":  { "PublicBaseUrl": "http://185.116.162.233/wheel" }
```

مثال لینک نهایی: `http://185.116.162.233/form/contact-form`

---

## ساختار پوشه‌ها روی سرور

| مسیر | توضیح |
|------|--------|
| `~/Api_Vapp_Manually` | API + اسکریپت‌های devops |
| `~/Admin_Vapp` | پنل ادمین React |
| `~/Public_Vapp` | فرم/گردونه عمومی React |
| `/var/www/vapp-admin` | خروجی build ادمین (static) |
| `/var/www/vapp-public` | خروجی build عمومی (static) |

---

## نصب اولیه Public_Vapp (یک‌بار روی سرور)

### روش A — Git (وقتی ریپو روی GitHub است)

```bash
ssh vapp-prod
git clone git@github.com:seyedWebpro/Public_Vapp.git ~/Public_Vapp
```

### روش B — rsync از Mac (بدون git)

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
SERVER=vapp-prod bash devops/scripts/sync-to-server.sh
```

سپس روی سرور:

```bash
chmod +x ~/Api_Vapp_Manually/devops/scripts/*.sh
bash ~/Api_Vapp_Manually/devops/scripts/deploy-public-front-host.sh
```

---

## Deploy روزانه

### از Mac (پیشنهادی — سریع)

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/deploy-from-mac.sh public
```

اگر `npm run build` را قبلاً زده‌اید:

```bash
bash devops/scripts/deploy-from-mac.sh public-fast
```

Admin + Public با هم:

```bash
bash devops/scripts/deploy-from-mac.sh all-fronts
```

### روی سرور (بعد از git pull)

```bash
cd ~/Api_Vapp_Manually && git pull origin main && bash devops/scripts/deploy-public-front-host.sh
```

اگر Public_Vapp هم git دارد:

```bash
cd ~/Public_Vapp && git pull origin main && bash ~/Api_Vapp_Manually/devops/scripts/deploy-public-front-host.sh
```

### همه سرویس‌ها (API + Admin + Public)

روی سرور:

```bash
cd ~/Api_Vapp_Manually && git pull origin main && bash devops/scripts/deploy-server.sh --fast --wait
```

---

## اسکریپت‌ها

| اسکریپت | کجا | کار |
|---------|-----|-----|
| `deploy-public-front-upload-dist.sh` | Mac | build + rsync dist → سرور |
| `deploy-public-front-host.sh` | سرور | npm build + static nginx |
| `deploy-public-front.sh` | سرور | Docker build (:3006) |
| `apply-nginx.sh` | سرور | مسیرهای `/form` و `/wheel` |
| `deploy-from-mac.sh public` | Mac | ورودی ساده |
| `health-check.sh` | سرور | چک API + Admin + Public |

---

## متغیرهای محیطی

| متغیر | پیش‌فرض | توضیح |
|-------|---------|--------|
| `PUBLIC_DIR` | `~/Public_Vapp` | سورس روی سرور |
| `PUBLIC_STATIC_ROOT` | `/var/www/vapp-public` | مسیر static nginx |
| `PUBLIC_DEPLOY_MODE` | `host` | `host` یا `docker` |
| `VITE_API_URL` | خالی | خالی = API از همان دامنه (`/api`) |
| `SERVER` | `vapp-prod` | alias SSH در Mac |

---

## تست بعد از deploy — از کجا ببینم؟

### ۱) سلامت کلی (روی سرور)

```bash
bash ~/Api_Vapp_Manually/devops/scripts/health-check.sh
```

خروجی مطلوب: `API:200 ... PUBLIC:200 ... OK: all services healthy`

### ۲) ظاهر فرم و گردونه (مرورگر)

| چه می‌بینید | آدرس |
|-------------|------|
| صفحه فرم | `http://185.116.162.233/form/{slug}` |
| صفحه گردونه | `http://185.116.162.233/wheel/{slug}` |
| پنل ادمین | `http://185.116.162.233/` |

`{slug}` را از پنل ادمین بگیرید — فرم/گردونه باید **منتشر (Published)** شده باشد.

- اگر SPA باز شد ولی «فرم یافت نشد» → slug اشتباه است یا هنوز publish نشده
- اگر صفحه سفید یا asset نمی‌آید → `apply-nginx.sh` و `deploy-public-front-host.sh` را دوباره بزنید

### ۳) لینک SMS (PublicBaseUrl)

بعد از publish در پنل، فیلد `publicUrl` باید شبیه این باشد:

- فرم: `http://185.116.162.233/form/my-slug`
- گردونه: `http://185.116.162.233/wheel/my-slug`

روی سرور چک env:

```bash
docker exec vapp_api_prod printenv | grep PublicBaseUrl
```

### ۴) API عمومی (بدون مرورگر)

```bash
# slug واقعی و منتشرشده
curl -s http://127.0.0.1:8080/api/FormPublic/your-slug
curl -s http://127.0.0.1:8080/api/LuckyWheelPublic/your-slug

# فقط shell صفحه (۲۰۰ = Public_Vapp بالا است)
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1/form/your-slug
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1/wheel/your-slug
```

Swagger: `http://185.116.162.233/swagger` → بخش‌های `FormPublic` و `LuckyWheelPublic`

از مرورگر: نام و موبایل را وارد کنید → فرم ارسال یا گردونه بچرخد.

---

## عیب‌یابی

| مشکل | راه‌حل |
|------|--------|
| `/form/...` صفحه ادمین یا 404 | `bash devops/scripts/apply-nginx.sh` |
| Public:000 در health | `deploy-public-front-host.sh` را اجرا کنید |
| API خطا در صفحه عمومی | `curl http://127.0.0.1:8080/health` — API بالا باشد |
| asset لود نمی‌شود | مسیر `/public-assets/` در nginx و build با `assetsDir: public-assets` |
| لینک SMS اشتباه | `docker exec vapp_api_prod printenv \| grep PublicBaseUrl` — باید `185.116.162.233/form` و `/wheel` باشد |
| `docker/.env` پاک شد | بعد از `sync-to-server` دوباره از `~/vapp-secrets.txt` بسازید؛ sync دیگر `.env` را حذف نمی‌کند |

---

## فایل‌های مرتبط

- `MAC-QUICK-DEPLOY.md` — دستورات Mac
- `server-update-commands.txt` — cheat sheet سرور
- `MAC-SERVER.md` — SSH و تنظیم اول
