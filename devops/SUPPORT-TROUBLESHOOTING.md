# راهنمای بررسی مشکل (پشتیبانی)

وقتی کاربر یا ادمین خطا گزارش می‌دهد، **اول این فایل** را باز کنید: مشخص می‌کند کجا دنبال چه چیزی بگردید.

---

## ۱) دو لایه لاگ — کجا چی هست؟

| لایه | کجا | برای چی؟ | نگهداری |
|------|-----|----------|---------|
| **Audit (دیتابیس)** | SQL Server → `dbo.AdminAuditLogs` | «کی چه کار کرد؟» خرید، پرداخت، کیف‌پول، تغییرات ادمین/ماژول | ~۱۸۰ روز |
| **لاگ فنی API (فایل)** | سرور: `/root/Api_Vapp_Manually/log/log-YYYYMMDD.txt` | Exception، stack، پیام‌های سرویس، دیباگ | ۳۰ روز |
| **لاگ زنده Docker** | `docker logs vapp_api_prod` | همان خروجی Console کانتینر (اغلب تازه) | محدود به buffer داکر |
| **جداول دامنه (مالی)** | `Payments` · `WalletTransactions` | وضعیت واقعی پول/تراکنش (نه فقط audit) | دائم |
| **اسکرپر شماره‌جو** | `~/scraping_Number_Vapp/logs/api_server.log` | خطای ربات اسکرپ | جدا از API |

> **قانون طلایی:**  
> مشکل «پول / خرید / کی تغییر داد؟» → اول **دیتابیس (Audit + Payments/Wallet)**  
> مشکل «۵۰۰ / کرش / خطای عجیب اپ» → اول **فایل لاگ سرور + TraceId**

جزئیات کوتاه هر لایه: [`SERVER-LOGS.md`](SERVER-LOGS.md) · [`ADMIN-AUDIT.md`](ADMIN-AUDIT.md)  
سناریوهای Audit: [`AUDIT_RUNBOOK.md`](AUDIT_RUNBOOK.md)

---

## ۲) قبل از جستجو این‌ها را از کاربر/ادمین بگیرید

هرچه بیشتر داشته باشید، سریع‌تر پیدا می‌کنید:

| شناسه | مثال | کجا کمک می‌کند |
|--------|------|----------------|
| **زمان تقریبی** (تهران) | ۱۴۰۵/۰۵/۲۴ حدود ۱۵:۳۰ | نام فایل روز + فیلتر `CreatedAt` |
| **شماره موبایل** یا **UserId** | `0912…` / `42` | Audit (`ActorUserId`/`TargetUserId`) + لاگ فایل |
| **TraceId** (اگر در پیام خطا آمده) | از پاسخ API فیلد `traceId` | `grep` روی فایل لاگ همان روز |
| **Authority / RefId پرداخت** | کد زرین‌پال | Audit payment + جدول `Payments` |
| **CorrelationId** (اگر در Audit دیدید) | رشته کوتاه | پیوند چند ردیف Audit یک درخواست |
| صفحه/ماژول اپ | «خرید اشتراک»، «ارسال SMS»، «لاگین» | انتخاب لایه درست (جدول پایین) |

SSH:

```bash
ssh vapp-prod
cd ~/Api_Vapp_Manually
```

---

## ۳) جدول تصمیم سریع: مشکل → کجا بروید

| گزارش کاربر / ادمین | اول کجا؟ | دستور / مسیر سریع |
|---------------------|----------|-------------------|
| اپ ۵۰۰ / سفید / خطای کلی | فایل لاگ API | بخش ۴ |
| لاگین ادمین شکست / مشکوک | Audit | `--action Auth.AdminLoginFailed` |
| لاگین کاربر موبایل | فقط فایل لاگ (فعلاً Audit ندارد) | `grep -i login` روی فایل روز |
| Logout | فقط فایل لاگ | `grep -i logout` |
| پول کم/زیاد شد، کیف‌پول | Audit + `WalletTransactions` | `--category wallet` و SQL بخش ۵ |
| پرداخت / زرین‌پال | Audit + `Payments` | `--category payment` |
| خرید اشتراک | Audit | `--action Subscription.Purchased` |
| SMS ارسال نشد / تأیید ادمین | Audit + لاگ فایل | `--category approval` یا `message` |
| شماره‌جو / اسکرپ | Audit NumberSeeker + لاگ اسکرپر | بخش ۷ |
| ادمین چیزی را عوض کرده | Audit | `--actor <UserId>` یا `--entity-type …` |
| سایت ادمین/پابلیک بالا نمی‌آید | Docker + nginx | `docker ps` · `docker logs vapp-admin` |

---

## ۴) لاگ فنی سرور (Exception / ۵۰۰)

### مسیرها

| محیط | مسیر |
|------|------|
| روی سرور (میزبان) | `/root/Api_Vapp_Manually/log/` یا `~/Api_Vapp_Manually/log/` |
| نام فایل | `log-YYYYMMDD.txt` (مثلاً `log-20260815.txt`) |
| داخل کانتینر | `/app/log` (همان پوشه mount شده) |

