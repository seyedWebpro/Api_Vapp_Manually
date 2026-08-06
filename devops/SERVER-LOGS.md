# لاگ‌های سرور — خیلی کوتاه

## کجا ذخیره می‌شوند؟

| نوع | مسیر |
|-----|------|
| فایل‌های API (Serilog) | `/root/Api_Vapp_Manually/log/` |
| نام فایل | `log-YYYYMMDD.txt` (روزانه، تا ۳۰ روز) |
| داخل کانتینر | `/app/log` ← همان پوشه روی دیسک سرور |

## دیدن لاگ‌ها

```bash
# SSH به سرور
ssh vapp-prod

# آخرین لاگ فایل امروز
ls -lt /root/Api_Vapp_Manually/log/
tail -f /root/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt

# لاگ زندهٔ کانتینر (Console)
docker logs -f --tail 100 vapp_api_prod
docker logs --tail 50 vapp-admin
docker logs --tail 50 vapp-public
```

## جستجوی سریع

```bash
grep -i error /root/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt
docker logs --tail 200 vapp_api_prod 2>&1 | grep -i error
```

## Audit ادمین (جدول SQL)

برای ردپای اکشن‌های ادمین → [`ADMIN-AUDIT.md`](ADMIN-AUDIT.md)
