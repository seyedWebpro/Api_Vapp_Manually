# LuckyWheel — راهنمای اتصال فرانت (گردونه شانس)

> برای Cursor / توسعه‌دهنده Flutter. **فاز ۱ backend آماده است** (ساخت، مدیریت، انتشار، لینک).

## پیش‌نیاز

```
Base URL:  /api/LuckyWheel
Auth:      Authorization: Bearer <jwt>
Content:   application/json
```

دفترچه تلفن:

```
GET /api/ContactNotebook?pageNumber=1&pageSize=100&isActive=true
```

---

## شکل پاسخ (همه endpointها)

```json
{
  "statusCode": 200,
  "success": true,
  "message": "پیام فارسی",
  "errorCode": null,
  "data": { },
  "errors": null,
  "traceId": "0HN4K..."
}
```

### نمایش خطا به کاربر (الزامی)

| وضعیت | کار UI |
|--------|--------|
| `success=true` | پیام موفقیت اختیاری (`message`) |
| `success=false` | **فقط** `message` + در صورت وجود `errors[]` |
| `errorCode` | منطق داخلی (مثلاً `TOKEN_EXPIRED` → logout) |
| `traceId` | فقط گزارش به پشتیبانی — به کاربر نشان نده |
| HTTP 500 | همان `message` کنترل‌شده بک‌اند + دکمه retry |
| قطع شبکه | «ارتباط برقرار نشد» (فقط وقتی API پاسخ نداد) |

**هرگز نشان نده:** exception خام، SQL، stack trace، status code به‌تنهایی.

---

## منطق کسب‌وکار

| قانون | مقدار |
|--------|--------|
| جوایز | حداقل **۲**، حداکثر **۸** |
| مجموع `probability` | دقیقاً **۱۰۰** |
| `displayOrder` | ۱ تا ۸، **بدون تکرار** |
| `saveToPhonebook=true` | حداقل **۱** `notebookId` معتبر |
| `slug` | اختیاری؛ فقط `a-z`، `0-9`، `-` |
| `publicUrl` | `{LuckyWheel:PublicBaseUrl}/{slug}` |
| Preview | فقط کلاینت — از `GET /{id}` |
| `participantCount` | فعلاً **۰** (فاز ۲) |

---

## نگاشت صفحه → API

### ویزارد ساخت

| صفحه | API |
|------|-----|
| مرحله ۱ — اطلاعات + دفترچه | `POST /` |
| مرحله ۲ — جوایز | `POST /{id}/update` با `{ "items": [...] }` |
| Preview | `GET /{id}` |
| انتشار | `POST /{id}/publish` |

### مدیریت (پس از ساخت)

| صفحه UI | API |
|---------|-----|
| لیست گردونه‌ها | `GET /` |
| کارت — لینک / بج فعال | `publicUrl` + `isActive` از لیست |
| کارت — شرکت‌کننده | `participantCount` (فعلاً ۰) |
| حذف | `POST /{id}/delete` |
| تنظیمات (ورود) | `GET /{id}` |
| توگل فعال/غیرفعال | `POST /{id}/toggle-active` |
| اطلاعات اصلی | `POST /{id}/update` (بدون `items`) |
| ویرایش جوایز | `POST /{id}/update` (فقط `items`) |
| مشاهده نتایج | فاز ۲ ❌ |

---

## به‌روزرسانی جزئی (Partial Update) — مهم

`POST /{id}/update` — **همه فیلدها اختیاری.** فقط فیلدهایی که در body می‌فرستی تغییر می‌کنند.

| فیلد در body | رفتار |
|--------------|--------|
| `title` | فقط اگر ارسال شود — خالی/فاصله → `400` |
| `description` | فقط اگر ارسال شود — `""` توضیحات را پاک می‌کند |
| `slug` | فقط اگر ارسال شود (غیرخالی) |
| `saveToPhonebook` | فقط اگر ارسال شود |
| `notebookIds` | اگر ارسال شود → **جایگزین کامل** لیست |
| `items` | اگر ارسال شود → **جایگزین کامل** لیست جوایز |
| فیلد ارسال نشده | **مقدار قبلی حفظ می‌شود** |
| body خالی `{}` | `400` — «هیچ موردی برای به‌روزرسانی ارسال نشده است» |

### نکات Flutter

1. **فقط فیلدهای تغییرکرده** را بفرست — `null` یا حذف از JSON = بدون تغییر
2. **اطلاعات اصلی** و **جوایز** جدا — نیازی نیست هر دو را با هم بفرستی
3. `notebookIds` را فقط وقتی بفرست که کاربر دفترچه‌ها را تغییر داد یا toggle روشن است
4. `saveToPhonebook=false` بدون `notebookIds` → دفترچه‌های قبلی در DB می‌مانند (برای وقتی دوباره روشن شود)

### مثال‌ها

**فقط عنوان (جوایز حفظ می‌شوند):**
```json
{ "title": "گردونه جشن تابستانه" }
```

**فقط جوایز (اطلاعات اصلی حفظ می‌شود):**
```json
{
  "items": [
    { "name": "۱۰٪ تخفیف", "probability": 50, "displayOrder": 1 },
    { "name": "پوچ", "probability": 50, "displayOrder": 2 }
  ]
}
```

