# تکرار CI/CD Vapp روی پروژه / سورس دیگر

این سند توضیح می‌دهد **در Vapp چه ساختیم** و چطور همان الگو را روی سایت بعدی پیاده کنی.

---

## معماری

```
┌─────────────────┐     push main           ┌──────────────────┐
│  GitHub Repo    │ ──────────────────────► │ GitHub Actions   │
│  (API/Admin/    │                         │  CI: build+test  │
│   Public/       │                         │  CD: deploy job│
│   Scraper)      │                         └────────┬─────────┘
└─────────────────┘                                  │ SSH :22
                                                     ▼
                                          Production VPS (تک‌سرور)
                                          · API docker :8080
                                          · Admin static /var/www/...
                                          · Public static /var/www/...
                                          · Scraper docker :8000
```

### فایل‌های کلیدی

| فایل | نقش |
|------|-----|
| `.github/workflows/ci-cd.yml` | pipeline اصلی (هر ریپو) |
| `devops/CI_CD.md` | secrets و راهنما (مرجع در ریپو API) |
| `devops/scripts/gh-deploy-production.sh` | trigger یک‌خطی از Mac |
| `devops/scripts/post-deploy-verify.sh` | smoke بعد از deploy |

### تفاوت چهار سرویس

| | API | Admin | Public | Scraper |
|---|-----|-------|--------|---------|
| Branch | `main` | `main` | `main` | `main` |
| CI | .NET build + smoke tests | npm build | npm build | Python unit tests |
| CD | Docker → SSH load | rsync dist | rsync dist | Docker → SSH load |
| Secrets | `VAPP_SSH_*` | همان | همان | همان |

---

## چک‌لیست — پروژه جدید

### فاز ۱ — کپی devops

```bash
cp -R /path/to/vapp/Api_Vapp_Manually/devops /path/to/NewProject/devops
```

ویرایش:
- `server.conf` — IP، SSH، مسیرها
- `domain/env.domain.example`
- `deploy/nginx-*.conf.example`

### فاز ۲ — workflow در هر ریپو

1. کپی `.github/workflows/ci-cd.yml` از ریپوی مشابه Vapp
2. عوض کن:
   - `REMOTE_*_DIR`
   - image names / static paths
   - health URLs
   - secret prefix (مثلاً `MYAPP_SSH_*`)

### فاز ۳ — GitHub Secrets + Environment

برای **هر ریپو**:
1. Environment → `production`
2. Secrets → `VAPP_SSH_*` (یا prefix جدید)

### فاز ۴ — تست

```bash
bash devops/scripts/gh-deploy-production.sh --api --dry-run
bash devops/scripts/gh-deploy-production.sh --api --watch
```

---

## بهبودهای نسبت به microless

| موضوع | microless | Vapp (بهبود) |
|-------|-----------|--------------|
| Admin/Public CD | Docker image | **rsync static** — سریع‌تر، هم‌راستا با Mac |
| Secret prefix | `IRAN_SSH_*` / `FOREIGN_SSH_*` | **`VAPP_SSH_*`** — تک سرور |
| Scraper CI | `requirements-ci.txt` | همان + `requirements-ci.txt` سبک |
| Post-deploy | `post-deploy-verify-iran.sh` | `post-deploy-verify.sh` — Vapp endpoints |
| Concurrency | ✅ | ✅ + quality gate TRX برای API |

---

## الگوی workflow — نکات مهم

1. **هرگز `docker pull ghcr.io` روی VPS ایران** — SSH + `docker load` یا rsync
2. **پورت SSH Vapp = 22**
3. **تست کامل .NET** — gate روی subset پایدار (unit tests)
4. **Concurrency** — run قدیمی همان branch کنسل شود
5. **deploy فقط `main`** — `develop` فقط CI

---

## فایل‌های مرجع Vapp

| سند | محتوا |
|-----|--------|
| [`CI_CD.md`](CI_CD.md) | راهنمای کامل secrets |
| [`CI_CD_QUICK.md`](CI_CD_QUICK.md) | یک‌خطی‌ها |
| [`MAC-SERVER.md`](MAC-SERVER.md) | SSH setup |
| [`COMMANDS.txt`](COMMANDS.txt) | deploy دستی |
