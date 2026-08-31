# DevOps — چه کارهایی انجام شد (Aug 2026)

خلاصهٔ تغییرات CI/CD و deploy برای Vapp.  
**وضعیت production (۳۱ Aug 2026):** self-hosted runner فعال · آخرین deploy هر ۴ سرویس ✅

---

## مشکل اولیه

1. **GitHub hosted runner** نمی‌توانست SSH به VPS بزند (`Connection timed out`) — سرور فقط از IP ایران.
2. **Deploy از Mac** با `all` ~۲۰+ دقیقه (uplink کند + سه لایه سریالی).
3. **Admin/Public workflow** با `NODE_ENV=production` در deploy دوباره build می‌کرد → خطای TypeScript.

---

## راه‌حل نهایی: Self-hosted runner (hybrid)

| لایه | کجا اجرا می‌شود |
|------|------------------|
| CI + test | GitHub `ubuntu-latest` |
| Package (Docker image) | GitHub `ubuntu-latest` |
| **Deploy** | **VPS** — `[self-hosted, vapp-prod]` |

۴ runner روی VPS (حساب GitHub **User** → یک runner per repo):

| Runner | ریپo | مسیر |
|--------|------|------|
| `vapp-prod-api` | Api_Vapp_Manually | `/opt/actions-runner-vapp-api` |
| `vapp-prod-admin` | Admin_Pannel_Vapp | `/opt/actions-runner-vapp-admin` |
| `vapp-prod-public` | PublicWeb_Vapp | `/opt/actions-runner-vapp-public` |
| `vapp-prod-scraper` | scraping_Number_Vapp | `/opt/actions-runner-vapp-scraper` |

---

## فایل‌های جدید

| فایل | نقش |
|------|-----|
| `scripts/setup-github-self-hosted-runner.sh` | نصب یک runner |
| `scripts/install-all-vapp-github-runners.sh` | نصب هر ۴ runner از Mac |
| `SELF_HOSTED_RUNNER.md` | راهنمای runner |
| `DEPLOY-FLOW.md` | فرایند قدم‌به‌قدم |
| `PUSH-AND-DEPLOY.md` | ★ push + deploy یک‌صفحه‌ای (شروع اینجا) |
| `scripts/push-and-deploy.sh` | دستور یک‌جا: push GitHub + deploy |
| `DEPLOY-TIMING.md` | زمان‌بندی Mac vs GitHub |
| `WHAT-WAS-DONE.md` | همین سند |

---

## Workflow تغییرات (هر ۴ ریپo)

- **API / Scraper:** job `package` (build Docker + artifact) → job `deploy` روی VPS (`docker load`)
- **Admin / Public:** CI → artifact `dist` → deploy روی VPS (`rsync` محلی)
- حذف SSH از job Deploy
- Admin health: قبول `301` (redirect HTTPS)

---

## Mac deploy — هنوز موجود

| اسکریپت | کاربرد |
|---------|--------|
| `deploy-from-mac.sh` | hotfix بدون انتظار CI |
| `deploy-api-upload-image.sh` | stream upload بهبود یافته |
| `vpn-bypass-vapp-server.sh` | Cursor + VPN |

---

## مقایسه با microless

| | microless | Vapp (جدید) |
|---|-----------|-------------|
| CD از GitHub | SSH به `:3031` | self-hosted روی VPS |
| Front deploy | ~۵–۸ min (Docker) | Admin/Public ~**۲ min** (static) |
| API deploy | ~۶–۱۰ min | ~**۱۴ min** (artifact download) |

---

## دستور روزمره (بعد از setup)

```bash
bash devops/scripts/push-and-deploy.sh   # push + deploy prod
# یا
git push origin main   # همان ریپویی که عوض شده
```

---

## بررسی سلامت

```bash
# روی VPS
bash /root/Api_Vapp_Manually/devops/scripts/post-deploy-verify.sh

# runnerها
systemctl status 'actions.runner.*'
```

---

## مستندات به‌روز

- [`CI_CD.md`](CI_CD.md) · [`CI_CD_QUICK.md`](CI_CD_QUICK.md)
- [`COMMANDS.txt`](COMMANDS.txt) · [`README.md`](README.md)