### دستورات روزمره

```bash
# لیست فایل‌ها
ls -lt ~/Api_Vapp_Manually/log/ | head

# دنبال کردن زنده (امروز)
tail -f ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt

# خطاهای امروز
grep -iE '\[ERR\]|exception|unhandled|fail' ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt | tail -80

# با TraceId که کاربر از اپ/پاسخ API داده
grep -F 'TRACE_ID_HERE' ~/Api_Vapp_Manually/log/log-YYYYMMDD.txt

# با شماره موبایل یا UserId
grep -F '0912xxxxxxx' ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt
grep -E 'UserId:? ?42\b|user 42\b' ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt

# لاگ زنده کانتینر (اگر فایل هنوز flush نشده)
docker logs --tail 200 vapp_api_prod 2>&1 | grep -iE 'error|exception|fail'
docker logs -f --tail 100 vapp_api_prod
```

### معنی سطح‌ها در فایل

- `[ERR]` → Exception / خطای جدی (اولویت پشتیبانی)
- `[WRN]` → هشدار (مثلاً OTP اشتباه، rate limit)
- `[INF]` → اطلاع عمومی

Unhandled exception معمولاً با `TraceId` در پیام Serilog می‌آید (`GlobalExceptionHandlerMiddleware`).

---

## ۵) Audit دیتابیس (کی چه کرد؟)

### ابزار آماده

```bash
cd ~/Api_Vapp_Manually

# آخرین ۵۰ رویداد
bash devops/scripts/audit-search.sh --lines 50

# یک دسته
bash devops/scripts/audit-search.sh --category payment --lines 100
bash devops/scripts/audit-search.sh --category wallet --lines 100
bash devops/scripts/audit-search.sh --category auth --lines 50
bash devops/scripts/audit-search.sh --category subscription --lines 50

# اکشن مشخص
bash devops/scripts/audit-search.sh --action Payment.VerifyFailed --lines 50
bash devops/scripts/audit-search.sh --action Subscription.Purchased --lines 50
bash devops/scripts/audit-search.sh --action Auth.AdminLoginFailed --lines 50
bash devops/scripts/audit-search.sh --action Auth.UserLoginFailed --lines 50
bash devops/scripts/audit-search.sh --action Auth.UserLoginSucceeded --lines 50
bash devops/scripts/audit-search.sh --action Auth.Logout --lines 50
bash devops/scripts/audit-search.sh --action Auth.OtpSent --lines 50
bash devops/scripts/audit-search.sh --action Sms.InsufficientBalance --lines 50
bash devops/scripts/audit-search.sh --action Subscription.Expired --lines 50

# کراول صحت لاگ پشتیبانی
BASE_URL=http://127.0.0.1:5054 bash devops/scripts/crawl-support-audit.sh

# کارهای یک کاربر/ادمین
bash devops/scripts/audit-search.sh --actor 5 --from 2026-08-14 --to 2026-08-16 --lines 200

# یک موجودیت
bash devops/scripts/audit-search.sh --entity-type Payment --entity-id 123 --lines 50

# جستجو داخل JSON قبل/بعد
bash devops/scripts/audit-search.sh --q-json 10890000 --lines 50
```

Categoryهای رایج: `admin` · `auth` · `subscription` · `payment` · `wallet` · `cashback` · `message` · `approval` · `user` · `role`

API ادمین (با توکن):

```bash
curl -sS "http://127.0.0.1:8080/api/Admin/Audit?page=1&pageSize=50" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

جزئیات بیشتر: [`ADMIN-AUDIT.md`](ADMIN-AUDIT.md) · [`AUDIT_RUNBOOK.md`](AUDIT_RUNBOOK.md)

### جداول مالی (وضعیت واقعی تراکنش)

وقتی Audit کافی نیست یا می‌خواهید وضعیت نهایی پول را ببینید:

```sql
-- آخرین پرداخت‌ها
SELECT TOP 50 Id, UserId, Amount, Status, Gateway, Authority, RefId, CreatedAt, UpdatedAt
FROM dbo.Payments
ORDER BY Id DESC;

-- یک Authority زرین‌پال
SELECT * FROM dbo.Payments WHERE Authority = N'A000...';

