# CI/CD — GitHub Actions (Phase 1 + Phase 2)

اتوماسیون رسمی برای چهار ریپوی Vapp. مسیر CD با اسکریپت‌های Mac یکی است: **بیلد بیرون سرور → SSH** (image با `docker load` یا static با `rsync` — نه `docker pull` از GHCR روی VPS).

| ریپو | Workflow | Branch | CD target |
|------|----------|--------|-----------|
| [Api_Vapp_Manually](https://github.com/seyedWebpro/Api_Vapp_Manually) | `.github/workflows/ci-cd.yml` | `main` | `195.24.237.132:22` — Docker `vapp-api` |
| [Admin_Pannel_Vapp](https://github.com/seyedWebpro/Admin_Pannel_Vapp) | `.github/workflows/ci-cd.yml` | `main` | همان سرور — `/var/www/vapp-admin` |
| [PublicWeb_Vapp](https://github.com/seyedWebpro/PublicWeb_Vapp) | `.github/workflows/ci-cd.yml` | `main` | همان سرور — `/var/www/vapp-public` |
| [scraping_Number_Vapp](https://github.com/seyedWebpro/scraping_Number_Vapp) | `.github/workflows/ci-cd.yml` | `main` | همان سرور — Docker `phonescraper_api:latest` |

**تفاوت با microless:** Vapp تک‌سرور است (API + Admin + Public + Scraper روی یک VPS). پورت SSH **`22`** است (نه 3031).

> **⚠️ محدودیت CD از GitHub Actions:** runnerهای GitHub از IP خارج به سرور `195.24.237.132:22` **SSH timeout** می‌گیرند (فایروال/VPS). **CI روی GitHub کار می‌کند**؛ **CD عملی از Mac** با `deploy-from-mac.sh` یا `gh-deploy-production.sh` (بعد از self-hosted runner / باز کردن IP). جزئیات پایین.

---

## ★ یک دستور — آپدیت API + Admin + Public

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/gh-deploy-production.sh --prod --watch
```

با push: `--push` اضافه کن. جزئیات: [`CI_CD_QUICK.md`](CI_CD_QUICK.md)

---

## کارهایی که شما باید در GitHub انجام دهید

ترتیب پیشنهادی: اول Secrets + Environment، بعد commit/push کد CI.

---

### A) Environment به نام `production` (هر چهار ریپو)

برای **هر ریپو** این کار را تکرار کن:

1. Settings → Environments → **New environment**
2. Name دقیقاً: `production` → **Configure environment**
3. تیک **Required reviewers** → خودت را Add کن → **Save protection rules**

بدون Required reviewers، Deploy بلافاصله اجرا می‌شود. با آن، بعد از سبز شدن CI باید **Approve and deploy** بزنی.

لینک‌ها:
- API: https://github.com/seyedWebpro/Api_Vapp_Manually/settings/environments
- Admin: https://github.com/seyedWebpro/Admin_Pannel_Vapp/settings/environments
- Public: https://github.com/seyedWebpro/PublicWeb_Vapp/settings/environments
- Scraper: https://github.com/seyedWebpro/scraping_Number_Vapp/settings/environments

---

### B) Repository secrets

Secrets مشترک برای **هر چهار ریپو** (همان مقادیر):

| Name | Value |
|------|--------|
| `VAPP_SSH_PRIVATE_KEY` | کل فایل کلید خصوصی Mac (پایین) |
| `VAPP_SSH_HOST` | `195.24.237.132` |
| `VAPP_SSH_PORT` | `22` |
| `VAPP_SSH_USER` | `root` |

> Vapp احراز هویت ادمین با **OTP** است — secret رمز عبور لازم نیست.

کلید خصوصی:

```bash
cat ~/.ssh/id_ed25519_vapp_server
```

> همان کلیدی که `ssh vapp-prod` با آن کار می‌کند — [`MAC-SERVER.md`](MAC-SERVER.md)

---

### میان‌بر CLI (اگر `gh` لاگین است)

```bash
KEY=~/.ssh/id_ed25519_vapp_server
for REPO in Api_Vapp_Manually Admin_Pannel_Vapp PublicWeb_Vapp scraping_Number_Vapp; do
  gh secret set VAPP_SSH_PRIVATE_KEY -R "seyedWebpro/$REPO" < "$KEY"
  gh secret set VAPP_SSH_HOST     -R "seyedWebpro/$REPO" -b '195.24.237.132'
  gh secret set VAPP_SSH_PORT     -R "seyedWebpro/$REPO" -b '22'
  gh secret set VAPP_SSH_USER     -R "seyedWebpro/$REPO" -b 'root'
done
```

Environment را با UI بساز (CLI environment + reviewer سخت‌تر است).

---

### C) Push فایل‌های CI — **ترتیب مهم**

**اول API** (شامل devops scripts) — بعد Admin / Public / Scraper:

```bash
# 1) API — حتماً اول
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
git add .github/workflows/ci-cd.yml devops/
git commit -m "Add GitHub Actions CI/CD"
git push origin main

# 2) Admin, Public, Scraper — بعد از push API
```

Admin/Public/Scraper در deploy، اسکریپت verify را از `main` ریپو API sparse-checkout می‌کنند.

**فقط فایل‌های CI/CD را commit کن** — نه `.env`، نه `log/`، نه `node_modules`.

بعد از push به `main`:

1. Job **Build & Test / Verify** سبز شود
2. Job **Deploy** روی **Review deployments** بایستد (اگر Required reviewers فعال است)
3. **Approve and deploy** را بزن

---

## رفتار pipeline

1. push/PR به `main`/`master`/`develop` → CI
2. push به `main`/`master` (+ workflow_dispatch با Deploy) → بعد از CI، Deploy با `environment: production`
3. `develop` → فقط CI، بدون CD

---

## رابطه با Mac

| سناریو | ابزار |
|--------|--------|
| hotfix کوچک | `bash devops/scripts/deploy-from-mac.sh api` |
| release از GitHub | push → Approve production |
| اضطراری | اسکریپت‌های همین پوشه [`COMMANDS.txt`](COMMANDS.txt) |

---

## عیب‌یابی

| علامت | کار |
|-------|-----|
| Missing secrets | بخش B |
| `Permission denied (publickey)` | همان private key که `ssh vapp-prod` با آن کار می‌کند |
| Deploy منتظر Approve | Environment → Required reviewers |
| API health ≠ 200 | `ssh vapp-prod 'docker logs vapp_api_prod --tail 80'` |
| Admin 404 | `ssh vapp-prod 'ls -la /var/www/vapp-admin/'` |
| Scraper health ≠ 200 | `ssh vapp-prod 'docker logs phonescraper_api_prod --tail 80'` |
| Mac سریع‌تر | `bash devops/scripts/deploy-from-mac.sh health` |
| GHA Deploy: `Connection timed out` SSH | سرور IP GitHub Actions را نمی‌بیند — CD از Mac: `bash devops/scripts/deploy-from-mac.sh all` |
| Secrets: `IRAN_SSH_*` اضافه | workflow فقط `VAPP_SSH_*` می‌خواند — هر دو OK ولی `VAPP_SSH_PRIVATE_KEY` **الزامی** |

---

تکرار روی پروژه دیگر: [`CI_CD_REPLICATE.md`](CI_CD_REPLICATE.md)
