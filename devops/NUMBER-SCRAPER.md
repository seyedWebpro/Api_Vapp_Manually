# Number Scraper (شماره‌جو) — راهنمای DevOps

ربات اسکرپ پایتون روی **همان سرور Vapp** deploy می‌شود. موبایل **هرگز** مستقیم به ربات وصل نمی‌شود.

```
موبایل ──HTTPS──► Vapp .NET (:8080 / ok-sms.ir)
                      │  JWT + feature: number_seeker
                      │  X-API-Key (داخلی)
                      ▼
                 ربات Python (:8000)
```

| مورد | مقدار |
|------|--------|
| IP سرور | `185.116.162.233` |
| SSH | `ssh vapp-prod` (پورت **3031**) |
| Repo Vapp | `~/Api_Vapp_Manually` |
| Mac (لوکال) | `~/Documents/javad_project/vapp/scraping_Number_Vapp` |
| سرور | `~/scraping_Number_Vapp` |
| GitHub | `git@github.com:seyedWebpro/scraping_Number_Vapp.git` |
| DevOps | `scraping_Number_Vapp/devops/` (Mac) · `~/scraping_Number_Vapp/devops/` (سرور) |

---

## معماری و امنیت

- **موبایل** فقط endpointهای `/api/NumberSeeker/*` روی Vapp را می‌بیند.
- **ربات** فقط از شبکه داخلی Docker/سرور در دسترس است (`host.docker.internal:8000`).
- **API Key مشترک** بین Vapp و ربات — موبایل آن را نمی‌بیند.
- **DB ربات** (`PhoneScraperDB`) برای فلو اصلی **ضروری نیست** — Vapp مالک تسک‌هاست (`NumberSeekerTasks` در `DbVapp`).

---

## env — Vapp (`docker/.env`)

```env
SCRAPER_API_KEY=your-secret-key-min-16-chars
NumberScraperApi__Enabled=true
NumberScraperApi__BaseUrl=http://host.docker.internal:8000
NumberScraperApi__ApiKey=${SCRAPER_API_KEY}
NumberScraperApi__TimeoutSeconds=120
```

نمونه کامل: [`devops/.env.server.example`](.env.server.example)

بعد از تغییر env، restart Vapp API:

```bash
cd ~/Api_Vapp_Manually && docker compose -f docker/docker-compose.production.yml --env-file docker/.env up -d --no-deps --force-recreate --no-build api
```

---

## env — ربات (`~/scraping_Number_Vapp/.env`)

```env
API_KEY=your-secret-key-min-16-chars          # همان SCRAPER_API_KEY
API_PORT_MAPPING=8000:8000                    # مهم: برای host.docker.internal
DOTNET_WEBHOOK_ENABLED=true
DOTNET_WEBHOOK_URL=http://host.docker.internal:8080/api/NumberSeeker/internal/webhook/task-completed
DOTNET_WEBHOOK_API_KEY=your-secret-key-min-16-chars
```

> **نکته:** `127.0.0.1:8000:8000` باعث می‌شود container Vapp به ربات وصل نشود. باید `8000:8000` باشد (پورت از بیرون firewall باز نیست).

---

## نصب اولیه ربات (یک‌بار روی سرور)

```bash
ssh vapp-prod

# clone
git clone git@github.com:seyedWebpro/scraping_Number_Vapp.git ~/scraping_Number_Vapp

# bootstrap خودکار: .env ربات + patch کردن docker/.env در Vapp
bash ~/scraping_Number_Vapp/devops/scripts/bootstrap-scraper-on-server.sh
```

سپس **یکی** از روش‌های deploy:

### روش A — build روی سرور (پیشنهادی وقتی upload از Mac قطع می‌شود)

```bash
cd ~/scraping_Number_Vapp && docker compose -f docker-compose.production.yml --env-file .env build api && docker compose -f docker-compose.production.yml --env-file .env up -d --force-recreate --no-build
```

### روش B — build روی Mac + upload

```bash
cd ~/Documents/javad_project/vapp/scraping_Number_Vapp
bash devops/scripts/deploy-from-mac.sh api
```

اگر upload قطع شد (image حجیم ~۶۰۰MB):

```bash
bash devops/scripts/deploy-api-upload-rsync.sh    # resume با rsync
# یا
bash devops/scripts/deploy-from-mac.sh api-upload # فقط upload (بدون rebuild)
```

---

## آپدیت روزانه

