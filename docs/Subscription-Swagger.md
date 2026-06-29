# اشتراک — تست در Swagger

> سه endpoint اپ. برای تست دستی در Swagger یا `Api_Vapp.http`.

## پیش‌نیاز

```
Base URL:  /api/UserSubscription
Auth:      Bearer JWT  —  یا در Development با DisableAuth=true بدون توکن
```

در `appsettings.json` برای تست محلی:

```json
"Development": { "DisableAuth": true }
```

Swagger: `http://localhost:5054/swagger`

---

## ترتیب تست (مهم)

| مرحله | Endpoint | توضیح |
|-------|----------|--------|
| ۱ | `GET /catalog` | planId پلن **Plus** یا **Gold** را از `data.plans` بردار |
| ۲ | `POST /checkout/preview` | مبلغ و درگاه‌ها را ببین |
| ۳ | `POST /purchase` | `redirectUrl` برای پرداخت (فعلاً شبیه‌سازی) |

---

## ۱) کاتالوگ

```
GET /api/UserSubscription/catalog
```

بدون body. باید `200` و `success: true` برگردد.

از `data.plans` پلنی که `isFree: false` و `canPurchase: true` است انتخاب کن (معمولاً Plus).

---

## ۲) پیش‌نمایش checkout

```
POST /api/UserSubscription/checkout/preview
```

```json
{
  "planId": 2,
  "discountCode": null
}
```

`planId` را از مرحله ۱ بگذار. **پلن Free کار نمی‌کند** → 400.

---

## ۳) خرید

```
POST /api/UserSubscription/purchase
```

```json
{
  "planId": 2,
  "gateway": "Behpardakht",
  "discountCode": null
}
```

| gateway | نتیجه |
|---------|--------|
| `Behpardakht` | ✅ `redirectUrl` + `paymentId` |
| `Wallet` | ❌ 400 — برای اشتراک فعال نیست |

اگر تخفیف ۱۰۰٪ باشد → `requiresPayment: false` و اشتراک مستقیم فعال می‌شود.

---

## خطاهای عادی (۴۰۰ — نه باگ)

| شرایط | پیام تقریبی |
|--------|-------------|
| `planId` پلن رایگان | پلن رایگان قابل خرید نیست |
| همان پلن فعال دارید | اشتراک فعال دارید |
| کد تخفیف اشتباه | کد تخفیف نامعتبر |
| `gateway: Wallet` | درگاه نامعتبر |

این‌ها **رفتار درست** است؛ 500 نباید ببینی.

---

## نمونه body برای Swagger

**Preview:**
```json
{ "planId": 2 }
```

**Purchase:**
```json
{ "planId": 2, "gateway": "Behpardakht" }
```

`planId` واقعی را همیشه از `GET catalog` بگیر.