-- کیف‌پول کاربر
SELECT TOP 50 * FROM dbo.WalletTransactions
WHERE UserId = 42
ORDER BY Id DESC;
```

---

## ۶) سناریوهای پشتیبانی پرتکرار

### الف) «پرداخت کردم ولی اشتراک نیامد»

1. زمان + شماره/UserId را بگیرید.
2. Audit: `--category payment` و `--action Subscription.Purchased` برای همان بازه/`--actor`.
3. جدول `Payments`: Status باید Verified باشد و Authority/RefId جور باشد.
4. اگر VerifyFailed در Audit هست → لاگ فایل همان ساعت را با Authority یا UserId `grep` کنید.
5. اگر Payment OK ولی Subscription نیست → Audit `Subscription.Activated` / `Subscription.Purchased`.

### ب) «کیف‌پولم کم شد / اشتباه شارژ»

1. `bash devops/scripts/audit-search.sh --category wallet --actor USER_ID --lines 100`
2. `WalletTransactions` برای همان UserId.
3. Cashback/referral: `--category cashback` یا اکشن‌های `WalletReferral.*`.

### ج) «اپ ارور داد / صفحه سفید»

1. اگر پاسخ API `traceId` دارد → همان را در فایل لاگ روز `grep` کنید.
2. وگرنه بازه زمانی را در `log-YYYYMMDD.txt` با `[ERR]` بگردید.
3. مسیر درخواست (`RequestPath`) در Audit گاهی کمک می‌کند؛ ولی Exception معمولاً فقط در فایل است.

### د) «ادمین لاگین نمی‌شود»

```bash
bash devops/scripts/audit-search.sh --action Auth.AdminLoginFailed --lines 100
docker logs vapp_api_prod 2>&1 | grep 'AUDIT_ALERT type=AdminLoginFailSpike'
```

### ه) «SMS / کمپین نرفت»

```bash
bash devops/scripts/audit-search.sh --category message --lines 100
bash devops/scripts/audit-search.sh --category approval --lines 100
grep -iE 'sms|message|approval|kavenegar' ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt | tail -100
```

### و) «شماره‌جو کار نمی‌کند»

1. Audit: `--q NumberSeeker` یا اکشن‌های `NumberSeeker.*`
2. لاگ API: `grep -i NumberSeeker ~/Api_Vapp_Manually/log/log-$(date +%Y%m%d).txt`
3. لاگ ربات: `tail -100 ~/scraping_Number_Vapp/logs/api_server.log`  
   راهنما: [`NUMBER-SCRAPER.md`](NUMBER-SCRAPER.md)

---

## ۷) چیزهایی که فعلاً در Audit دیتابیس نیستند

وقت پشتیبانی را روی جدول اشتباه هدر ندهید:

| رویداد | Audit DB؟ | کجا بگردید؟ |
|--------|-----------|-------------|
| ورود موفق/ناموفق کاربر موبایل | بله — `Auth.UserLoginSucceeded` / `Auth.UserLoginFailed` | فایل لاگ + TraceId در پاسخ |
| Logout | بله — `Auth.Logout` | فایل لاگ |
| OTP ارسال / شکست / قفل | بله — `Auth.OtpSent` / `Auth.OtpSendFailed` / `Auth.OtpLocked` | فایل لاگ (بدون متن OTP) |
| خواندن لیست‌ها / GET معمولی | خیر | معمولاً لاگ نمی‌شود |
| هر Exception | خیر (مگر سرویس صریحاً بنویسد) | فایل لاگ + TraceId |
| کمبود موجودی / نتیجه SMS پولی | بله — `Sms.InsufficientBalance` / `Sms.SendSucceeded` / `Sms.SendFailed` | فایل لاگ |
| انقضای اشتراک | بله — `Subscription.Expired` (جاب پس‌زمینه) | — |
| 401/403 با Bearer | خیر (نمونه‌برداری در فایل) | `AUTH_DENY` در لاگ سرور |

---

## ۸) چک سلامت سریع (قبل از عمیق شدن در لاگ)

```bash
bash ~/Api_Vapp_Manually/devops/scripts/health-check.sh
docker ps
curl -sS -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8080/health
```

اگر API بالا نیست، اول کانتینر/دیسک/nginx را درست کنید؛ بعد لاگ فایل همان لحظهٔ restart را ببینید.

---

## ۹) نقشه فایل‌های مرتبط در devops

| فایل | نقش |
|------|-----|
| **این فایل** `SUPPORT-TROUBLESHOOTING.md` | نقطه شروع پشتیبانی |
| `SERVER-LOGS.md` | مسیر و دستورات کوتاه لاگ فایل/داکر |
| `ADMIN-AUDIT.md` | جدول `AdminAuditLogs` |
| `AUDIT_RUNBOOK.md` | سناریوهای جستجوی Audit |
| `scripts/audit-search.sh` | جستجوی SQL آماده |
| `NUMBER-SCRAPER.md` | لاگ و دیپلوی اسکرپر |
| `COMMANDS.txt` | cheat sheet دیپلوی + لینک لاگ |

---

## ۱۰) جریان پیشنهادی یک تیکت پشتیبانی (۱ دقیقه)

```
۱. زمان + شماره/UserId + ماژول (+ TraceId اگر هست)
۲. جدول بخش ۳ → انتخاب لایه
۳. اگر مالی/ادمین → audit-search.sh (+ در صورت نیاز Payments/Wallet)
۴. اگر کرش/۵۰۰ → grep روی log-YYYYMMDD.txt یا docker logs
۵. اگر شماره‌جو → لاگ اسکرپر هم چک شود
۶. یافته: Action / Status / Exception را در تیکت بنویسید
```
