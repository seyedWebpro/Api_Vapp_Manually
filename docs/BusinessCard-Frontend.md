# BusinessCard — راهنمای اتصال فرانت (کارت ویزیت)

> برای Cursor / توسعه‌دهنده Flutter و Public_Vapp. معماری و endpointها هم‌تراز با **فرم‌ساز (UserForm)** است.

## پیش‌نیاز

```
Base URL:  /api/BusinessCard
Auth:      Authorization: Bearer <jwt>
Content:   application/json

Public:    /api/BusinessCardPublic/{slug}  (بدون Auth)
```

## شکل پاسخ (همه endpointها)

```json
{
  "statusCode": 200,
  "success": true,
  "message": "پیام فارسی",
  "errorCode": null,
  "data": { },
  "errors": null
}
```

- `success=false` → `message` (+ در صورت وجود `errors[]`) را به کاربر نشان بده
- تاریخ‌ها **UTC** هستند
- خطای 403 = کارت متعلق به کاربر دیگر است

---

## منطق کسب‌وکار (خلاصه)

- **قالب‌ها (Template)** فقط سمت Flutter — backend فقط `templateKey` + پیکربندی نهایی را ذخیره می‌کند
- **Preview** فقط کلاینت — API ندارد
- **Publish** → `status=Published` + `slug` + `publicUrl` + `isActive=true`
- سوئیچ فعال/غیرفعال فقط بعد از Publish (مثل فرم‌ساز / گردونه)
- حداقل **یک بخش فعال** برای انتشار لازم است
- `slug` فقط: `a-z`، `0-9`، `-` (مثلاً `zahra-salon`)
- حداکثر **۲۰** تصویر اسلایدر و **۵۰** تعرفه

---

## نگاشت صفحه → API

| صفحه UI | API |
|---------|-----|
| لیست کارت‌ها | `GET /?pageNumber=1&pageSize=10` |
| جزئیات / ویرایش | `GET /{id}` |
| ساخت پیش‌نویس (پس از اطلاعات اصلی) | `POST /` |
| ذخیره اطلاعات اصلی | `POST /{id}/update-info` |
| ذخیره بخش‌ها | `POST /{id}/update-sections` |
| انتشار + لینک | `POST /{id}/publish` |
| تنظیمات — سوئیچ فعال بودن | `POST /{id}/toggle-active` *(alias: `toggle-status`)* |
| حذف | `POST /{id}/delete` |
| ارسال سریع SMS به مخاطب | `POST /quick-send` — نیاز به `business_card` + `free_quick_send` |
| صفحه عمومی وب | `GET /api/BusinessCardPublic/{slug}` |

---

## فلو پیشنهادی Flutter

```
انتخاب قالب (local)     →  draft محلی
اطلاعات اصلی            →  POST /  →  cardId
ویرایش بخش‌ها           →  POST /{id}/update-sections
پیش‌نمایش               →  local state (بدون API)
انتشار                  →  POST /{id}/publish  →  publicUrl
```

`cardId` را در state ویزارد نگه دار.

Public URL مثال: `https://ok-sms.ir/card/{slug}` (از `BusinessCard:PublicBaseUrl`)

---

## `POST /` — ایجاد پیش‌نویس

```json
{
  "templateKey": "business",
  "title": "سالن زیبایی زهرا",
  "logoUrl": "/uploads/...",
  "slug": "zahra-salon",
  "sliderEnabled": true,
  "descriptionEnabled": true,
  "servicesEnabled": true,
  "mapEnabled": true,
  "contactEnabled": true,
  "descriptionTitle": "درباره ما",
  "descriptionText": "...",
  "mapLatitude": 35.7,
  "mapLongitude": 51.4,
  "mapAddress": "تهران",
  "contactPhone": "09121234567",
  "contactEmail": "info@example.com",
  "contactInstagram": "zahra_salon",
  "bankingEnabled": true,
  "bankAccountNumber": "1234567890",
  "bankCardNumber": "6037991234567890",
  "bankShebaNumber": "IR120170000000123456789001",
  "socialLinks": [
    { "networkType": "instagram", "label": "اینستاگرام کاری", "value": "zahra_work", "displayOrder": 0 },
    { "networkType": "whatsapp", "label": "واتساپ شخصی", "value": "09121234567", "displayOrder": 1 }
  ],
  "sliderImages": [{ "imageUrl": "/uploads/...", "displayOrder": 0 }],
  "serviceItems": [{ "title": "فیشیال", "price": 350000, "imageUrl": null, "displayOrder": 0 }]
}
```

> نکته: `contactInstagram` هنوز برای سازگاری کار می‌کند؛ ترجیح با `socialLinks` است. اگر فقط `contactInstagram` بفرستید، بک‌اند خودش یک لینک `instagram` می‌سازد.
---

## `POST /{id}/update-info`

فقط فیلدهای ارسال‌شده تغییر می‌کنند:

```json
{
  "title": "سالن زیبایی زهرا",
  "logoUrl": "/uploads/...",
  "clearLogo": false,
  "slug": "zahra-salon"
}
```

---

## `POST /{id}/update-sections`

اگر `sliderImages` یا `serviceItems` یا `socialLinks` ارسال شوند، **جایگزین کامل** می‌شوند.