**اطلاعات اصلی + دفترچه:**
```json
{
  "title": "گردونه نوروز",
  "description": "توضیح جدید",
  "slug": "norooz-wheel",
  "saveToPhonebook": true,
  "notebookIds": [1, 2]
}
```

---

## Endpointها

### `POST /` — ایجاد پیش‌نویس

```json
{
  "title": "گردونه نوروز",
  "description": "اختیاری",
  "slug": "norooz-wheel",
  "saveToPhonebook": true,
  "notebookIds": [1]
}
```

پاسخ: `201` + `status: "Draft"`

---

### `GET /{id}` — جزئیات

برای Preview، تنظیمات، و پر کردن فرم ویرایش.

```json
{
  "data": {
    "id": 12,
    "title": "گردونه نوروز",
    "description": "...",
    "slug": "norooz-wheel",
    "status": "Published",
    "saveToPhonebook": true,
    "isActive": true,
    "publicUrl": "https://app.com/wheel/norooz-wheel",
    "notebookIds": [1],
    "items": [
      { "name": "۱۰٪ تخفیف", "probability": 30, "displayOrder": 1 }
    ],
    "createdAt": "...",
    "updatedAt": "...",
    "publishedAt": "..."
  }
}
```

---

### `POST /{id}/publish` — انتشار

```json
{ "slug": "norooz-wheel" }
```

یا `{}` — slug از پیش‌نویس یا auto از title.

پیش‌نیاز: `title` غیرخالی + `items` معتبر (۲–۸ آیتم، جمع ۱۰۰).

---

### `GET /` — لیست

```
GET /?pageNumber=1&pageSize=10
```

```json
{
  "data": {
    "wheels": {
      "items": [
        {
          "id": 12,
          "title": "گردونه جشن تابستانه",
          "slug": "summer-festival",
          "status": "Published",
          "isActive": true,
          "publicUrl": "https://app.com/wheel/summer-festival",
          "participantCount": 0,
          "createdAt": "...",
          "publishedAt": "..."
        }
      ],
      "totalCount": 1,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1
    }
  }
}
```

---

### `POST /{id}/toggle-active` — فعال/غیرفعال

فقط `Published`.

```json
{ "isActive": false }
```

اگر وضعیت از قبل همان باشد → `200` + «گردونه از قبل فعال/غیرفعال است»

---

### `POST /{id}/delete` — حذف

- Soft delete
- `slug` آزاد می‌شود
- فایل‌های آپلود مرتبط (در صورت وجود) از سرور حذف می‌شوند

پاسخ: `data: true`

---

## جدول خطاها

| status | errorCode | message / اقدام |
|--------|-----------|-----------------|
| 400 | `VALIDATION_FAILED` | `message` + `errors[]` — validation |
| 400 | `INVALID_INPUT` | pagination نامعتبر |
| 401 | `UNAUTHORIZED` / `TOKEN_*` | redirect login |
| 403 | `FORBIDDEN` | دسترسی ندارید |
| 404 | `NOT_FOUND` | گردونه یافت نشد |
| 500 | `UNEXPECTED_ERROR` | پیام عمومی + retry |
| 500 | `DATABASE_ERROR` | مشکل ذخیره‌سازی + retry |

### پیام‌های رایج `errors[]`

| پیام | علت |
|------|-----|
| حداقل ۲ جایزه… | کمتر از ۲ آیتم |
| حداکثر ۸ جایزه… | بیش از ۸ آیتم |
| مجموع درصد… باید ۱۰۰ باشد | جمع ≠ 100 |
| حداقل یک دفترچه… | toggle روشن بدون دفترچه |
| این slug قبلاً استفاده شده | slug تکراری |
| عنوان گردونه نمی‌تواند خالی باشد | title خالی در update |
| هیچ موردی برای به‌روزرسانی… | body خالی `{}` |

---

## فلو Flutter

### ساخت
```
POST / → wheelId
POST /{id}/update (items)
GET /{id} → preview
POST /{id}/publish → publicUrl
```

### مدیریت
```
GET / → لیست
GET /{id} → تنظیمات
POST /{id}/toggle-active
POST /{id}/update → اطلاعات اصلی یا جوایز (جدا)
POST /{id}/delete
```

---

## چک‌لیست تست سریع

| سناریو | انتظار |
|--------|--------|
| `POST /` معتبر | `201` Draft |
| `update` فقط title | `200` — items حفظ |
| `update` فقط items | `200` — title حفظ |
| `update` `{}` | `400` |
| `update` title خالی | `400` |
| `publish` بدون items | `400` |
| `toggle-active` Draft | `400` |
| `delete` سپس `GET` | `404` |
| بدون JWT | `401` |
| گردونه کاربر دیگر | `403` |

---

## فاز ۲ (فعلاً نیست)

- چرخش عمومی (`public/{slug}/spin`)
- ذخیره شماره در دفترچه هنگام چرخش
- محدودیت یک‌بار per شماره
- مشاهده نتایج / شرکت‌کنندگان
- `participantCount` واقعی
- صفحه وب HTML/CSS/JS

---

## Swagger و تست دستی

- Swagger: `/swagger` → `LuckyWheel`
- نمونه درخواست: `Api_Vapp.http` در root پروژه
