# DNS و SSL — دامنه‌های Vapp

دو دامنهٔ جدا:

| دامنه | کاربرد | وضعیت سرور |
|------|--------|------------|
| `api.v-application.ir` | درگاه پرداخت / callback زرین‌پال | ✅ فعال + SSL — **دست نزن** |
| `vapplication.ir` | ادمین، فرم، گردونه، کارت، OTP، Swagger | نیاز به DNS + Certbot |
| `v-application.ir` (بدون api) | سایت معرفی / مارکتینگ | روی IP دیگر است — برای Vapp لازم نیست |

## کاری که باید در Cloudflare بزنی (الزامی برای اپ)

پنل Cloudflare دامنهٔ **`vapplication.ir`**:

| Type | Name | Content | Proxy |
|------|------|---------|-------|
| A | `@` | `195.24.237.132` | **DNS only (Grey Cloud)** |
| A | `www` | `195.24.237.132` | **DNS only (Grey Cloud)** |

تست بعد از ذخیره (چند دقیقه صبر):

```bash
dig vapplication.ir +short
# باید دقیقاً: 195.24.237.132
```

الان از سرور `vapplication.ir` **هیچ A record ندارد** → تا این را نزنی، HTTPS عمومی و Certbot کار نمی‌کند.

### دست نزن

| رکورد | چرا |
|------|-----|
| `api.v-application.ir` → `195.24.237.132` | درگاه پرداخت؛ الان درست است |
| `v-application.ir` → `188.212.22.227` | سایت جدا؛ برای اپ لازم نیست |

## چرا Grey Cloud؟

با Proxied (نارنجی) Cloudflare گاهی به سرورهای ایران **522** می‌دهد. ترافیک مستقیم به IP سرور + SSL با Certbot.

## SSL اپ — بعد از درست شدن DNS

روی سرور (بعد از اینکه dig همان IP را نشان داد):

```bash
ssh vapp-prod 'cd ~/Api_Vapp_Manually && bash devops/scripts/switch-to-domain.sh --certbot'
# یا فقط:
sudo certbot --nginx -d vapplication.ir -d www.vapplication.ir \
  --non-interactive --agree-tos --register-unsafely-without-email --redirect
```

درگاه (`api.v-application.ir`) از قبل SSL دارد — دوباره صادر نکن مگر لازم باشد.

## فایروال

```bash
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
```

## URLهای نهایی

| سرویس | آدرس |
|--------|------|
| پنل ادمین | `https://vapplication.ir/auth` |
| فرم / گردونه / کارت | `https://vapplication.ir/form|wheel|card/...` |
| Swagger | `https://vapplication.ir/swagger` |
| OTP در SMS | `@vapplication.ir #کد` |
| Callback درگاه | `https://api.v-application.ir/api/Payment/callback/...` |
