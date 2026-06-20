# DevOps — Vapp

اسکریپت‌های deploy روی سرور لینوکس. **قابل کپی برای پروژه‌های دیگر**؛ قبل از استفاده این‌ها را با stack خودت هماهنگ کن:

| مورد | Vapp (فعلی) | پروژه جدید |
|------|-------------|------------|
| API | .NET 8 + Docker | مسیر repo، `docker-compose`، Dockerfile |
| Front | Vite/React + Docker | repo جدا، پورت، `VITE_*` یا معادل framework |
| DB | SQL Server در Docker | نوع DB، container name، connection string |
| Proxy | Nginx | `location`ها، پورت upstream، دامنه/IP |

## ساختار

```
devops/
  scripts/          deploy، bootstrap، backup، SSH
  deploy/           nginx example
  backup/           بکاپ DB
  domain/           یادداشت دامنه / IP
  .env.server.example
```

## ترتیب معمول

1. `setup-github-deploy-key.sh` — کلید سرور → GitHub  
2. `bootstrap-first-run.sh` — نصب اولیه (یک‌بار)  
3. `deploy-server.sh --fast --wait` — آپدیت بعدی  

جزئیات: `server-update-commands.txt` · `GITHUB_SSH.md`
