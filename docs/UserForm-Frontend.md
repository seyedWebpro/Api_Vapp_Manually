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
- **Publish** → `status=Published` + `slug` + `publicUrl`
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
| ذخیره تغییرات | `POST /{id}/update` |
| انتشار + لینک | `POST /{id}/publish` |
| فعال/غیرفعال (فقط Published) | `POST /{id}/toggle-status` |
| حذف | `POST /{id}/delete` |
| انتخاب دفترچه تلفن | `GET /api/ContactNotebook` *(کنترلر جدا)* |

---

## فلو پیشنهادی Flutter

```
انتخاب قالب (local)  →  POST /  →  formId
ویرایش مرحله‌ای       →  POST /{id}/update  (فقط فیلدهای تغییرکرده)
پیش‌نمایش             →  local state (بدون API)
انتشار                →  POST /{id}/publish  →  publicUrl
```

`formId` را در state ویزارد/ویرایش نگه دار.

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

## `POST /{id}/update` — به‌روزرسانی جزئی

**همه فیلدها اختیاری.** فقط چیزی که می‌فرستی عوض می‌شود.

| فیلد body | رفتار |
|-----------|--------|
| `title`, `description`, `slug`, `saveToPhonebook` | فقط اگر ارسال شود |
| `isActive` | فقط فرم **Published** — مقدار صریح فعال/غیرفعال |
| `notebookIds` | اگر ارسال شود → جایگزین کامل لیست |
| `fields` | merge جزئی بر اساس `fieldKey` — فقط propertyهای ارسال‌شده عوض می‌شوند |

```json
{ "title": "عنوان جدید" }
```

```json
{
  "fields": [
    {
      "fieldKey": "mobile",
      "label": "شماره تماس"
    }
  ]
}
```

> فقط `fieldKey` الزامی است. propertyهای ارسال‌نشده (مثل `fieldType`, `isRequired`, `displayOrder`) **بدون تغییر** می‌مانند.

```json
{ "isActive": false }
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

- body خالی → `400` («هیچ موردی برای به‌روزرسانی ارسال نشده است»)

---

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
| `POST /{id}/toggle-status` | — | فقط `Published` — `isActive` برعکس می‌شود |
| `POST /{id}/delete` | — | soft delete + حذف فایل‌های آپلودشده فرم از سرور — `data: true` |

---

## خطاهای رایج

| statusCode | معنی | اقدام UI |
|------------|------|----------|
| 400 | validation | `errors[]` یا `message` |
| 403 | مالک نیست | پیام دسترسی |
| 404 | فرم نیست | برگشت به لیست |
| 500 | سرور | پیام عمومی — retry |

پیام‌های validation نمونه: slug تکراری، دفترچه نامعتبر، بدون فیلد mobile برای phonebook، بدون فیلد فعال برای publish.

---

## چیزهایی که فعلاً نیست (فاز ۲)

- `GET /api/public/form/{slug}` — schema عمومی ❌
- `POST /api/public/form/{slug}/submit` — ثبت پاسخ فرم ❌
- لیست submissionها ❌
- صفحه وب لینک SMS ❌

---

## Swagger

بعد از run: `/swagger` → `UserForm`
