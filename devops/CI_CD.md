# CI/CD — GitHub Actions (Phase 1 + Phase 2)

اتوماسیون برای چهار ریپوی Vapp.

**CD = self-hosted runner روی VPS** (`runs-on: [self-hosted, vapp-prod]`) — سرور فقط از IP ایران reachable است؛ GitHub hosted runner نمی‌تواند SSH inbound بزند.

| ریپo | CI (GitHub hosted) | CD (self-hosted) |
|------|-------------------|------------------|
| [Api_Vapp_Manually](https://github.com/seyedWebpro/Api_Vapp_Manually) | build + test | docker load + restart |
| [Admin_Pannel_Vapp](https://github.com/seyedWebpro/Admin_Pannel_Vapp) | build dist | rsync → `/var/www/vapp-admin` |
| [PublicWeb_Vapp](https://github.com/seyedWebpro/PublicWeb_Vapp) | build dist | rsync → `/var/www/vapp-public` |
| [scraping_Number_Vapp](https://github.com/seyedWebpro/scraping_Number_Vapp) | unit tests | docker load + restart |

راهنمای کامل runner: **[`SELF_HOSTED_RUNNER.md`](SELF_HOSTED_RUNNER.md)**

---

## ★ پیش‌نیاز — نصب runner (یک‌بار)

### ۱) Token از GitHub (Org — هر ۴ ریپo)

https://github.com/organizations/seyedWebpro/settings/actions/runners/new  
→ Linux → copy registration token (~۱ ساعت اعتبار)

### ۲) نصب روی VPS

```bash
cd /root/Api_Vapp_Manually
git pull origin main   # بعد از push workflow + devops
bash devops/scripts/setup-github-self-hosted-runner.sh --token 'PASTE_TOKEN'
bash devops/scripts/setup-github-self-hosted-runner.sh --status
```

GitHub → Organization → Settings → Actions → Runners → **`vapp-prod`** = **Idle**

### ۳) Push workflowها (ترتیب: API اول)

```bash
# Api_Vapp_Manually → Admin_Vapp → Public_Vapp → scraping_Number_Vapp
```

---

## معماری hybrid

```
push main
  → CI (ubuntu-latest): test (+ build dist برای front)
  → Package (ubuntu-latest): docker image artifact  [فقط API + Scraper]
  → Deploy (self-hosted vapp-prod): docker load / rsync — محلی روی VPS
```

Secrets **`VAPP_SSH_*` برای Deploy job لازم نیست** (فقط deploy دستی از Mac).

---

## ★ یک دستور — trigger از Mac

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/gh-deploy-production.sh --prod --watch
```

جزئیات: [`CI_CD_QUICK.md`](CI_CD_QUICK.md)

---

## Environment `production` (هر چهار ریپo)

1. Settings → Environments → **New environment** → name: `production`
2. **Required reviewers** → خودت → Save

- API: https://github.com/seyedWebpro/Api_Vapp_Manually/settings/environments
- Admin: https://github.com/seyedWebpro/Admin_Pannel_Vapp/settings/environments
- Public: https://github.com/seyedWebpro/PublicWeb_Vapp/settings/environments
- Scraper: https://github.com/seyedWebpro/scraping_Number_Vapp/settings/environments

---

## Secrets (اختیاری — فقط Mac deploy)

| Name | When needed |
|------|-------------|
| `VAPP_SSH_*` | deploy-from-mac.sh — **نه** self-hosted CD |

---

## رفتار pipeline

1. push/PR → CI روی `ubuntu-latest`
2. push `main` → Package (API/Scraper) → Deploy روی `self-hosted`
3. `develop` → فقط CI

---

## رابطه با Mac

| سناریo | ابزار |
|--------|--------|
| release از GitHub | push → Approve → self-hosted deploy |
| hotfix فوری | `deploy-from-mac.sh api\|admin\|public` |
| Cursor + VPN | `vpn-bypass-vapp-server.sh` |

زمان deploy: [`DEPLOY-TIMING.md`](DEPLOY-TIMING.md)

---

## عیب‌یابی

| علامت | کار |
|-------|-----|
| Deploy job queued forever | runner Offline — `setup-github-self-hosted-runner.sh --status` |
| No runner with labels | Org runner نصب کن (نه فقط یک repo) |
| `Connection timed out` SSH | قدیمی — workflow جدید SSH نمی‌زند |
| API health ≠ 200 | `docker logs vapp_api_prod --tail 80` |
| Deploy منتظر Approve | Environment → Required reviewers |

---

## چرا microless بدون self-hosted کار می‌کند؟

microless: سرور `185.213.167.188:3031` از GitHub runner reachable است.  
Vapp: `195.24.237.132` فقط IP ایران — hence self-hosted runner.

---

تکرار روی پروژه دیگر: [`CI_CD_REPLICATE.md`](CI_CD_REPLICATE.md)