```json
{
  "sliderEnabled": true,
  "descriptionEnabled": true,
  "servicesEnabled": true,
  "mapEnabled": false,
  "contactEnabled": true,
  "bankingEnabled": true,
  "descriptionTitle": "درباره سالن",
  "descriptionText": "متن توضیحات",
  "contactPhone": "09121234567",
  "contactEmail": "info@example.com",
  "bankAccountNumber": "1234567890",
  "bankCardNumber": "6037991234567890",
  "bankShebaNumber": "IR120170000000123456789001",
  "socialLinks": [
    { "networkType": "instagram", "label": "اینستاگرام کاری", "value": "zahra_work", "displayOrder": 0 },
    { "networkType": "instagram", "label": "اینستاگرام شخصی", "value": "zahra_personal", "displayOrder": 1 },
    { "networkType": "whatsapp", "label": "واتساپ پشتیبانی", "value": "09121234567", "displayOrder": 2 },
    { "networkType": "telegram", "value": "zahra_salon", "displayOrder": 3 },
    { "networkType": "eitaa", "value": "zahra_salon", "displayOrder": 4 },
    { "networkType": "rubika", "value": "zahra_salon", "displayOrder": 5 },
    { "networkType": "bale", "value": "zahra_salon", "displayOrder": 6 },
    { "networkType": "website", "value": "https://example.com", "displayOrder": 7 }
  ],
  "sliderImages": [],
  "serviceItems": []
}
```

### `socialLinks`

| فیلد | توضیح |
|------|--------|
| `networkType` | اجباری — یکی از: `instagram`, `telegram`, `whatsapp`, `linkedin`, `twitter`, `youtube`, `facebook`, `tiktok`, `snapchat`, `rubika`, `soroush`, `eitaa`, `bale`, `website`, `custom` |
| `label` | اختیاری — نام نمایشی (مثلاً «اینستاگرام کاری»)؛ اگر خالی باشد برچسب فارسی پیش‌فرض نوع شبکه استفاده می‌شود |
| `value` | اجباری — هندل / شماره / URL |
| `displayOrder` | ترتیب نمایش |

- می‌توان چند لینک از **یک نوع** داشت (دو اینستاگرام، دو واتساپ، …)
- حداکثر ۳۰ لینک
- `contactInstagram` برای سازگاری نگه داشته شده؛ با اولین لینک `instagram` در `socialLinks` همگام می‌شود

### اطلاعات بانکی

| فیلد | توضیح |
|------|--------|
| `bankingEnabled` | نمایش بخش بانکی در صفحه عمومی |
| `bankAccountNumber` | شماره حساب (فقط ارقام) |
| `bankCardNumber` | شماره کارت ۱۶ رقمی |
| `bankShebaNumber` | شبا — `IR` + ۲۴ رقم (فاصله و حروف کوچک قبول و نرمال می‌شود) |

---

## `POST /{id}/publish`

```json
{ "slug": "zahra-salon" }
```

یا `{}` / بدون body → از slug ذخیره‌شده یا تولید خودکار از عنوان استفاده می‌شود.

---

## `POST /{id}/toggle-active`

```json
{ "isActive": false }
```

فقط برای کارت‌های `Published`.

---

## `POST /quick-send` — ارسال سریع به مخاطب

پس از ذخیره مخاطب در مودال «ارسال سریع»، کاربر کارت را انتخاب می‌کند و لینک عمومی کارت با SMS ارسال می‌شود.

```json
{
  "contactId": 12,
  "businessCardId": 3
}
```

شرایط:
- مخاطب متعلق به کاربر باشد
- کارت متعلق به کاربر، `Published` و `IsActive=true` باشد و `publicUrl` داشته باشد
- Feature اشتراک: `business_card` (کلاس) + `free_quick_send` (این endpoint)

پاسخ موفق: `DirectSendResultDto` (`sentCount`, `failedCount`, `totalCost`, …) — مشابه `SocialMediaLink/quick-send`.

لیست کارت‌ها برای انتخاب در UI: همان `GET /api/BusinessCard`.

---

## آپلود فایل

الگوی مشابه Contact/User:

`POST /api/BusinessCard/{id}/upload-image`  
`Content-Type: multipart/form-data`  
`Authorization: Bearer <jwt>`

فیلدها:
- `imageFile` (الزامی)
- `imageType` (اختیاری): `logo` | `slider` | `service` | `image`

رفتار:
- اعتبارسنجی با `SecureFileValidator` (فقط تصویر، حداکثر ۱۰MB)
- پوشه: `uploads/businesscard/{id}/{logo|slider|service|images}/...`
- پاسخ `data` = URL قابل نمایش (`GetFileUrl`) مثل بقیه ماژول‌ها
- `imageType=logo` → لوگوی قبلی حذف و مسیر در DB ذخیره می‌شود
- `slider` / `service` → فقط آپلود؛ URL را در `update-sections` ذخیره کنید

نمونه پاسخ:

```json
{
  "success": true,
  "message": "لوگو با موفقیت آپلود شد",
  "data": "/uploads/businesscard/12/logo/7f4d1f....png"
}
```

نگاشت:
- لوگو → همین endpoint با `imageType=logo` (نیازی به update-info جدا برای لوگو نیست)
- بنر اسلایدر → `imageType=slider` سپس `sliderImages[].imageUrl`
- عکس تعرفه → `imageType=service` سپس `serviceItems[].imageUrl`

---

## Public_Vapp

- مسیر: `/card/:slug`
- دمو قالب: `/card/demo`
- بدون OTP — فقط نمایش عمومی
- سرویس: `GET /api/BusinessCardPublic/{slug}`
- قالب‌ها سمت وب (هم‌تراز موبایل): `classic` | `shop` | `personal` | `corporate` | `creative`
- Alias: `templateKey=business` → قالب `creative` (مثل Flutter)
- `descriptionTitle` → tagline در هیرو
- بخش تماس: `contactPhone` / `contactEmail` / `socialLinks[]`
- بخش بانکی: `bankingEnabled` + شماره حساب/کارت/شبا (با دکمه کپی)
- اکشن‌بار پایین صفحه: تماس / **کپی لینک** / **اشتراک لینک** / **افزودن به مخاطب** (vCard)
