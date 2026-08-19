# Mac → سرور Vapp (SSH + Deploy)

منبع حقیقت: [`server.conf`](server.conf)

| مورد | مقدار |
|------|--------|
| IP | `195.24.237.132` |
| SSH port | **`22`** (مثل CaspianEdu روی همین دیتاسنتر) |
| SSH alias | `vapp-prod` |
| API repo (سرور) | `/root/Api_Vapp_Manually` |
| Admin repo (سرور) | `/root/Admin_Vapp` |
| Public repo (سرور) | `/root/Public_Vapp` |

> **فیلترشکن:** با VPN به این دیتاسنتر وصل نمی‌شوید. SSH را **بدون فیلترشکن** بزنید.

---

## ۱) نصب یک‌بار — SSH config روی Mac

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/setup-local-ssh-to-server.sh --force
```

`~/.ssh/config`:

```
Host vapp-prod
  HostName 195.24.237.132
  Port 22
  User root
  IdentityFile ~/.ssh/id_ed25519_vapp_server
  IdentitiesOnly yes
```

کلید public روی سرور (کنسول وب اگر SSH timeout):

```bash
mkdir -p ~/.ssh && chmod 700 ~/.ssh && echo 'PASTE_PUBLIC_KEY' >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys && echo KEY_ADDED
```

---

## ۲) تست اتصال (بدون فیلترشکن)

```bash
ssh vapp-prod 'echo SSH_OK'
nc -zv -w 5 195.24.237.132 22
curl -sS -m10 -o /dev/null -w 'health:%{http_code}\n' http://195.24.237.132/health
```

---

## ۳) Deploy از Mac

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/deploy-from-mac.sh api
bash devops/scripts/deploy-from-mac.sh admin
bash devops/scripts/deploy-from-mac.sh public
bash devops/scripts/deploy-from-mac.sh health
```

---

## ۴) آپدیت روی خود سرور (پیشنهادی — مثل CaspianEdu)

این دیتاسنتر به Docker Hub / npm / MCR وصل است — **میرور ایران‌سرور استفاده نشود**.

```bash
ssh vapp-prod
bash ~/Api_Vapp_Manually/vapp-iran-update.sh --test
bash ~/Api_Vapp_Manually/vapp-iran-update.sh --full
```

---

## ۵) عیب‌یابی

| علامت | علت | راه‌حل |
|--------|-----|--------|
| `Operation timed out` | فیلترشکن روشن است | VPN را خاموش کنید |
| `Permission denied (publickey)` | کلید Mac روی سرور نیست | دستور یک‌خطی `authorized_keys` از کنسول وب |
| `API:000` بلافاصله بعد deploy | API در حال startup/migration | ۶۰ ثانیه صبر → `health-check.sh` |

---

## ۶) لینک‌ها

| سرویس | URL |
|--------|-----|
| Admin | http://195.24.237.132/admin |
| Swagger | http://195.24.237.132/swagger |
| Health | http://195.24.237.132/health |
| Form | http://195.24.237.132/form/{slug} |
