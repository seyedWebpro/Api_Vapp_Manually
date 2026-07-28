# Runbook — پشتیبانی Audit (AdminAuditLogs)

جستجوی سریع تغییرات حساس بدون نیاز به UI.

## پیش‌نیاز

روی سرور یا لوکال:

```bash
cd ~/Api_Vapp_Manually   # یا مسیر پروژه روی مک
```

Connection از `AUDIT_SQL_CONNECTION` یا appsettings خوانده می‌شود.
روی سرور می‌توانید از داخل کانتینر API هم SQL بزنید.

---

## ۱) کی قیمت پلن را عوض کرد؟

```bash
bash devops/scripts/audit-search.sh \
  --action SubscriptionPlan.PriceUpdated \
  --entity-type SubscriptionPlan \
  --entity-id 12 \
  --lines 50
```

بدون دانستن id پلن:

```bash
bash devops/scripts/audit-search.sh \
  --action SubscriptionPlan.PriceUpdated \
  --lines 100
```

جستجوی عدد داخل JSON (قبل/بعد):

```bash
bash devops/scripts/audit-search.sh \
  --action SubscriptionPlan.PriceUpdated \
  --q-json 10890000 \
  --lines 50
```

---

## ۲) همهٔ تغییرات یک پلن (قیمت + وضعیت + ویرایش)

```bash
bash devops/scripts/audit-search.sh \
  --entity-type SubscriptionPlan \
  --entity-id 12 \
  --lines 100
```

---

## ۳) اکشن‌های یک ادمین در یک روز

```bash
bash devops/scripts/audit-search.sh \
  --actor 5 \
  --from 2026-07-25 \
  --to 2026-07-26 \
  --lines 200
```

---

## ۴) اسپایک لاگین ناموفق ادمین

```bash
bash devops/scripts/audit-search.sh \
  --action Auth.AdminLoginFailed \
  --from "$(date -u -v-1H +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S)" \
  --lines 200
```

آلرت خودکار در لاگ API با این الگو ثبت می‌شود (grep):

```bash
docker logs vapp_api_prod 2>&1 | grep 'AUDIT_ALERT type=AdminLoginFailSpike'
```

---

## API (با توکن ادمین)

```bash
curl -sS "http://127.0.0.1:8080/api/Admin/Audit?action=SubscriptionPlan.PriceUpdated&entityId=12" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq .

# جستجو داخل JSON
curl -sS "http://127.0.0.1:8080/api/Admin/Audit?q=10890000&searchInJson=true" \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq .
```

---

## Retention

- پیش‌فرض: حذف لاگ‌های قدیمی‌تر از **۱۸۰ روز** (حداقل قابل تنظیم ۹۰)
- جاب روزانه: `AuditRetentionBackgroundService`
- تنظیم در `appsettings` بخش `Audit`
