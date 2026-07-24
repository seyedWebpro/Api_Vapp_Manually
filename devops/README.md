# DevOps — Vapp

Overview, structure and links.  
**For the actual command list see [`COMMANDS.txt`](COMMANDS.txt).**

---

## Quick start

```bash
# Mac — from Api_Vapp_Manually
bash devops/scripts/deploy-from-mac.sh api     # .NET API
bash devops/scripts/deploy-from-mac.sh admin   # Admin panel
bash devops/scripts/deploy-from-mac.sh public  # Public form/wheel
bash devops/scripts/deploy-from-mac.sh health  # Check services
```

All commands, options and server-side commands are in [`COMMANDS.txt`](COMMANDS.txt).

---

## What's where

```
devops/
  COMMANDS.txt          ← all deploy/ops commands (cheat sheet)
  MAC-QUICK-DEPLOY.md   ← Mac deploy guide by change type
  PUBLIC-VAPP.md        ← public form/wheel details
  NUMBER-SCRAPER.md     ← number-scraper robot
  MAC-SERVER.md         ← SSH / first-time setup
  GITHUB_SSH.md         ← deploy key setup
  scripts/              ← deploy, bootstrap, backup, health-check scripts
  deploy/               ← nginx example config
  backup/               ← DB backup scripts
  domain/               ← domain / Cloudflare guide
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
4. For daily updates: `deploy-from-mac.sh` or `deploy-server.sh --fast --wait`

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
- [`MAC-QUICK-DEPLOY.md`](MAC-QUICK-DEPLOY.md) — which mode to pick
- [`PUBLIC-VAPP.md`](PUBLIC-VAPP.md) — public form/wheel deploy
- [`NUMBER-SCRAPER.md`](NUMBER-SCRAPER.md) — scraper deploy, env, test
- [`MAC-SERVER.md`](MAC-SERVER.md) — SSH port 3031 and first setup
