# SSH — Mac↔سرور و سرور↔GitHub

> **Mac → سرور (پورت 3031، deploy از Mac):** [`MAC-SERVER.md`](MAC-SERVER.md)

دو نوع کلید لازم است:

| جهت | هدف | کجا ساخته می‌شود | کجا اضافه می‌شود |
|-----|------|------------------|------------------|
| **Mac → Server** | SSH بدون پسورد به VPS | Mac | `authorized_keys` روی سرور |
| **Server → GitHub** | `git pull` روی سرور | سرور | GitHub → SSH keys |

---

## ۱) Mac → سرور (ورود SSH)

**پورت SSH این سرور: `3031`** (نه 22). Alias: `vapp-prod`

روی **Mac**:

```bash
cd ~/path/to/Api_Vapp_Manually
SSH_PORT=3031 bash devops/scripts/setup-local-ssh-to-server.sh
```

کلید public را روی سرور بگذارید:

```bash
ssh-copy-id -p 3031 -i ~/.ssh/id_ed25519_vapp_server.pub root@185.116.162.233
```

تست:

```bash
ssh vapp-prod 'echo OK'
# یا
ssh -p 3031 -i ~/.ssh/id_ed25519_vapp_server root@185.116.162.233 'echo OK'
```

Deploy API از Mac (پیشنهادی برای سرور ایران):

```bash
SERVER=vapp-prod bash devops/scripts/deploy-api-upload-image.sh
```

---

## ۲) سرور → GitHub (git pull)

روی **سرور** (بعد از SSH):

```bash
bash ~/Api_Vapp_Manually/devops/scripts/setup-github-deploy-key.sh
```

خروجی `id_ed25519_vapp_github.pub` را کپی کنید.

### اضافه کردن به GitHub

1. برو به [github.com/settings/keys](https://github.com/settings/keys)
2. **New SSH key**
3. Title: `vapp-server-185.116.162.233`
4. Key type: **Authentication Key**
5. Paste کلید public → **Add SSH key**

> **نکته:** با SSH key روی اکانت GitHub، هر دو repo (`Api_Vapp_Manually` و `Admin_Pannel_Vapp`) قابل pull هستند.  
> اگر فقط یک repo می‌خواهید، در همان repo بروید: Settings → Deploy keys → Add deploy key (فقط همان repo).

### تست اتصال GitHub

```bash
ssh -T git@github.com
# باید ببینید: Hi seyedWebpro! You've successfully authenticated...
```

---

## ۳) Clone اولیه با SSH (روی سرور)

```bash
git clone git@github.com:seyedWebpro/Api_Vapp_Manually.git ~/Api_Vapp_Manually
git clone git@github.com:seyedWebpro/Admin_Pannel_Vapp.git ~/Admin_Vapp
```

اگر قبلاً با HTTPS clone کرده‌اید:

```bash
bash ~/Api_Vapp_Manually/devops/scripts/switch-git-remotes-to-ssh.sh
```

---

## ۴) جریان کار روزانه

**روی Mac:** push به GitHub

```bash
cd Api_Vapp_Manually && git push origin main
cd Admin_Vapp && git push origin main
```

**روی سرور:** pull + deploy

```bash
ssh vapp-prod
cd ~/Api_Vapp_Manually && git pull origin main && bash devops/scripts/deploy-server.sh --fast --wait
```

(`deploy-server.sh --pull-only` هم API و هم Admin را pull می‌کند.)

---

## ۵) عیب‌یابی

| خطا | راه‌حل |
|-----|--------|
| `port 22: Connection refused` | SSH روی **3031** است — `ssh vapp-prod` یا `-p 3031` |
| `Permission denied (publickey)` به سرور | کلید Mac را در `authorized_keys` سرور بگذارید؛ `ssh-copy-id -p 3031 ...` |
| `Permission denied (publickey)` به GitHub | کلید سرور را در GitHub SSH keys اضافه کنید؛ `ssh -T git@github.com` |
| `Host key verification failed` | `ssh-keyscan github.com >> ~/.ssh/known_hosts` روی سرور |
| HTTPS asks password on pull | `switch-git-remotes-to-ssh.sh` را اجرا کنید |

---

## Repoها

| پروژه | GitHub SSH URL |
|-------|----------------|
| API | `git@github.com:seyedWebpro/Api_Vapp_Manually.git` |
| Admin | `git@github.com:seyedWebpro/Admin_Pannel_Vapp.git` |
