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
  "sliderImages": [{ "imageUrl": "/uploads/...", "displayOrder": 0 }],
  "serviceItems": [{ "title": "فیشیال", "price": 350000, "imageUrl": null, "displayOrder": 0 }]
}
```

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

اگر `sliderImages` یا `serviceItems` ارسال شوند، **جایگزین کامل** می‌شوند.

```json
{
  "sliderEnabled": true,
  "descriptionEnabled": true,
  "servicesEnabled": true,
  "mapEnabled": false,
  "contactEnabled": true,
  "descriptionTitle": "درباره سالن",
  "descriptionText": "متن توضیحات",
  "contactPhone": "09121234567",
  "contactEmail": "info@example.com",
  "contactInstagram": "zahra_salon",
  "sliderImages": [],
  "serviceItems": []
}
```

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

## آپلود فایل

از همان `IFileUploadService` با `entityType = "businesscard"` استفاده کنید.

---

## Public_Vapp

- مسیر: `/card/:slug`
- بدون OTP — فقط نمایش عمومی
- سرویس: `GET /api/BusinessCardPublic/{slug}`
