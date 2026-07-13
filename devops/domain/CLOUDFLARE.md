# DNS و SSL — ok-sms.ir (Vapp)

## وضعیت مورد انتظار

| مورد | مقدار |
|------|--------|
| دامنه | `ok-sms.ir` + `www.ok-sms.ir` |
| IP سرور Vapp | `185.116.162.233` |
| SSH | پورت `3031` (نه 22) |
| Proxy Cloudflare | **DNS only (Grey Cloud)** توصیه می‌شود |
| HTTPS | Certbot روی سرور (Let's Encrypt) |

## ۱) DNS — الزامی قبل از Certbot

در پنل DNS (Cloudflare یا رجیسترار):

| Type | Name | Content | Proxy |
|------|------|---------|-------|
| A | `@` | `185.116.162.233` | DNS only |
| A | `www` | `185.116.162.233` | DNS only |

تست از Mac:

```bash
dig ok-sms.ir +short
dig www.ok-sms.ir +short
# هر دو باید: 185.116.162.233
```

> **توجه:** اگر دامنه به IP دیگری اشاره کند (مثلاً `193.141.65.146`)، ابتدا A record را اصلاح کنید.

## ۲) چرا Grey Cloud؟

با Proxied (نارنجی) Cloudflare گاهی به سرورهای ایران **522** می‌دهد. مثل پروژه vamyab: ترافیک مستقیم به IP سرور + SSL با Certbot.

## ۳) SSL — Certbot (بعد از DNS درست)

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d ok-sms.ir -d www.ok-sms.ir \
  --non-interactive --agree-tos --register-unsafely-without-email --redirect
```

تمدید: `systemctl status certbot.timer`

## ۴) فایروال

```bash
sudo ufw allow 3031/tcp   # SSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
```

پورت‌های `8080`، `3005`، `3006` فقط `127.0.0.1` — از بیرون باز نیستند.

## ۵) URLهای عمومی بعد از دامنه

| سرویس | آدرس |
|--------|------|
| پنل ادمین | `https://ok-sms.ir/auth` |
| API | `https://ok-sms.ir/api/...` |
| فرم SMS | `https://ok-sms.ir/form/{slug}` |
| گردونه SMS | `https://ok-sms.ir/wheel/{slug}` |
| Swagger | `https://ok-sms.ir/swagger` |