### Vapp (.NET) — بعد از push

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
git push origin main
bash devops/scripts/deploy-from-mac.sh api
```

> فقط `git pull` کافی **نیست** — image Docker باید rebuild شود.

### ربات — بعد از push

| روش | دستور |
|-----|--------|
| Mac (upload image) | `cd scraping_Number_Vapp && bash devops/scripts/deploy-from-mac.sh api` |
| Mac (upload قطع شده) | `bash devops/scripts/deploy-api-upload-rsync.sh` |
| سرور (build محلی) | `cd ~/scraping_Number_Vapp && git pull && docker compose -f docker-compose.production.yml --env-file .env build api && docker compose -f docker-compose.production.yml --env-file .env up -d --force-recreate --no-build` |
| فقط restart | `cd ~/scraping_Number_Vapp && docker compose -f docker-compose.production.yml --env-file .env up -d --no-deps --force-recreate --no-build api` |

### توکن پلتفرم‌ها (divar / sheypoor)

```bash
cd ~/Documents/javad_project/vapp/scraping_Number_Vapp
bash devops/scripts/deploy-from-mac.sh sync-data
# یا
SERVER=vapp-prod bash devops/scripts/sync-to-server.sh --data-only
```

بعد restart ربات:

```bash
ssh vapp-prod 'docker restart phonescraper_api_prod'
```

---

## تست سلامت

```bash
# ربات
ssh vapp-prod 'curl -s http://127.0.0.1:8000/health | python3 -m json.tool | head -30'

# Vapp → ربات (از داخل container)
ssh vapp-prod 'docker exec vapp_api_prod curl -s http://host.docker.internal:8000/health | head -c 200'

# Vapp NumberSeeker (نیاز به JWT + feature number_seeker)
# از Swagger: https://ok-sms.ir/swagger → NumberSeeker
```

### تست اسکرپ از سرور (مستقیم ربات)

```bash
ssh vapp-prod 'API_KEY=$(grep ^API_KEY= ~/scraping_Number_Vapp/.env | cut -d= -f2-); curl -s -X POST http://127.0.0.1:8000/api/scrape -H "X-API-Key: $API_KEY" -H "Content-Type: application/json" -d "{\"source\":\"sheypoor\",\"city\":\"tehran\",\"category\":\"mobile\",\"max_phones\":3}"'
```

---

## Endpointهای Vapp (موبایل)

| Method | Path | توضیح |
|--------|------|--------|
| POST | `/api/NumberSeeker/scrape` | شروع اسکرپ |
| GET | `/api/NumberSeeker/task/{id}` | poll وضعیت |
| POST | `/api/NumberSeeker/task/{id}/import` | import به Contact |
| POST | `/api/NumberSeeker/task/{id}/cancel` | لغو |
| GET | `/api/NumberSeeker/tasks` | تاریخچه |
| GET | `/api/NumberSeeker/sources` | لیست پلتفرم‌ها |
| GET | `/api/NumberSeeker/health` | سلامت proxy |

Webhook داخلی (فقط ربات): `POST /api/NumberSeeker/internal/webhook/task-completed`

---

## عیب‌یابی

| مشکل | راه‌حل |
|------|--------|
| Vapp `SCRAPER_UNAVAILABLE` | `docker exec vapp_api_prod curl http://host.docker.internal:8000/health` — اگر timeout → `API_PORT_MAPPING=8000:8000` |
| `token_not_configured` | sync `data/platform_tokens.json` + restart ربات |
| `db_unavailable` در نتیجه اسکرپ | طبیعی — DB ربات اختیاری است |
| upload از Mac قطع می‌شود | `deploy-api-upload-rsync.sh` یا build روی سرور |
| SQL ربات unhealthy | ۶۰–۹۰ ثانیه صبر؛ یا `docker restart phonescraper_sqlserver_prod` |
| `git dubious ownership` | `git config --global --add safe.directory /root/scraping_Number_Vapp` |

```bash
docker logs --tail 50 phonescraper_api_prod
docker logs --tail 50 vapp_api_prod | grep -i NumberScraper
```

---

## فایل‌های مرتبط

| مسیر | کار |
|------|-----|
| `devops/NUMBER-SCRAPER.md` | همین راهنما |
| `devops/.env.server.example` | env نمونه Vapp + Scraper |
| `devops/server-update-commands.txt` | cheat sheet یک‌خطی |
| `~/scraping_Number_Vapp/devops/` | اسکریپت‌های deploy ربات |
| `~/scraping_Number_Vapp/devops/VAPP-INTEGRATION.md` | جزئیات اتصال |

---

## دستور یک‌خطی (سرور)

```bash
# health هر دو
bash ~/Api_Vapp_Manually/devops/scripts/health-check.sh && curl -s http://127.0.0.1:8000/health

# آپدیت Vapp
cd ~/Api_Vapp_Manually && git pull origin main && bash devops/scripts/deploy-server-visible.sh --fast

# آپدیت ربات (بعد از build/upload image)
cd ~/scraping_Number_Vapp && git pull origin main && docker compose -f docker-compose.production.yml --env-file .env up -d --no-deps --force-recreate --no-build api
```
