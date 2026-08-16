# Zohal (شاهکار) — IP و تنظیمات

## IPهایی که در پنل زحل ثبت کنید

| محیط | IP | توضیح |
|------|-----|--------|
| **Production (سرور)** | `185.116.162.233` | IP عمومی VPS — **الزامی** |
| **توسعه Mac (فعلی)** | `188.245.97.173` | برای تست local از Mac — اختیاری |

مسیر پنل: [dashboard.zohal.io](https://dashboard.zohal.io/) → **توسعه‌دهندگان** → **IPهای مجاز**

---

## توکن روی سرور

در `/root/Api_Vapp_Manually/docker/.env`:

```env
ZOHAL_API_TOKEN=your-token
Zohal__Enabled=true
Zohal__BaseUrl=https://service.zohal.io/api/v0
Zohal__TimeoutSeconds=30
```

بعد از تغییر:

```bash
ssh vapp-prod
cd /root/Api_Vapp_Manually/docker
docker compose -f docker-compose.production.yml up -d api
```

---

## لاگ‌ها

| محل | محتوا |
|-----|--------|
| جدول `ZohalInquiryLogs` | هر استعلام: HTTP، result، error_code، message، matched، duration، traceId |
| جدول `AdminAuditLogs` | اکشن‌های `Shahkar.*` با metadata |
| فایل `log/log-*.txt` | Serilog ساختاریافته |

SQL ایجاد جدول (در صورت نیاز):

```bash
ssh vapp-prod "SA=\$(grep ^SA_PASSWORD= /root/Api_Vapp_Manually/docker/.env|cut -d= -f2-); docker exec -i vapp_sqlserver_prod /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P \"\$SA\" -C -d DbVapp" \
  < devops/scripts/sql/ensure-zohal-inquiry-logs.sql
```

---

## تست crawl

```bash
# local
bash devops/scripts/crawl-shahkar-register.sh

# production
BASE_URL=http://185.116.162.233 \
AUTH_PHONE=09920374397 \
AUTH_NATIONAL_ID=4220855361 \
PREPARE_USER=1 \
bash devops/scripts/crawl-shahkar-register.sh
```

---

## خطاهای زحل → پیام کاربر

| وضعیت زحل | کاربر می‌بیند | errorCode | لاگ/audit |
|-----------|---------------|-----------|---------|
| matched=true | OTP ارسال | — | `Shahkar.Matched` |
| matched=false | کد ملی با موبایل مطابقت ندارد | `IDENTITY_VERIFICATION_FAILED` | `Shahkar.NotMatched` |
| کد ملی نامعتبر | کد ملی نامعتبر است | `INVALID_INPUT` | `Shahkar.InvalidInput` |
| کمبود شارژ کیف پول | سرویس تطبیق در دسترس نیست | `IDENTITY_VERIFICATION_UNAVAILABLE` | `Shahkar.InsufficientBalance` |
| توکن/IP/خطای سرور | سرویس تطبیق در دسترس نیست | `IDENTITY_VERIFICATION_UNAVAILABLE` | `Shahkar.Failed` / ... |

**مهم:** پیام خام زحل (مثل «کیف پول اعتبار کافی ندارد») فقط در `ZohalInquiryLogs.ProviderMessage` و لاگ سرور ثبت می‌شود — به کاربر نشان داده نمی‌شود.

---

## هزینه

هر استعلام شاهkar ≈ **۱,۵۵۰ تومان** (تعرفه زیبال/زحل). موجودی را در پنل زحل شارژ کنید.
