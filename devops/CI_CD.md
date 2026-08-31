# CI/CD — GitHub Actions + Self-hosted Runner

**وضعیت (Aug 2026):** ✅ CI روی GitHub · ✅ CD روی VPS (`vapp-prod`) · Mac فقط hotfix

| ریپo | CI | CD |
|------|-----|-----|
| [Api_Vapp_Manually](https://github.com/seyedWebpro/Api_Vapp_Manually) | dotnet test | docker load + restart |
| [Admin_Pannel_Vapp](https://github.com/seyedWebpro/Admin_Pannel_Vapp) | vite build | rsync → `/var/www/vapp-admin` |
| [PublicWeb_Vapp](https://github.com/seyedWebpro/PublicWeb_Vapp) | vite build | rsync → `/var/www/vapp-public` |
| [scraping_Number_Vapp](https://github.com/seyedWebpro/scraping_Number_Vapp) | python tests | docker load + restart |

---

## روزمره — یک خط

```bash
git push origin main   # همان ریپویی که عوض شده
```

یا:

```bash
bash devops/scripts/gh-deploy-production.sh --admin --push --watch
```

زمان: [`DEPLOY-TIMING.md`](DEPLOY-TIMING.md) · فرایند: [`DEPLOY-FLOW.md`](DEPLOY-FLOW.md) · تاریخچه: [`WHAT-WAS-DONE.md`](WHAT-WAS-DONE.md)

---

## معماری

```
push main
  → CI (GitHub ubuntu-latest)
  → Package [API/Scraper only] (GitHub)
  → Deploy (VPS self-hosted: vapp-prod)
```

**چرا self-hosted?** VPS فقط IP ایران — GitHub runner خارجی SSH timeout می‌گرفت.

---

## Runners روی VPS (نصب شده)

| Runner | ریپo | سرویس systemd |
|--------|------|----------------|
| vapp-prod-api | Api_Vapp_Manually | `actions.runner.seyedWebpro-Api_Vapp_Manually.vapp-prod-api` |
| vapp-prod-admin | Admin_Pannel_Vapp | `actions.runner.seyedWebpro-Admin_Pannel_Vapp.vapp-prod-admin` |
| vapp-prod-public | PublicWeb_Vapp | `actions.runner.seyedWebpro-PublicWeb_Vapp.vapp-prod-public` |
| vapp-prod-scraper | scraping_Number_Vapp | `actions.runner.seyedWebpro-scraping_Number_Vapp.vapp-prod-scraper` |

```bash
# وضعیت
ssh vapp-prod 'systemctl status actions.runner.*'
bash devops/scripts/setup-github-self-hosted-runner.sh --status

# نصب مجدد (از Mac)
bash devops/scripts/install-all-vapp-github-runners.sh
```

راهنما: [`SELF_HOSTED_RUNNER.md`](SELF_HOSTED_RUNNER.md)

---

## Environment `production`

هر چهار ریپo → Settings → Environments → `production` (+ Required reviewers اختیاری)

---

## Secrets

| Secret | لازم برای CD? |
|--------|----------------|
| `VAPP_SSH_*` | ❌ (فقط Mac deploy) |

---

## Mac (hotfix)

```bash
bash devops/scripts/deploy-from-mac.sh api|admin|public
bash devops/scripts/vpn-bypass-vapp-server.sh   # اگر VPN روشن است
```

---

## عیب‌یابی

| علامت | کار |
|-------|-----|
| Deploy queued | runner Offline — `systemctl restart 'actions.runner.*'` |
| SSH timeout در log قدیمی | workflow جدید — ignore |
| API health ≠ 200 | `docker logs vapp_api_prod --tail 80` |
| Approve منتظر | GitHub → Review deployments |

---

## microless vs Vapp

| | microless | Vapp |
|---|-----------|------|
| CD | SSH `:3031` از GitHub | self-hosted روی VPS |
| Front | Docker ~۵–۸ min | static ~**۲ min** |
| API | ~۶–۱۰ min | ~**۱۴ min** |

---

[`CI_CD_REPLICATE.md`](CI_CD_REPLICATE.md) · [`CI_CD_QUICK.md`](CI_CD_QUICK.md)
