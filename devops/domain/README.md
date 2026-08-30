# دامنه‌های production

| دامنه | کاربرد |
|------|--------|
| `api.v-application.ir` | درگاه پرداخت / callback زرین‌پال (**دست نزن**) |
| `vapplication.ir` | ادمین، فرم، گردونه، کارت ویزیت، نوبت‌دهی، OTP autofill، Swagger |

وضعیت فعلی: DNS + HTTPS برای هر دو فعال است. جزئیات Cloudflare: [CLOUDFLARE.md](CLOUDFLARE.md)

## معماری

```
کاربر → DNS
  api.v-application.ir  → Nginx (vapp-gateway) + SSL → API :8080  (فقط پرداخت)
  vapplication.ir       → Nginx (vapp) + SSL
                        → /api, /swagger, /health → API :8080
                        → /form, /wheel, /card, /book → Public_Vapp (static)
                        → / → Admin_Vapp (static)
```

| لایه | جزئیات |
|------|--------|
| DNS | `vapplication.ir` + `api.v-application.ir` → `195.24.237.132` |
| TLS | Certbot — `vapplication.ir` و `api.v-application.ir` جدا |
| API | `127.0.0.1:8080` — `vapp_api_prod` |
| DB | Docker `vapp_sqlserver_prod` → **`DbVapp` only** (see [`../DB.md`](../DB.md)) |
| لینک SMS | `https://vapplication.ir/form/{slug}` و `/wheel/{slug}` |

---

## یک‌خطی (روی سرور)

```bash
# فقط env + nginx اپ (درگاه دست نخورده می‌ماند)
DOMAIN_HOST=vapplication.ir GATEWAY_HOST=api.v-application.ir \
  bash devops/scripts/switch-to-domain.sh

# + Certbot اپ (اگر گواهی نبود)
DOMAIN_HOST=vapplication.ir GATEWAY_HOST=api.v-application.ir \
  bash devops/scripts/switch-to-domain.sh --certbot
```

از Mac:

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
SERVER=vapp-prod bash devops/scripts/sync-to-server.sh
ssh vapp-prod 'cd ~/Api_Vapp_Manually && bash devops/scripts/switch-to-domain.sh --certbot'
```

---

## `.env` API (نمونه)

از [env.domain.example](env.domain.example):

```env
PUBLIC_API_BASE_URL=https://api.v-application.ir
PUBLIC_FRONTEND_URL=https://vapplication.ir
FORM_PUBLIC_BASE_URL=https://vapplication.ir/form
WHEEL_PUBLIC_BASE_URL=https://vapplication.ir/wheel
CARD_PUBLIC_BASE_URL=https://vapplication.ir/card
BOOKING_PUBLIC_BASE_URL=https://vapplication.ir/book
ZarinPal__CallbackUrl=https://api.v-application.ir/api/Payment/callback/zarinpal
Sms__OtpAutofillDomain=vapplication.ir
```

اپ موبایل (`FrontMobile_Vapp/.env`): `BASE_URL_RELEASE=https://vapplication.ir`

---

## تست

```bash
bash devops/scripts/health-check.sh --with-domain
curl -sS -o /dev/null -w '%{http_code}\n' https://vapplication.ir/
curl -sS -o /dev/null -w '%{http_code}\n' https://vapplication.ir/form/test
curl -sS -o /dev/null -w '%{http_code}\n' https://api.v-application.ir/health
docker exec vapp_api_prod printenv | grep -E 'PublicBaseUrl|CallbackUrl|OtpAutofill'
```

---

## فایل‌های این پوشه

| فایل | کاربرد |
|------|--------|
| [env.domain.example](env.domain.example) | نمونه `docker/.env` |
| [CLOUDFLARE.md](CLOUDFLARE.md) | DNS و SSL |
| [ROLLBACK.md](ROLLBACK.md) | برگشت به IP |
| [CURRENT_IP_MODE.snapshot.txt](CURRENT_IP_MODE.snapshot.txt) | مرجع حالت قبلی |
| `../scripts/switch-to-domain.sh` | سوئیچ دامنه |
| `../scripts/apply-nginx.sh` | Nginx اپ + حفظ درگاه |

## نکات

- Nginx اپ: `/etc/nginx/sites-available/vapp` — درگاه: `vapp-gateway` (جدا؛ با apply-nginx بازنویسی نمی‌شود).
- **`apply-nginx.sh` خودکار `DOMAIN_HOST` را از `server.conf` + گواهی Let's Encrypt می‌گیرد** — deploy فرانت/API دیگر HTTPS را نمی‌شکند.
- IP موقتاً در `server_name` اپ هست؛ لینک‌های SMS قدیمی با IP هنوز باز می‌شوند.
- برگشت: [ROLLBACK.md](ROLLBACK.md) یا `switch-to-domain.sh --ip-only` (درگاه را جدا نگه دارید).
