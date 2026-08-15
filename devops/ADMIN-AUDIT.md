# جدول AdminAuditLogs — راهنمای کوتاه

ردپای اکشن‌های حساس ادمین/سیستم در SQL Server. فقط append می‌شود (بدون ویرایش/حذف دستی).

> برای پشتیبانی روزمره (کجا بگردم؟ پرداخت / ۵۰۰ / SMS): [`SUPPORT-TROUBLESHOOTING.md`](SUPPORT-TROUBLESHOOTING.md)

## جدول

| مورد | مقدار |
|------|--------|
| نام | `dbo.AdminAuditLogs` |
| ستون‌های مهم | `Action`, `EntityType`, `EntityId`, `ActorUserId`, `OldValue`, `NewValue`, `CreatedAt` |
| نگهداری | حدود ۱۸۰ روز (جاب `AuditRetentionBackgroundService`) |

## بررسی سریع (پیشنهادی)

```bash
ssh vapp-prod
cd /root/Api_Vapp_Manually

# آخرین ۵۰ رکورد
bash devops/scripts/audit-search.sh --lines 50

# اکشن خاص
bash devops/scripts/audit-search.sh --action SubscriptionPlan.PriceUpdated --lines 50

# تغییرات یک موجودیت
bash devops/scripts/audit-search.sh --entity-type SubscriptionPlan --entity-id 12

# کارهای یک ادمین
bash devops/scripts/audit-search.sh --actor 5 --from 2026-08-01 --to 2026-08-07

# جستجو داخل JSON قبل/بعد
bash devops/scripts/audit-search.sh --q-json 10890000 --lines 50
```

## SQL مستقیم

```sql
SELECT TOP 50 Id, CreatedAt, Category, Action, EntityType, EntityId,
       ActorUserId, Succeeded, LEFT(ISNULL(NewValue,''), 200) AS NewPreview
FROM dbo.AdminAuditLogs
ORDER BY Id DESC;

-- لاگین ناموفق ادمین
SELECT TOP 100 * FROM dbo.AdminAuditLogs
WHERE Action = N'Auth.AdminLoginFailed'
ORDER BY Id DESC;
```

## API (توکن ادمین)

```bash
curl -sS "http://127.0.0.1:8080/api/Admin/Audit?page=1&pageSize=50" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

## Categoryهای رایج

`admin` · `auth` · `subscription` · `payment` · `wallet` · `cashback` · `message` · `approval` · `user` · `role`

جزئیات سناریوها → [`AUDIT_RUNBOOK.md`](AUDIT_RUNBOOK.md)
