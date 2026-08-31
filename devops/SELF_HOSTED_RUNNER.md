# Self-hosted runner — CD برای VPS ایران-only

سرور Vapp از IP خارج SSH نمی‌گیرد → GitHub hosted runner deploy fail.  
**راه‌حل:** runner روی خود VPS — deploy محلی، بدون SSH از خارج.

---

## معماری

```
push main
  → CI (GitHub hosted): build + test
  → Package (GitHub hosted): docker save / dist artifact
  → Deploy (self-hosted روی VPS): docker load + restart / rsync static
```

---

## نصب یک‌بار (روی VPS)

### ۱) Token از GitHub

**Org (پیشنهادی — هر ۴ ریپو):**  
https://github.com/organizations/seyedWebpro/settings/actions/runners/new  
→ Runner → Linux → copy token

**یا فقط API repo:**  
https://github.com/seyedWebpro/Api_Vapp_Manually/settings/actions/runners/new

### ۲) نصب runner

```bash
# روی VPS (root)
cd /root/Api_Vapp_Manually
git pull origin main   # یا scp اسکript

bash devops/scripts/setup-github-self-hosted-runner.sh --token 'PASTE_TOKEN_HERE'
```

### ۳) تأیید

GitHub → Settings → Actions → Runners → **`vapp-prod`** باید **Idle** باشد.

```bash
bash devops/scripts/setup-github-self-hosted-runner.sh --status
```

---

## استفاده

مثل قبل — push به `main` → CI سبز → Approve production → Deploy روی VPS.

```bash
# از Mac — trigger workflows
bash devops/scripts/gh-deploy-production.sh --prod --watch
```

Secrets `VAPP_SSH_*` برای **Deploy job دیگر لازم نیست** (فقط اگر جایی SSH fallback باشد).

---

## عیب‌یابی

| علامت | کار |
|-------|-----|
| Runner Offline | `systemctl restart 'actions.runner.*'` روی VPS |
| Job queued forever | runner Idle نیست / label `vapp-prod` اشتباه |
| docker: permission denied | runner as root نصب شده (RUNNER_ALLOW_RUNASROOT=1) |
| artifact download slow | طبیعی برای API image (~100MB) |

```bash
journalctl -u 'actions.runner.*' -f
docker ps
bash /root/Api_Vapp_Manually/devops/scripts/post-deploy-verify.sh
```

---

## حذف runner

```bash
bash devops/scripts/setup-github-self-hosted-runner.sh --uninstall --token 'NEW_REMOVE_TOKEN'
```

Token جدید از GitHub → Runners → runner → Remove.

---

## فایل‌های مرتبط

- `devops/scripts/setup-github-self-hosted-runner.sh`
- `devops/CI_CD.md`
- `.github/workflows/ci-cd.yml` (هر ریپo — deploy → `runs-on: [self-hosted, vapp-prod]`)
