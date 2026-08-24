# دامنه‌های production

دو دامنه:

| دامنه | کاربرد |
|------|--------|
| `api.v-application.ir` | درگاه پرداخت / callback زرین‌پال (**دست نزن**) |
| `vapplication.ir` | ادمین، فرم، گردونه، کارت ویزیت، نوبت‌دهی، OTP autofill، Swagger |

## معماری

```
کاربر → DNS
  api.v-application.ir  → درگاه / callback پرداخت (SSL موجود) → API :8080
  vapplication.ir       → Nginx :80 / :443
                        → /api, /swagger, /health → API :8080
                        → /form, /wheel, /card, /book → Public_Vapp (static)
                        → / → Admin_Vapp (static)
```

| لایه | جزئیات |
|------|--------|
| DNS | A برای هر دو دامنه → سرور — [CLOUDFLARE.md](CLOUDFLARE.md) |
| TLS | Certbot روی سرور (پس از DNS) |
| API | `127.0.0.1:8080` — `vapp_api_prod` |
| لینک SMS | `https://vapplication.ir/form/{slug}` و `/wheel/{slug}` |

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

→ [CLOUDFLARE.md](CLOUDFLARE.md) — `dig v-application.ir +short` باید `195.24.237.132` باشد.

### ۲) `.env` API

از [env.domain.example](env.domain.example):

```env
PUBLIC_API_BASE_URL=https://api.v-application.ir
PUBLIC_FRONTEND_URL=https://vapplication.ir
FORM_PUBLIC_BASE_URL=https://vapplication.ir/form
WHEEL_PUBLIC_BASE_URL=https://vapplication.ir/wheel
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

اپ موبایل (`FrontMobile_Vapp/.env`): `BASE_URL_RELEASE=https://vapplication.ir`

### ۵) تست

```bash
bash devops/scripts/health-check.sh --with-domain
curl -sS -o /dev/null -w '%{http_code}\n' -H 'Host: v-application.ir' http://127.0.0.1/form/
docker exec vapp_api_prod printenv | grep PublicBaseUrl
```

از Mac (بعد از DNS):

```bash
curl -sS -o /dev/null -w '%{http_code}\n' https://v-application.ir/
curl -sS -o /dev/null -w '%{http_code}\n' https://vapplication.ir/form/test
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

- **IP همچنان کار می‌کند:** در حالت دامنه، `server_name` شامل `195.24.237.132` هم هست (دسترسی موقت).
- **لینک‌های SMS قدیمی** با IP همچنان باز می‌شوند تا زمانی که IP را از nginx حذف کنید.
- **برگشت:** [ROLLBACK.md](ROLLBACK.md) یا `switch-to-domain.sh --ip-only`
- **Forwarded headers:** API از پشت Nginx+HTTPS برای callback پرداخت/SMS درست عمل می‌کند.
