# دامنه production — ok-sms.ir

راهنمای کامل انتقال از IP به دامنه؛ الگو از `vamyabSIte/api_vamyab_shop/devops/domain`.

## معماری

```
کاربر → DNS (ok-sms.ir) → 185.116.162.233
                        → Nginx :80 / :443 (Certbot)
                        → /api, /swagger, /health → API :8080
                        → /form, /wheel → Public_Vapp (static)
                        → / → Admin_Vapp (docker :3005 یا static)
```

| لایه | جزئیات |
|------|--------|
| DNS | A record `@` و `www` → `185.116.162.233` — [CLOUDFLARE.md](CLOUDFLARE.md) |
| TLS | Certbot روی سرور (پس از DNS) |
| API | `127.0.0.1:8080` — `vapp_api_prod` |
| لینک SMS | `https://ok-sms.ir/form/{slug}` و `/wheel/{slug}` |

---

## یک‌خطی — سوئیچ به دامنه (روی سرور)

```bash
ssh vapp-prod 'cd ~/Api_Vapp_Manually && bash devops/scripts/switch-to-domain.sh --http-only'
```

بعد از DNS + آماده بودن SSL:

```bash
ssh vapp-prod 'cd ~/Api_Vapp_Manually && bash devops/scripts/switch-to-domain.sh --certbot'
```

از Mac (sync + سوئیچ):

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually && SERVER=vapp-prod bash devops/scripts/sync-to-server.sh && ssh vapp-prod 'cd ~/Api_Vapp_Manually && bash devops/scripts/switch-to-domain.sh --http-only'
```

---

## مراحل دستی (یک‌بار)

### ۱) DNS

→ [CLOUDFLARE.md](CLOUDFLARE.md) — `dig ok-sms.ir +short` باید `185.116.162.233` باشد.

### ۲) `.env` API

از [env.domain.example](env.domain.example):

```env
PUBLIC_API_BASE_URL=https://ok-sms.ir
PUBLIC_FRONTEND_URL=https://ok-sms.ir
FORM_PUBLIC_BASE_URL=https://ok-sms.ir/form
WHEEL_PUBLIC_BASE_URL=https://ok-sms.ir/wheel
```

### ۳) Nginx + API

```bash
cd ~/Api_Vapp_Manually
bash devops/scripts/switch-to-domain.sh --http-only
# بعد از DNS:
bash devops/scripts/switch-to-domain.sh --certbot
```

### ۴) Redeploy فرانت‌ها (اختیاری — اگر URL در bundle سخت‌کد شده)

```bash
bash devops/scripts/deploy-public-front-host.sh
# Admin اگر static است:
bash devops/scripts/deploy-front-host.sh
```

اپ موبایل (`Front_Vapp/.env`): `BASE_URL_RELEASE=https://ok-sms.ir`

### ۵) تست

```bash
bash devops/scripts/health-check.sh --with-domain
curl -sS -o /dev/null -w '%{http_code}\n' -H 'Host: ok-sms.ir' http://127.0.0.1/form/
docker exec vapp_api_prod printenv | grep PublicBaseUrl
```

از Mac (بعد از DNS):

```bash
curl -sS -o /dev/null -w '%{http_code}\n' https://ok-sms.ir/
curl -sS -o /dev/null -w '%{http_code}\n' https://ok-sms.ir/form/test
```

---

## فایل‌های این پوشه

| فایل | کاربرد |
|------|--------|
| [env.domain.example](env.domain.example) | نمونه `docker/.env` |
| [CLOUDFLARE.md](CLOUDFLARE.md) | DNS و SSL |
| [ROLLBACK.md](ROLLBACK.md) | برگشت به IP |
| [CURRENT_IP_MODE.snapshot.txt](CURRENT_IP_MODE.snapshot.txt) | مرجع حالت قبلی |
| `../scripts/switch-to-domain.sh` | اسکریپت اصلی سوئیچ |
| `../scripts/apply-nginx.sh` | Nginx با `DOMAIN_HOST` |

---

## نکات

- **IP همچنان کار می‌کند:** در حالت دامنه، `server_name` شامل `185.116.162.233` هم هست (دسترسی موقت).
- **لینک‌های SMS قدیمی** با IP همچنان باز می‌شوند تا زمانی که IP را از nginx حذف کنید.
- **برگشت:** [ROLLBACK.md](ROLLBACK.md) یا `switch-to-domain.sh --ip-only`
- **Forwarded headers:** API از پشت Nginx+HTTPS برای callback پرداخت/SMS درست عمل می‌کند.
