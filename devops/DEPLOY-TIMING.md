# زمان Deploy — چرا گاهی ۲۰+ دقیقه؟ (و چطور ~۶ دقیقه)

## ★ قانون طلایی

**فقط همان لایه‌ای را deploy کن که عوض شده** — نه `all` هر بار.

| سناریو | دستور | زمان واقعی (با cache + uplink معمول) |
|--------|--------|-------------------------------------|
| فقط C# / API | `deploy-from-mac.sh api` | **۴–۷ دقیقه** |
| فقط Admin | `deploy-from-mac.sh admin` | **۲–۴ دقیقه** |
| dist Admin آماده | `deploy-from-mac.sh admin-fast` | **~۳۰ ثانیه** |
| فقط Public | `deploy-from-mac.sh public` | **۱–۳ دقیقه** |
| فقط restart API | `deploy-from-mac.sh api-restart` | **۱–۳ دقیقه** |
| **هر سه + rebuild کامل** | `deploy-from-mac.sh all` | **۱۵–۲۵ دقیقه** ⚠️ |
| Scraper (Chromium) | `scraping_Number_Vapp/... deploy-from-mac.sh api` | **۱۵–۴۰ دقیقه** ⚠️ |

مقایسه با microless (~۶ دقیقه): آنجا معمولاً **یک سرویس** (API **یا** Front) deploy می‌شود، image کوچک‌تر/کش گرم‌تر، و uplink گاهی سریع‌تر است.

---

## تجزیه deploy «کامل» (all) — Aug 2026 واقعی

آخرین `deploy-from-mac.sh all` روی Mac:

| مرحله | زمان | توضیح |
|--------|------|--------|
| Public build + rsync | ~۱ دقیقه | Vite — سبک |
| Admin build + rsync | ~۳–۵ دقیقه | npm + Vite |
| API Docker build | ~۵–۶ دقیقه | dotnet restore + publish (اولین بار بدون cache گرم) |
| **API image upload** | **~۱۲ دقیقه** | ۱۱۲MB zst @ ~۱۳۰ KB/s uplink |
| git sync + restart + wait DB | ~۲ دقیقه | |
| **جمع `all`** | **~۲۱ دقیقه** | |

Scraper جدا (~۳۷ دقیقه): image Chromium + ODBC + pip + upload ~۱GB+ — **هرگز با `all` قاطی نکن**.

---

## سه علت اصلی کندی

### ۱) پهنای uplink Mac → سرور (بزرگ‌ترین عامل)

```
112MB ÷ 130 KB/s ≈ 14 دقیقه تئوری
```

- rsync/resume امن است ولی **سرعت uplink** سقف می‌گذارد.
- microless از همان الگو `docker save | gzip | ssh` استفاده می‌کند — اگر uplink سریع‌تر باشد، همان pipeline ~۳–۶ دقیقه می‌شود.

**راه‌حل:** فقط وقتی C# عوض شده `api` بزن؛ image را دوباره نفرست اگر `--no-deploy` یا `api-restart` کافی است.

### ۲) deploy چند لایه پشت سر هم (`all`)

`all` = Public → Admin → API **سریالی**. هر کدام build جدا.

**راه‌حل:** `all-parallel` (Public+Admin موازی) یا جداگانه:

```bash
bash devops/scripts/deploy-from-mac.sh admin   # فقط UI
bash devops/scripts/deploy-from-mac.sh api     # فقط backend
```

### ۳) rebuild بدون cache / اولین deploy

- اولین build بعد از clone: NuGet/npm/Docker layer cache سرد → +۵–۱۰ دقیقه
- deploy CI/CD اول: همه چیز از صفر

**راه‌حل:** deploy دوم همان روز معمولاً **نصف زمان** است.

---

## مسیر سریع (~۶ دقیقه) — روزمره

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually

# فقط API (تغییر C#)
bash devops/scripts/deploy-from-mac.sh api
# → build cache + stream upload: معمولاً ۴–۷ min

# فقط Admin (اگر dist از قبل build کردی)
cd ../Admin_Vapp && npm run build && cd ../Api_Vapp_Manually
bash devops/scripts/deploy-from-mac.sh admin-fast
# → ~۳۰ sec

bash devops/scripts/deploy-from-mac.sh health
```

---

## متغیرهای سرعت (اسکریپت API)

| Env | اثر |
|-----|-----|
| `STREAM_UPLOAD=1` | (پیش‌فرض) pipe مستقیم `docker save \| zstd \| ssh` — مثل microless، بدون فایل temp |
| `USE_RSYNC=1` | upload resumable — اگر uplink قطع می‌شود |
| `SKIP_GIT_SYNC=1` | فقط image عوض شده، devops روی سرور همان است |
| `ZSTD_LEVEL=1` | فشرده‌سازی سریع‌تر (فایل کمی بزرگ‌تر) |
| `SKIP_BUILD=1` | image محلی موجود — فقط upload |

مثال upload سریع بدون rebuild:

```bash
SKIP_BUILD=1 STREAM_UPLOAD=1 bash devops/scripts/deploy-api-upload-image.sh
```

---

## GitHub Actions vs Mac

| | GitHub CI | GitHub CD | Mac CD |
|---|-----------|-----------|--------|
| Build/Test | ✅ ~۳–۵ min | — | — |
| Deploy | — | ❌ SSH timeout از runner | ✅ |
| uplink | — | — | محدودیت ISP Mac |

CD عملی: **Mac** (`deploy-from-mac.sh`). جزئیات: [`CI_CD.md`](CI_CD.md)

---

## چک‌لیست «چرا دیر شد؟»

1. `all` یا scraper+zapp با هم زدی؟ → جدا deploy کن
2. uplink کند بود؟ → `bash devops/scripts/server-net-check.sh` (روی سرور) + speedtest Mac
3. اولین build/cache سرد؟ → بار دوم سریع‌تر
4. Scraper stop شد برای API RAM؟ → بعد API دوباره scraper را deploy کن

---

## فایل‌های مرتبط

- [`MAC-QUICK-DEPLOY.md`](MAC-QUICK-DEPLOY.md) — کدام mode
- [`COMMANDS.txt`](COMMANDS.txt) — همه دستورات
- [`CI_CD_QUICK.md`](CI_CD_QUICK.md) — GitHub
