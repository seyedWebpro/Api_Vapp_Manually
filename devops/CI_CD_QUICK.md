# CI/CD — یک‌صفحه‌ای (Vapp)

---

## ★ آپدیت production (روش اصلی)

```bash
bash devops/scripts/push-and-deploy.sh           # push + deploy prod (~14 min)
bash devops/scripts/push-and-deploy.sh --admin   # فقط Admin (~2 min)
# یا
git push origin main
```

راهنما: [`PUSH-AND-DEPLOY.md`](PUSH-AND-DEPLOY.md)

| حالت | دستور | زمان (تقریبی) |
|------|--------|----------------|
| فقط Admin | `--admin --watch` | **~۲ min** |
| فقط Public | `--public --watch` | **~۱–۲ min** |
| فقط API | `--api --watch` | **~۱۴ min** |
| prod (API+Admin+Public موازی) | `--prod --watch` | **~۱۴ min** (API کندترین) |
| + Scraper | `--all --watch` | **~۳۵ min** |

جزئیات: [`DEPLOY-TIMING.md`](DEPLOY-TIMING.md) · قدم‌ها: [`DEPLOY-FLOW.md`](DEPLOY-FLOW.md)

---

## چه اتفاقی می‌افتد؟

```
push → CI (GitHub) → Package [API/Scraper] → Deploy (VPS runner) → verify
```

- **بدون SSH از Mac**
- Admin/Public: dist آماده → rsync محلی (~۳۰–۴۵ sec)
- API: artifact Docker → load روی VPS (~۹ min)

---

## Mac (hotfix)

```bash
bash devops/scripts/deploy-from-mac.sh admin   # ~۲–۴ min
```

---

## Actions

| سرویس | URL |
|--------|-----|
| API | https://github.com/seyedWebpro/Api_Vapp_Manually/actions |
| Admin | https://github.com/seyedWebpro/Admin_Pannel_Vapp/actions |
| Public | https://github.com/seyedWebpro/PublicWeb_Vapp/actions |
| Scraper | https://github.com/seyedWebpro/scraping_Number_Vapp/actions |

---

## عیب‌یابی

| مشکل | راه‌حل |
|------|--------|
| Deploy queued | [`SELF_HOSTED_RUNNER.md`](SELF_HOSTED_RUNNER.md) |
| SSH timeout (قدیم) | workflow جدید — runner self-hosted |

---

[`CI_CD.md`](CI_CD.md) · [`WHAT-WAS-DONE.md`](WHAT-WAS-DONE.md)
