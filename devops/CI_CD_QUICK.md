# CI/CD — یک‌صفحه‌ای (Vapp)

---

## ★ سریع‌ترین راه — آپدیت API + Admin + Public

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/gh-deploy-production.sh --prod --watch
```

با push قبل از deploy:

```bash
bash devops/scripts/gh-deploy-production.sh --prod --push --watch
```

| حالت | دستور |
|------|--------|
| فقط API | `bash devops/scripts/gh-deploy-production.sh --api --watch` |
| فقط Admin | `bash devops/scripts/gh-deploy-production.sh --admin --watch` |
| فقط Public | `bash devops/scripts/gh-deploy-production.sh --public --watch` |
| فقط Scraper | `bash devops/scripts/gh-deploy-production.sh --scraper --watch` |
| prod (API+Admin+Public) | `bash devops/scripts/gh-deploy-production.sh --prod --watch` |
| همه (+ Scraper) | `bash devops/scripts/gh-deploy-production.sh --all --watch` |
| تست بدون اجرا | `bash devops/scripts/gh-deploy-production.sh --prod --dry-run` |

**زمان تقریبی:** CI ~۳–۵ دقیقه · Deploy API ~۴–۶ دقیقه · Admin/Public ~۲–۴ دقیقه · Scraper ~۸–۱۵ دقیقه (Chromium image)

---

## ★ سریع از Mac (بدون GitHub — hotfix)

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/deploy-from-mac.sh api
bash devops/scripts/deploy-from-mac.sh admin
bash devops/scripts/deploy-from-mac.sh public
bash devops/scripts/deploy-from-mac.sh health
```

---

## چه اتفاقی می‌افتد؟ (GitHub CD)

```
push به main
    → CI (build + test)
    → Deploy job
        API/Scraper: build Docker روی runner → SSH → docker load → restart
        Admin/Public: npm build روی runner → rsync dist → apply nginx
        → post-deploy-verify.sh
```

**مهم:** سرور از GHCR pull نمی‌کند — image/dist با SSH فرستاده می‌شود (مثل Mac).

---

## لینک‌های Actions

| سرویس | Actions |
|--------|---------|
| API | https://github.com/seyedWebpro/Api_Vapp_Manually/actions |
| Admin | https://github.com/seyedWebpro/Admin_Pannel_Vapp/actions |
| Public | https://github.com/seyedWebpro/PublicWeb_Vapp/actions |
| Scraper | https://github.com/seyedWebpro/scraping_Number_Vapp/actions |

---

## Secrets (خلاصه)

| ریپو | Secrets |
|------|---------|
| همه | `VAPP_SSH_PRIVATE_KEY`, `VAPP_SSH_HOST`, `VAPP_SSH_PORT`, `VAPP_SSH_USER` |

هر ریپو: Environment **`production`**

جزئیات + لینک‌های گام‌به‌گام: [`CI_CD.md`](CI_CD.md)

---

## عیب‌یابی یک‌خطی

| مشکل | راه‌حل |
|------|--------|
| `Connection refused` SSH | `VAPP_SSH_PORT` = **22** · VPN split tunnel — [`MAC-SERVER.md`](MAC-SERVER.md) |
| Deploy skip شد | push به `main` باشد |
| Mac سریع‌تر | `bash devops/scripts/deploy-from-mac.sh api` |
