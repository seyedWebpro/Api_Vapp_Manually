# فرایند Deploy — قدم‌به‌قدم (ساده)

دو مسیر اصلی: **GitHub (پیشنهادی)** و **Mac (hotfix)**.

---

## مسیر A — GitHub Actions (روزمره)

### تو چه می‌زنی؟

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
git push origin main                    # فقط ریپویی که عوض شده
# یا
bash devops/scripts/gh-deploy-production.sh --admin --push --watch
```

### بعدش چه می‌شود؟

```
┌─────────────────────────────────────────────────────────────┐
│ ۱) push به GitHub                                             │
└───────────────────────────────┬─────────────────────────────┘
                                ▼
┌─────────────────────────────────────────────────────────────┐
│ ۲) CI — روی سرورهای GitHub (آمریکا/اروپا)                    │
│    • API: dotnet test                                         │
│    • Admin/Public: npm ci + vite build                        │
│    • Scraper: python tests                                    │
└───────────────────────────────┬─────────────────────────────┘
                                ▼
┌─────────────────────────────────────────────────────────────┐
│ ۳) Package — فقط API و Scraper (روی GitHub)                 │
│    • docker build → docker save → آپلود artifact             │
└───────────────────────────────┬─────────────────────────────┘
                                ▼
┌─────────────────────────────────────────────────────────────┐
│ ۴) Approve (اگر Environment production + Required reviewer) │
│    GitHub → Actions → Review deployments → Approve            │
└───────────────────────────────┬─────────────────────────────┘
                                ▼
┌─────────────────────────────────────────────────────────────┐
│ ۵) Deploy — روی VPS ایران (self-hosted runner)              │
│    • API/Scraper: دانلود artifact → docker load → restart    │
│    • Admin/Public: دانلود dist → rsync → nginx               │
│    • post-deploy-verify.sh                                    │
└───────────────────────────────┬─────────────────────────────┘
                                ▼
                         ✅ production آپدیت شد
```

**SSH از Mac لازم نیست.** runner داخل VPS کار را انجام می‌دهد.

---

## مسیر B — Mac (hotfix / وقتی GitHub در دسترس نیست)

```bash
bash devops/scripts/deploy-from-mac.sh admin
```

```
Mac: build → upload با SSH → restart/rsync روی VPS
```

نیاز: `ssh vapp-prod` کار کند (VPN bypass اگر فیلترشکن روشن است).

---

## فقط یک سرویس عوض شده — چه push کنم؟

| تغییر در | push به | trigger خودکار |
|---------|---------|----------------|
| C# / API | `Api_Vapp_Manually` | API CI/CD |
| Admin UI | `Admin_Pannel_Vapp` | Admin CI/CD |
| Public form/wheel | `Public_Vapp` | Public CI/CD |
| Scraper Python | `scraping_Number_Vapp` | Scraper CI/CD |

**فقط همان ریپo را push کن** — بقیه deploy نمی‌شوند.

---

## `gh-deploy-production.sh` چه می‌کند؟

| فلگ | کار |
|-----|-----|
| `--push` | قبل از trigger، `git push` ریپوهای انتخاب‌شده |
| `--watch` | منتظر می‌ماند تا workflow تمام شود |
| `--api` / `--admin` / `--public` / `--scraper` | فقط همان workflow را dispatch می‌کند |
| `--prod` | API + Admin + Public (پیش‌فرض) |

---

## فایل‌های مرتبط

- [`DEPLOY-TIMING.md`](DEPLOY-TIMING.md) — زمان هر مرحله
- [`CI_CD_QUICK.md`](CI_CD_QUICK.md) — دستورات یک‌خطی
- [`SELF_HOSTED_RUNNER.md`](SELF_HOSTED_RUNNER.md) — runner روی VPS
