# DNS و SSL — دامنه‌های Vapp

| دامنه | کاربرد | وضعیت |
|------|--------|--------|
| `vapplication.ir` | ادمین، فرم، گردونه، کارت، OTP، Swagger | ✅ DNS + HTTPS |
| `www.vapplication.ir` | همان اپ | ✅ DNS + HTTPS |
| `api.v-application.ir` | درگاه پرداخت / callback | ✅ DNS + HTTPS — **دست نزن** |
| `v-application.ir` (apex) | سایت معرفی / مارکتینگ | IP دیگر (`188.212.22.227`) — برای اپ لازم نیست |

## Cloudflare — وضعیت مورد انتظار

دامنهٔ **`vapplication.ir`**:

| Type | Name | Content | Proxy |
|------|------|---------|-------|
| A | `@` | `195.24.237.132` | **DNS only (Grey Cloud)** |
| A | `www` | `195.24.237.132` | **DNS only (Grey Cloud)** |

دامنهٔ **`v-application.ir`** (فقط ساب‌دامین درگاه):

| Type | Name | Content | Proxy |
|------|------|---------|-------|
| A | `api` | `195.24.237.132` | **DNS only** |

تست:

```bash
dig vapplication.ir +short          # → 195.24.237.132
dig www.vapplication.ir +short      # → 195.24.237.132
dig api.v-application.ir +short     # → 195.24.237.132
```

### چرا Grey Cloud؟

با Proxied (نارنجی) Cloudflare گاهی به سرورهای ایران **522** می‌دهد. ترافیک مستقیم + Certbot.

### دست نزن

- گواهی / nginx درگاه `api.v-application.ir`
- A record سایت مارکتینگ `v-application.ir` → `188.212.22.227`

## SSL

اپ و درگاه با Let's Encrypt (Certbot) صادر شده‌اند:

```bash
# تمدید خودکار
systemctl status certbot.timer

# در صورت نیاز فقط اپ (درگاه را دوباره صادر نکن):
sudo certbot --nginx -d vapplication.ir -d www.vapplication.ir \
  --non-interactive --agree-tos --register-unsafely-without-email --redirect
```

## URLهای نهایی

| سرویس | آدرس |
|--------|------|
| پنل ادمین | https://vapplication.ir/auth |
| فرم / گردونه / کارت | https://vapplication.ir/form\|wheel\|card/... |
| Swagger | https://vapplication.ir/swagger |
| OTP در SMS | `@vapplication.ir #کد` |
| Callback درگاه | https://api.v-application.ir/api/Payment/callback/... |
