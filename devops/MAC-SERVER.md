# Mac → سرور Vapp (SSH + Deploy)

راهنمای اتصال از Mac به VPS و deploy API بدون build کند روی سرور ایران.

| مورد | مقدار |
|------|--------|
| IP | `185.116.162.233` |
| SSH port | **`3031`** (نه 22) |
| SSH alias | `vapp-prod` |
| API repo (سرور) | `/root/Api_Vapp_Manually` |
| Admin repo (سرور) | `/root/Admin_Vapp` |

> **مهم:** روی این سرور `sshd` فقط روی پورت **3031** listen می‌کند.  
> `ssh root@185.116.162.233` بدون `-p` → `Connection refused`

---

## ۱) نصب یک‌بار — SSH config روی Mac

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
SERVER=root@185.116.162.233 SSH_PORT=3031 bash devops/scripts/setup-local-ssh-to-server.sh
```

یا دستی:

```bash
mkdir -p ~/.ssh && cat >> ~/.ssh/config <<'EOF'

Host vapp-prod
  HostName 185.116.162.233
  Port 3031
  User root
  IdentityFile ~/.ssh/id_ed25519_vapp_server
  IdentitiesOnly yes
EOF
chmod 600 ~/.ssh/config
```

کلید public روی سرور (اگر ندارید):

```bash
ssh-copy-id -p 3031 -i ~/.ssh/id_ed25519_vapp_server.pub root@185.116.162.233
```

---

## ۲) تست اتصال

```bash
# با alias (پیشنهادی)
ssh vapp-prod 'echo SSH_OK'

# مستقیم با پورت
ssh -p 3031 root@185.116.162.233 'echo SSH_OK'

# پورت باز است؟
nc -zv -w 5 185.116.162.233 3031

# سایت (بدون SSH)
curl -sS -m10 -o /dev/null -w 'health:%{http_code}\n' http://185.116.162.233/health
```

---

## ۳) Deploy API از Mac (پیشنهادی — سرور ایران)

build روی Mac (سریع) → upload image → restart container روی سرور.

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
git pull origin main
SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh
```

فقط upload (بدون restart):

```bash
SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh --no-deploy
```

**زمان تقریبی:** ۱۰–۲۰ دقیقه (build cache + upload ~۱۰۰MB)

بعد از deploy، health ممکن است اول `API:000` باشد — ۳۰–۶۰ ثانیه صبر (migration).

---

## ۴) Deploy Admin / Full — روی خود سرور

فرانت معمولاً روی سرور deploy می‌شود (Docker + npm iranserver):

```bash
ssh vapp-prod
cd ~/Api_Vapp_Manually && git pull origin main
cd ~/Admin_Vapp && git pull origin main
bash ~/Api_Vapp_Manually/vapp-iran-update.sh --fast
```

فقط Admin:

```bash
bash ~/Api_Vapp_Manually/vapp-iran-update.sh
```

---

## ۵) جریان کار روزانه

| مرحله | کجا | دستور |
|--------|-----|--------|
| 1. توسعه | Mac | کد + commit |
| 2. Push | Mac | `git push origin main` |
| 3. Deploy | Mac | `bash devops/scripts/deploy-from-mac.sh api` یا `admin` — جدول کامل: **`MAC-QUICK-DEPLOY.md`** |
| 4. تأیید | Mac | `bash devops/scripts/deploy-from-mac.sh health` |

Deploy قدیمی (همان اسکریپت‌های زیرین):
- API: `SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh`
- Admin روی سرور: `bash vapp-iran-update.sh --front-only`

---

## ۶) rsync سورس (بدون git)

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
SERVER=vapp-prod bash devops/scripts/sync-to-server.sh
```

---

## ۷) عیب‌یابی

| علامت | علت | راه‌حل |
|--------|-----|--------|
| `port 22: Connection refused` | SSH روی 3031 است | `ssh -p 3031` یا `ssh vapp-prod` |
| `Permission denied (publickey)` | کلید Mac روی سرور نیست | `ssh-copy-id -p 3031 ...` |
| `API:000` بلافاصله بعد deploy | API در حال startup/migration | ۶۰ ثانیه صبر → `health-check.sh` |
| build API روی سرور ۳۰+ دقیقه | `dotnet restore` داخل Docker | از Mac: `deploy-api-upload-image.sh` |
| `mcr.microsoft.com` timeout | block از ایران | همان — upload از Mac |

**تشخیص سریع روی سرور:**

```bash
grep ^Port /etc/ssh/sshd_config
ss -tlnp | grep sshd
bash ~/Api_Vapp_Manually/devops/scripts/health-check.sh
docker ps --filter name=vapp_api_prod --format '{{.Status}}'
```

---

## ۸) لینک‌ها

| سرویس | URL |
|--------|-----|
| Admin | http://185.116.162.233/admin |
| Swagger | http://185.116.162.233/swagger |
| Health | http://185.116.162.233/health |

---

## فایل‌های مرتبط

- `GITHUB_SSH.md` — Mac↔سرور + سرور↔GitHub
- `server-update-commands.txt` — cheat sheet یک‌خطی
- `scripts/setup-local-ssh-to-server.sh` — ساخت کلید + `~/.ssh/config`
- `scripts/deploy-api-upload-image.sh` — build Mac → upload سرور
