# UserForm — راهنمای اتصال فرانت (فرم‌ساز)

> برای Cursor / توسعه‌دهنده Flutter. **فاز ۱ backend آماده اتصال است** (ساخت، ویرایش، انتشار، لیست).

## پیش‌نیاز

```
Base URL:  /api/UserForm
Auth:      Authorization: Bearer <jwt>
Content:   application/json
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
- خطای 403 = فرم متعلق به کاربر دیگر است

---

## منطق کسب‌وکار (خلاصه)

- **قالب‌ها (Template)** فقط سمت Flutter — backend فقط `templateKey` + فیلدهای نهایی را ذخیره می‌کند
- **Preview** فقط کلاینت — API ندارد
- **Publish** → `status=Published` + `slug` + `publicUrl` (یا از سوئیچ `toggle-status` با `isActive: true`)
- اگر `saveToPhonebook=true` → حداقل **۱ notebookId** + فیلد **mobile** فعال (`fieldKey` یا `fieldType`)
- حداکثر **۵۰ فیلد** در هر فرم
- `slug` فقط: `a-z`، `0-9`، `-` (مثلاً `my-form-2`)

---

## نگاشت صفحه → API

| صفحه UI | API |
|---------|-----|
| لیست فرم‌ها | `GET /?pageNumber=1&pageSize=10` |
| جزئیات / ویرایش | `GET /{id}` |
| ساخت پیش‌نویس (انتخاب قالب) | `POST /` |
| ذخیره اطلاعات اصلی | `POST /{id}/update-info` |
| ذخیره فیلدها | `POST /{id}/update-fields` |
| انتشار + لینک | `POST /{id}/publish` |
| تنظیمات — سوئیچ فعال بودن فرم | `POST /{id}/toggle-status` |
| حذف | `POST /{id}/delete` |
| انتخاب دفترچه تلفن | `GET /api/ContactNotebook` *(کنترلر جدا)* |

---

## فلو پیشنهادی Flutter

```
انتخاب قالب (local)  →  POST /  →  formId
ویرایش اطلاعات اصلی   →  POST /{id}/update-info
ویرایش فیلدها         →  POST /{id}/update-fields
پیش‌نمایش             →  local state (بدون API)
انتشار                →  POST /{id}/publish  →  publicUrl
```

`formId` را در state ویزارد/ویرایش نگه دار.

> **تغییر API (breaking):** `POST /{id}/update` حذف شد. به‌جای آن دو endpoint جدا استفاده کنید:
> - `update-info` → عنوان، توضیحات، slug، دفترچه
> - `update-fields` → آرایه `fields`
> - `toggle-status` → سوئیچ فعال بودن فرم (`isActive`)

---

## `POST /` — ایجاد پیش‌نویس

```json
{
  "templateKey": "contact-simple",
  "title": "فرم تماس",
  "description": "توضیح اختیاری",
  "slug": "contact-form",
  "saveToPhonebook": true,
  "notebookIds": [1, 2],
  "fields": [
    {
      "fieldKey": "full_name",
      "fieldType": "text",
      "label": "نام",
      "placeholder": "",
      "helpText": null,
      "isActive": true,
      "isRequired": true,
      "displayOrder": 1,
      "sourceFieldKey": null
    },
    {
      "fieldKey": "mobile",
      "fieldType": "mobile",
      "label": "موبایل",
      "isActive": true,
      "isRequired": true,
      "displayOrder": 2
    }
  ]
}
```

- `slug` اختیاری در create — اگر نباشد، موقع publish خودکار ساخته می‌شود
- پاسخ موفق: `statusCode=201`، `data.status = "Draft"`

---

## `POST /{id}/update-info` — اطلاعات اصلی

**همه فیلدها اختیاری.** فقط چیزی که می‌فرستی عوض می‌شود.

| فیلد body | رفتار |
|-----------|--------|
| `title`, `description`, `slug`, `saveToPhonebook` | فقط اگر ارسال شود |
| `notebookIds` | اگر ارسال شود → جایگزین کامل لیست |

> `isActive` اینجا نیست — فقط از `POST /{id}/toggle-status` استفاده کنید.

```json
{ "title": "عنوان جدید" }
```

```json
{
  "title": "درخواست استخدام و همکاری",
  "description": "لطفا اطلاعات خود را کامل وارد کنید.",
  "slug": "job-alpha",
  "saveToPhonebook": true,
  "notebookIds": [1, 2]
}
```

- body خالی `{}` یا بدون هیچ property → `400` + `VALIDATION_FAILED` («هیچ موردی برای به‌روزرسانی ارسال نشده است»)
- `title` خالی (`"   "`) → `400`
- `saveToPhonebook=true` بدون `notebookIds` معتبر یا بدون فیلد **mobile** فعال → `400` + `errors[]`

---

## `POST /{id}/update-fields` — فیلدهای فرم

`fields` **الزامی** — حداقل ۱ آیتم. merge جزئی بر اساس `fieldKey` (فقط propertyهای ارسال‌شده عوض می‌شوند).

| فیلد body | روتینگ |
|-----------|--------|
| `fields[].fieldKey` | الزامی — شناسه merge |
| `fields[].fieldType` | برای فیلد **جدید** الزامی |
| `fields[].label` | برای فیلد **جدید** الزامی |
| بقیه propertyها | اختیاری — partial update |

```json
{
  "fields": [
    {
      "fieldKey": "mobile",
      "fieldType": "mobile",
      "label": "شماره تماس",
      "isActive": true,
      "isRequired": true,
      "displayOrder": 1
    }
  ]
}
```

```json
{
  "fields": [
    {
      "fieldKey": "full_name",
      "isRequired": false
    }
  ]
}
```

- body خالی / `fields: []` / null → `400` + `VALIDATION_FAILED` («حداقل یک فیلد برای به‌روزرسانی الزامی است»)
- فیلد جدید بدون `fieldType` → `400`
- `fieldKey` تکراری در همان request → `400`
- اگر فرم `saveToPhonebook=true` دارد و بعد از merge فیلد mobile فعال نباشد → `400`

> **نکته phonebook:** برای ذخیره در دفترچه باید `fieldKey: "mobile"` یا `fieldType: "mobile"` باشد — `phone` / `Phone` قبول نمی‌شود.

## `POST /{id}/publish`

```json
{ "slug": "my-custom-slug" }
```

یا body خالی `{}` — slug از فرم یا auto-generate از title.

پاسخ:

```json
{
  "id": 5,
  "status": "Published",
  "slug": "contact-form",
  "publicUrl": "https://app.com/form/contact-form",
  "isActive": true,
  "publishedAt": "2026-06-27T08:00:00Z"
}
```

`publicUrl` از `FormBuilder:PublicBaseUrl` در appsettings ساخته می‌شود.

---

## `POST /{id}/toggle-status` — سوئیچ «فعال بودن فرم» (صفحه تنظیمات)

```json
{ "isActive": true }
```

| `isActive` | رفتار |
|------------|--------|
| `true` | فرم فعال — اگر Draft باشد خودکار **publish** می‌شود |
| `false` | فرم غیرفعال — لینک عمومی کار نمی‌کند |

- body خالی یا بدون `isActive` → `400`
- `GET /{id}` → فیلد `isActive` وضعیت سوئیچ را نشان می‌دهد (`Draft` = `false`)
- `update-info` دیگر `isActive` نمی‌پذیرد

پاسخ موفق: همان `UserFormResponseDto` + `status` / `publicUrl` / `isActive` به‌روز

---

## `GET /` — لیست

Query: `pageNumber`, `pageSize` (پیش‌فرض 1 و 10)

```json
{
  "data": {
    "forms": {
      "items": [
        {
          "id": 5,
          "title": "فرم تماس",
          "slug": "contact-form",
          "status": "Published",
          "isActive": true,
          "publicUrl": "https://app.com/form/contact-form",
          "createdAt": "...",
          "publishedAt": "..."
        }
      ],
      "totalCount": 1,
      "pageNumber": 1,
      "pageSize": 10
    }
  }
}
```

---

## `GET /{id}` — جزئیات

همان ساختار `UserFormResponseDto` + آرایه کامل `fields` و `notebookIds`.

---

## سایر endpointها

| API | Body | نتیجه |
|-----|------|--------|
| `POST /{id}/delete` | — | soft delete + حذف فایل‌های آپلودشده فرم از سرور — `data: true` |

---

## خطاهای رایج

| statusCode | معنی | اقدام UI |
|------------|------|----------|
| 400 | validation | `errors[]` یا `message` |
| 403 | مالک نیست | پیام دسترسی |
| 404 | فرم نیست | برگشت به لیست |
| 500 | سرور | پیام عمومی — retry |

پیام‌های validation نمونه: slug تکراری، دفترچه نامعتبر، بدون فیلد mobile برای phonebook، بدون فیلد فعال برای publish، body خالی در `update-info`، `fields` خالی در `update-fields`.

---

## چک‌لیست تست سریع (فرانت)

| # | سناریو | API | انتظار |
|---|--------|-----|--------|
| 1 | ساخت پیش‌نویس | `POST /` | `201` + `Draft` |
| 2 | فقط عنوان | `POST /{id}/update-info` `{ "title": "..." }` | `200` |
| 3 | body خالی info | `POST /{id}/update-info` `{}` | `400` |
| 4 | merge فیلد | `POST /{id}/update-fields` | `200` |
| 5 | `fields: []` | `POST /{id}/update-fields` | `400` |
| 6 | `saveToPhonebook=true` + `notebookIds` + mobile | `update-info` | `200` |
| 7 | `saveToPhonebook=true` بدون mobile | create یا update-fields | `400` |
| 8 | انتشار | `POST /{id}/publish` | `200` + `publicUrl` |
| 9 | سوئیچ فعال (Draft) | `POST /{id}/toggle-status` `{ "isActive": true }` | `200` + `Published` |
| 10 | سوئیچ غیرفعال | `POST /{id}/toggle-status` `{ "isActive": false }` | `200` + `isActive: false` |
| 11 | فرم کاربر دیگر | هر update | `403` |

---

## چیزهایی که فعلاً نیست (فاز ۲)

- `GET /api/public/form/{slug}` — schema عمومی ❌
- `POST /api/public/form/{slug}/submit` — ثبت پاسخ فرم ❌
- لیست submissionها ❌
- صفحه وب لینک SMS ❌

---

## Swagger

بعد از run: `/swagger` → `UserForm`
