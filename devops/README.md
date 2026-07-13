# DevOps — Vapp

اسکریپت‌های deploy روی سرور لینوکس. **قابل کپی برای پروژه‌های دیگر**؛ قبل از استفاده این‌ها را با stack خودت هماهنگ کن:

| مورد | Vapp (فعلی) | پروژه جدید |
|------|-------------|------------|
| API | .NET 8 + Docker | مسیر repo، `docker-compose`، Dockerfile |
| Front | Vite/React + Docker | repo جدا، پورت، `VITE_*` یا معادل framework |
| Public | Vite/React — `/form` `/wheel` | `Public_Vapp`، static یا docker :3006 |
| DB | SQL Server در Docker | نوع DB، container name، connection string |
| Proxy | Nginx | `location`ها، پورت upstream، دامنه/IP |

## ساختار

```
devops/
  NUMBER-SCRAPER.md     ربات شماره‌جو — deploy و اتصال به Vapp
  scripts/              deploy، bootstrap، backup، SSH
  deploy/               nginx example
  backup/               بکاپ DB
  domain/               دامنه ok-sms.ir — راهنمای کامل
  .env.server.example
```

## ترتیب معمول

1. `setup-github-deploy-key.sh` — کلید سرور → GitHub  
2. `bootstrap-first-run.sh` — نصب اولیه Vapp (یک‌بار)  
3. `bootstrap-scraper-on-server.sh` — نصب ربات (یک‌بار) — در repo ربات  
4. `deploy-server.sh --fast --wait` — آپدیت بعدی  

جزئیات: `server-update-commands.txt` · `MAC-SERVER.md` · `GITHUB_SSH.md` · `NUMBER-SCRAPER.md`

## Mac → سرور

| سند | موضوع |
|-----|--------|
| **`NUMBER-SCRAPER.md`** | **ربات شماره‌جو — deploy، env، تست، عیب‌یابی** |
| **`PUBLIC-VAPP.md`** | Deploy فرم/گردونه عمومی (لینک SMS) |
| **`domain/README.md`** | سوئیچ به دامنه `ok-sms.ir` |
| **`MAC-QUICK-DEPLOY.md`** | چه تغییری → چه دستور (سریع) |
| **`MAC-SERVER.md`** | SSH پورت 3031، تنظیم اولیه |

```bash
bash devops/scripts/deploy-from-mac.sh api      # بکند .NET
bash devops/scripts/deploy-from-mac.sh admin    # پنل ادمین
bash devops/scripts/deploy-from-mac.sh public   # فرم/گردونه عمومی
```

## Number Scraper (شماره‌جو)

```
vapp/
  Api_Vapp_Manually/     بکند .NET
  Admin_Vapp/            پنل ادمین
  Public_Vapp/           فرم/گردونه
  scraping_Number_Vapp/  ربات شماره‌جو
```

```
موبایل → Vapp .NET → ربات Python (:8000 داخلی)
```

| مورد | مسیر |
|------|------|
| راهنمای کامل | **`devops/NUMBER-SCRAPER.md`** |
| Repo ربات | `../scraping_Number_Vapp` (کنار Api_Vapp_Manually) |
| DevOps ربات | `~/scraping_Number_Vapp/devops/` |
| env Vapp | `SCRAPER_API_KEY` + `NumberScraperApi__*` در `docker/.env` |
| اتصال | `NumberScraperApi__BaseUrl=http://host.docker.internal:8000` |

```bash
# deploy ربات از Mac
cd ~/Documents/javad_project/vapp/scraping_Number_Vapp
bash devops/scripts/deploy-from-mac.sh api          # build + upload
bash devops/scripts/deploy-from-mac.sh sync-data    # توکن پلتفرم‌ها
bash devops/scripts/deploy-api-upload-rsync.sh      # upload با resume (اگر قطع شد)

# build روی سرور (وقتی upload از Mac مشکل دارد)
ssh vapp-prod 'cd ~/scraping_Number_Vapp && docker compose -f docker-compose.production.yml --env-file .env build api && docker compose -f docker-compose.production.yml --env-file .env up -d --force-recreate --no-build'
```

جزئیات: [`NUMBER-SCRAPER.md`](NUMBER-SCRAPER.md) · [`server-update-commands.txt`](server-update-commands.txt)
