# لاگ‌های سرور — خیلی کوتاه

> **پشتیبانی / پیدا کردن خطا:** اول [`SUPPORT-TROUBLESHOOTING.md`](SUPPORT-TROUBLESHOOTING.md) را باز کنید (جدول تصمیم + سناریوها).

## کجا ذخیره می‌شوند؟

| نوع | مسیر |
|-----|------|
| فایل‌های API (Serilog) | `/root/Api_Vapp_Manually/log/` (یا `~/Api_Vapp_Manually/log/`) |
| نام فایل | `log-YYYYMMDD.txt` (روزانه، تا ۳۰ روز) |
| داخل کانتینر | `/app/log` ← همان پوشه روی دیسک سرور |
| Audit کسب‌وکار | SQL → `dbo.AdminAuditLogs` → [`ADMIN-AUDIT.md`](ADMIN-AUDIT.md) |
| اسکرپر شماره‌جو | `~/scraping_Number_Vapp/logs/api_server.log` |

## دیدن لاگ‌ها

```bash
# SSH به سرور
ssh vapp-prod

# آخرین لاگ فایل امروز
ls -lt ~/Api_Vapp_Manually/log/
tail -f ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt

# خطاهای امروز
grep -iE '\[ERR\]|exception' ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt | tail -50

# لاگ زندهٔ کانتینر (Console)
docker logs -f --tail 100 vapp_api_prod
docker logs --tail 50 vapp-admin
docker logs --tail 50 vapp-public
```

## جستجوی سریع

```bash
grep -i error ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt
docker logs --tail 200 vapp_api_prod 2>&1 | grep -i error

# با TraceId از پاسخ خطای API
grep -F 'TRACE_ID_HERE' ~/Api_Vapp_Manually/log/log-YYYYMMDD.txt
```

## Audit ادمین (جدول SQL)

برای ردپای اکشن‌های ادمین/مالی → [`ADMIN-AUDIT.md`](ADMIN-AUDIT.md) · [`AUDIT_RUNBOOK.md`](AUDIT_RUNBOOK.md)

```bash
bash ~/Api_Vapp_Manually/devops/scripts/audit-search.sh --lines 50
```
