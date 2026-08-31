# زمان Deploy — Mac vs GitHub (self-hosted)

**آخرین اندازه‌گیری واقعی:** ۳۱ Aug 2026 (runهای موفق GitHub Actions)

---

## ★ چرا API (~۱۴ min) خیلی بیشتر از فرانت (~۲ min)?

| | **فرانت (Admin/Public)** | **بکند (API)** |
|---|--------------------------|----------------|
| نوع deploy | فایل static | Docker image |
| حجم انتقال | ~۲–۵ MB (dist) | ~۱۰۰+ MB فشرده (artifact) |
| build | Vite ~۳۰–۴۵ sec | dotnet test + docker build ~۴ min |
| انتقال به VPS | دانلود dist + rsync محلی ~۳۰ sec | **دانلود artifact GitHub → ایران ~۹ min** |
| restart | nginx reload | docker load + recreate + SQL migration |

**یک جمله:** فرانت «چند فایل سبک» است؛ API «image سنگین Docker» است که باید از GitHub به سرور ایران بیاید — **این ~۹ دقیقه گلوگاه اصلی است.**

Mac hotfix API (`deploy-from-mac.sh api`) ~۴–۷ min است چون uplink Mac مستقیم است، ولی GitHub CD uplink Mac نمی‌خواهد.

---

## ★ قانون طلایی

**فقط همان لایه‌ای را deploy کن که عوض شده.**

---

## GitHub Actions + self-hosted (روش جدید — پیشنهادی)

زمان از **push** تا **production live** (runner Idle، cache گرم):

| سرویس | کل workflow | CI | Package | Deploy روی VPS | vs microless |
|--------|-------------|-----|---------|----------------|--------------|
| **Admin** | **~۲ min** | ~۴۵ sec | — | ~۴۵ sec (rsync) | microless Front ~۵–۸ min → **Vapp سریع‌تر** |
| **Public** | **~۱–۲ min** | ~۳۰ sec | — | ~۳۰ sec | همان |
| **API** | **~۱۴ min** | ~۱ min | ~۳ min | ~۹ min (download artifact + load) | microless API ~۶–۱۰ min |
| **Scraper** | **~۳۰–۳۵ min** | ~۱۵ sec | ~۳ min | ~۲۵–۲۸ min (image بزرگ) | جدا deploy کن |

### `prod` (API + Admin + Public) — اگر همزمان push شوند

Workflowها **موازی** اجرا می‌شوند → زمان wall-clock ≈ **کندترین لایه ≈ ~۱۴ min (API)**، نه جمع سه‌تایی.

| | Mac `all` (قدیم) | GitHub `--prod` (جدید) |
|---|------------------|------------------------|
| Admin+Public+API | ~۲۱ min سریالی + uplink Mac | ~۱۴ min موازی (API محدودکننده) |
| نیاز Mac online | ✅ | ❌ (فقط push) |

---

## تجزیه Admin (~۲ min) — run واقعی

| مرحله | زمان |
|--------|------|
| Verify & build (GitHub) | ~۴۵ sec |
| Deploy (VPS: download dist + rsync + nginx) | ~۴۵ sec |
| **جمع** | **~۱ min ۴۲ sec** |

**مثل microless Front (~۳–۶ min) — حتی کمی سریع‌تر** چون static است نه Docker image.

---

## تجزیه API (~۱۴ min) — run واقعی

| مرحله | زمان | کجا |
|--------|------|-----|
| Build & Test | ~۱ min ۱۶ sec | GitHub |
| Package API image | ~۳ min ۶ sec | GitHub |
| Deploy (download artifact + docker load + restart + wait DB) | ~۹ min ۱۴ sec | VPS |
| **جمع** | **~۱۳ min ۴۸ sec** | |

گلوگاه Deploy API: **دانلود artifact ~۱۰۰MB از GitHub به VPS ایران** (نه SSH از Mac).

---

## Mac deploy (hotfix — هنوز موجود)

| سناریo | دستور | زمان |
|--------|--------|------|
| فقط Admin | `deploy-from-mac.sh admin` | ~۲–۴ min |
| Admin dist آماده | `admin-fast` | ~۳۰ sec |
| فقط Public | `public` | ~۱–۳ min |
| فقط API | `api` | ~۴–۷ min (build + uplink Mac) |
| **all** سریالی | `all` | **~۱۵–۲۵ min** ⚠️ |
| Scraper | scraper repo `deploy-from-mac.sh api` | ~۱۵–۴۰ min |

Mac هنوز برای **hotfix فوری** یا وقتی GitHub Actions down است.

---

## چرا Mac `all` ~۲۱ min بود؟

1. **Uplink Mac → VPS** ~۱۲ min برای API image
2. Public + Admin + API **سریالی**
3. Build سرد اولین بار

GitHub CD این‌ها را حل می‌کند:
- بدون uplink Mac
- Admin/Public deploy **ثانیه‌ای** روی VPS
- API/Scraper: build روی GitHub (سریع)، فقط artifact به VPS می‌آید

---

## Approve دستی

اگر Environment `production` با **Required reviewers** فعال است، بین CI و Deploy چند دقیقه تا چند ساعت **توقف دستی** اضافه می‌شود (در جدول بالا نیست).

---

## چک‌لیست «چرا دیر شد؟»

1. فقط API push کردی ولی `--prod` زدی؟ → `--api` بزن
2. Runner Offline? → `systemctl status 'actions.runner.*'`
3. Scraper + API با هم? → جدا deploy کن
4. منتظر Approve? → GitHub → Review deployments

---

## فایل‌های مرتبط

- [`DEPLOY-FLOW.md`](DEPLOY-FLOW.md) — قدم‌به‌قدم
- [`CI_CD_QUICK.md`](CI_CD_QUICK.md) — دستورات
- [`MAC-QUICK-DEPLOY.md`](MAC-QUICK-DEPLOY.md) — Mac modes
