# DevOps — Vapp

Overview, structure and links.  
**For the actual command list see [`COMMANDS.txt`](COMMANDS.txt).**

**Production domains:** app `https://vapplication.ir` · payment gateway `https://api.v-application.ir`  
**Production DB:** SQL Server Docker only — database name **`DbVapp`** (`vapp_sqlserver_prod`). No legacy remote SQL / `aDb_Vapp`.  
→ [`domain/README.md`](domain/README.md) · [`domain/CLOUDFLARE.md`](domain/CLOUDFLARE.md) · [`DB.md`](DB.md)

---

## Quick start

```bash
# ★ روش اصلی — GitHub + self-hosted runner (Aug 2026)
git push origin main   # همان ریپویی که عوض شده
bash devops/scripts/gh-deploy-production.sh --prod --watch

# Mac — hotfix فقط
bash devops/scripts/deploy-from-mac.sh api|admin|public
bash devops/scripts/deploy-from-mac.sh health
```

All commands: [`COMMANDS.txt`](COMMANDS.txt).  
**چه کار شد:** [`WHAT-WAS-DONE.md`](WHAT-WAS-DONE.md) · **زمان:** [`DEPLOY-TIMING.md`](DEPLOY-TIMING.md) · **فرایند:** [`DEPLOY-FLOW.md`](DEPLOY-FLOW.md)  
CI/CD: [`CI_CD_QUICK.md`](CI_CD_QUICK.md) · [`CI_CD.md`](CI_CD.md)

---

## What's where

```
devops/
  COMMANDS.txt                    ← all deploy/ops commands (cheat sheet)
  WHAT-WAS-DONE.md                ← ★ چه کار شد (Aug 2026 self-hosted)
  DEPLOY-FLOW.md                  ← قدم‌به‌قدم: push → production
  DEPLOY-TIMING.md                ← زمان Mac vs GitHub (real numbers)
  MAC-QUICK-DEPLOY.md             ← Mac deploy guide by change type
  SUPPORT-TROUBLESHOOTING.md      ← START HERE for support: where to look for errors
  SERVER-LOGS.md                  ← where/how to read server file logs
  ADMIN-AUDIT.md                  ← AdminAuditLogs table usage (short)
  AUDIT_RUNBOOK.md                ← audit search scenarios (detailed)
  PUBLIC-VAPP.md                  ← public form/wheel details
  DB.md                           ← production DB = Docker DbVapp only
  NUMBER-SCRAPER.md               ← number-scraper robot
  MAC-SERVER.md                   ← SSH / first-time setup
  GITHUB_SSH.md                   ← deploy key setup
  CI_CD.md                        ← GitHub Actions CI/CD (secrets, setup)
  SELF_HOSTED_RUNNER.md           ← ★ نصب runner روی VPS (CD از GitHub)
  CI_CD_QUICK.md                  ← one-page CI/CD cheat sheet
  CI_CD_REPLICATE.md              ← replicate CI/CD to another project
  scripts/                        ← deploy, bootstrap, backup, health-check scripts
  deploy/                         ← nginx example config
  backup/                         ← DB backup scripts
  domain/                         ← domain / Cloudflare guide
```

---

## Stack mapping (copy template)

| Layer | Vapp | Replace for a new project |
|-------|------|---------------------------|
| API | .NET 8 + Docker | repo path, `docker-compose`, Dockerfile |
| Admin | Vite/React + static nginx | repo path, port, `VITE_*` vars |
| Public | Vite/React — `/form` `/wheel` | `Public_Vapp`, static or docker :3006 |
| DB | SQL Server in Docker | DB type, container name, connection string |
| Proxy | Nginx | locations, upstream port, domain/IP |

---

## First-time setup

1. `scripts/setup-github-deploy-key.sh` — server key → GitHub
2. `scripts/bootstrap-first-run.sh` — install Vapp once
3. `scripts/bootstrap-scraper-on-server.sh` — install scraper robot (in its own repo)
4. For daily updates: **git push** → GitHub CD (self-hosted) · hotfix: `deploy-from-mac.sh`

---

## Repositories

```
vapp/
  Api_Vapp_Manually/    # .NET API + DevOps scripts
  Admin_Vapp/           # React admin panel
  Public_Vapp/          # Public form / lucky wheel
  scraping_Number_Vapp/ # Number scraper robot
```

Mobile → Vapp .NET API → Number Scraper (:8000 internally)

---

## See also

- [`COMMANDS.txt`](COMMANDS.txt) — **all commands in one place**
- [`DEPLOY-TIMING.md`](DEPLOY-TIMING.md) — **deploy duration breakdown**
- [`MAC-QUICK-DEPLOY.md`](MAC-QUICK-DEPLOY.md) — which mode to pick
- [`SUPPORT-TROUBLESHOOTING.md`](SUPPORT-TROUBLESHOOTING.md) — **support: where to look when something breaks**
- [`SERVER-LOGS.md`](SERVER-LOGS.md) — server logs location & commands
- [`ADMIN-AUDIT.md`](ADMIN-AUDIT.md) — AdminAuditLogs usage (short)
- [`AUDIT_RUNBOOK.md`](AUDIT_RUNBOOK.md) — audit search scenarios
- [`PUBLIC-VAPP.md`](PUBLIC-VAPP.md) — public form/wheel deploy
- [`NUMBER-SCRAPER.md`](NUMBER-SCRAPER.md) — scraper deploy, env, test
- [`MAC-SERVER.md`](MAC-SERVER.md) — SSH port 22 and first setup
