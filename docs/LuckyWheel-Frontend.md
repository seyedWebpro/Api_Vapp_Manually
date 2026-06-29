# LuckyWheel — راهنمای اتصال و تست فرانت (گردونه شانس)

> برای Cursor / توسعه‌دهنده Flutter. **فاز ۱ backend آماده اتصال است** (ساخت، ویرایش، انتشار، لینک).

## پیش‌نیاز

```
Base URL:  /api/LuckyWheel
Auth:      Authorization: Bearer <jwt>
Content:   application/json
```

دفترچه تلفن (مرحله ۱ UI):

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

### قانون نمایش خطا به کاربر

| فیلد | کاربرد UI |
|------|-----------|
| `success=false` | عملیات ناموفق |
| `message` | **همیشه** پیام اصلی به کاربر (فارسی، کنترل‌شده) |
| `errors[]` | لیست جزئیات validation — زیر `message` یا کنار فیلدها |
| `errorCode` | منطق داخلی اپ (مثلاً `TOKEN_EXPIRED` → logout) |
| `traceId` | فقط در گزارش به پشتیبانی — **به کاربر عادی نشان نده** |

**هرگز نشان نده:** متن خام exception، stack trace، پیام SQL، کد HTTP به‌تنهایی.

**خطای 500:** فقط `message` بک‌اند را نشان بده (مثلاً «خطای غیرمنتظره…») — retry پیشنهاد بده.  
**قطع شبکه:** فقط وقتی API اصلاً پاسخ نداد → «ارتباط برقرار نشد».

---

## منطق کسب‌وکار (خلاصه)

- الگو **مشابه UserForm**: پیش‌نویس → آپدیت → انتشار
- **Preview** فقط کلاینت — API جدا ندارد؛ از `GET /{id}` داده بگیر
- **Publish** → `status=Published` + `slug` + `publicUrl`
- جوایز: حداقل **۲**، حداکثر **۸** آیتم
- مجموع `probability` همه آیتم‌ها = **دقیقاً ۱۰۰**
- `displayOrder` بین ۱ تا ۸ — **بدون تکرار**
- اگر `saveToPhonebook=true` → حداقل **۱** `notebookId` معتبر
- `slug` اختیاری؛ فقط `a-z`، `0-9`، `-` (مثلاً `norooz-wheel`)
- `publicUrl` = `{LuckyWheel:PublicBaseUrl}/{slug}` (مثلاً `https://app.com/wheel/norooz-wheel`)

---

## نگاشت صفحه → API

| صفحه UI | API |
|---------|-----|
| لیست گردونه‌ها | `GET /?pageNumber=1&pageSize=10` |
| جزئیات / Preview | `GET /{id}` |
| ویزارد — مرحله ۱ (اطلاعات + دفترچه) | `POST /` |
| ویزارد — مرحله ۲ (جوایز + درصد) | `POST /{id}/update` |
| ویزارد — مرحله ۳ (تأیید و انتشار) | `POST /{id}/publish` |
| فعال/غیرفعال (فقط Published) | `POST /{id}/toggle-status` |
| حذف | `POST /{id}/delete` |
| انتخاب دفترچه | `GET /api/ContactNotebook` |

---

## فلو پیشنهادی Flutter

```
مرحله ۱  →  POST /              →  wheelId (در state نگه دار)
مرحله ۲  →  POST /{id}/update   →  items + probabilities
Preview  →  GET /{id}           →  رندر گردونه (local)
انتشار   →  POST /{id}/publish  →  publicUrl
```

`wheelId` را تا پایان ویزارد در state نگه دار.

---

## Endpointها — جزئیات و نمونه

### `POST /` — ایجاد پیش‌نویس (مرحله ۱)

**Body نمونه:**

```json
{
  "title": "گردونه نوروز",
  "description": "شانس خود را امتحان کنید",
  "slug": "norooz-wheel",
  "saveToPhonebook": true,
  "notebookIds": [1, 2]
}
```

| فیلد | الزامی | توضیح |
|------|--------|--------|
| `title` | خیر* | *برای publish الزامی است |
| `description` | خیر | حداکثر ۲۰۰۰ کاراکتر |
| `slug` | خیر | در create یا publish |
| `saveToPhonebook` | خیر | پیش‌فرض `false` |
| `notebookIds` | شرطی | اگر `saveToPhonebook=true` → حداقل ۱ |

**پاسخ موفق:** `statusCode=201`، `data.status = "Draft"`

```json
{
  "statusCode": 201,
  "success": true,
  "message": "پیش‌نویس گردونه با موفقیت ایجاد شد",
  "data": {
    "id": 12,
    "title": "گردونه نوروز",
    "status": "Draft",
    "saveToPhonebook": true,
    "notebookIds": [1, 2],
    "items": [],
    "publicUrl": null
  }
}
```

---

### `POST /{id}/update` — به‌روزرسانی (مرحله ۲ و ویرایش)

**همه فیلدها اختیاری.** فقط چیزی که می‌فرستی عوض می‌شود.

| فیلد body | رفتار |
|-----------|--------|
| `title`, `description`, `slug`, `saveToPhonebook` | فقط اگر ارسال شود |
| `notebookIds` | اگر ارسال شود → **جایگزین کامل** لیست |
| `items` | اگر ارسال شود → **جایگزین کامل** لیست جوایز |

**Body نمونه — مرحله ۲ (جوایز):**

```json
{
  "items": [
    { "name": "۱۰٪ تخفیف", "probability": 30, "displayOrder": 1 },
    { "name": "۲۰٪ تخفیف", "probability": 30, "displayOrder": 2 },
    { "name": "پوچ", "probability": 40, "displayOrder": 3 }
  ]
}
```

**Body نمونه — فقط عنوان:**

```json
{ "title": "عنوان جدید" }
```

---

### `GET /{id}` — جزئیات (Preview)

همان ساختار `LuckyWheelResponseDto` + آرایه کامل `items` و `notebookIds`.

```json
{
  "data": {
    "id": 12,
    "title": "گردونه نوروز",
    "status": "Draft",
    "items": [
      { "name": "۱۰٪ تخفیف", "probability": 30, "displayOrder": 1 }
    ],
    "notebookIds": [1],
    "publicUrl": null
  }
}
```

---

### `POST /{id}/publish` — انتشار + لینک

```json
{ "slug": "norooz-wheel" }
```

یا body خالی `{}` — slug از پیش‌نویس یا auto-generate از title.

**پاسخ موفق:**

```json
{
  "statusCode": 200,
  "success": true,
  "message": "گردونه با موفقیت منتشر شد",
  "data": {
    "id": 12,
    "status": "Published",
    "slug": "norooz-wheel",
    "publicUrl": "https://app.com/wheel/norooz-wheel",
    "isActive": true,
    "publishedAt": "2026-06-29T18:00:00Z"
  }
}
```

---

### `GET /` — لیست

Query: `pageNumber` (پیش‌فرض 1)، `pageSize` (پیش‌فرض 10، حداکثر 100)

```json
{
  "data": {
    "wheels": {
      "items": [
        {
          "id": 12,
          "title": "گردونه نوروز",
          "slug": "norooz-wheel",
          "status": "Published",
          "isActive": true,
          "publicUrl": "https://app.com/wheel/norooz-wheel",
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

### سایر endpointها

| API | Body | نتیجه |
|-----|------|--------|
| `POST /{id}/toggle-status` | — | فقط `Published` — `isActive` برعکس می‌شود |
| `POST /{id}/delete` | — | soft delete — `data: true` |

---

## جدول خطاها — کامل

| statusCode | errorCode | message نمونه | اقدام UI |
|------------|-----------|---------------|----------|
| 400 | `VALIDATION_FAILED` | داده‌های ورودی نامعتبر است | `errors[]` را کنار فیلدها نشان بده |
| 400 | `VALIDATION_FAILED` | هیچ موردی برای به‌روزرسانی ارسال نشده است | body خالی نفرست |
| 400 | `VALIDATION_FAILED` | عنوان گردونه الزامی است | قبل از publish عنوان بگیر |
| 400 | `VALIDATION_FAILED` | جوایز گردونه نامعتبر است | `errors[]` (جمع درصد، تعداد…) |
| 400 | `VALIDATION_FAILED` | تنظیمات دفترچه تلفن نامعتبر است | دفترچه انتخاب کن |
| 400 | `VALIDATION_FAILED` | فرمت slug نامعتبر است | فقط a-z, 0-9, - |
| 400 | `VALIDATION_FAILED` | این slug قبلاً استفاده شده است | slug دیگر |
| 400 | `VALIDATION_FAILED` | فقط گردونه‌های منتشرشده قابل فعال/غیرفعال… | فقط روی Published |
| 400 | `INVALID_INPUT` | شماره صفحه باید بزرگتر از صفر باشد | pageNumber ≥ 1 |
| 400 | `INVALID_INPUT` | تعداد در هر صفحه باید بین 1 تا 100 باشد | pageSize 1–100 |
| 401 | `UNAUTHORIZED` / `TOKEN_INVALID` | توکن نامعتبر… | redirect به login |
| 403 | `FORBIDDEN` | شما مجاز به انجام این عملیات نیستید | گردونه متعلق به کاربر دیگر |
| 404 | `NOT_FOUND` | گردونه یافت نشد | برگشت به لیست |
| 500 | `UNEXPECTED_ERROR` | خطای غیرمنتظره… | retry + پشتیبانی |
| 500 | `DATABASE_ERROR` | مشکلی در ذخیره‌سازی… | retry |

### پیام‌های `errors[]` (validation جوایز)

| پیام | علت | پیشگیری سمت Flutter |
|------|-----|---------------------|
| حداقل ۲ جایزه برای گردونه لازم است | کمتر از ۲ آیتم | دکمه ادامه را تا ۲ آیتم غیرفعال کن |
| حداکثر ۸ جایزه… | بیش از ۸ آیتم | حداکثر ۸ ردیف در UI |
| نام جایزه الزامی است | `name` خالی | validation فرم |
| درصد شانس… بین 0.01 تا 100 | مقدار نامعتبر | input عددی 0.01–100 |
| ترتیب نمایش X تکراری است | `displayOrder` تکراری | ترتیب یکتا بده |
| مجموع درصد… باید دقیقاً 100 باشد | جمع ≠ 100 | شمارنده مجموع روی UI (مثلاً ۹۵٪) |
| حداقل یک دفترچه تلفن باید انتخاب شود | toggle روشن بدون دفترچه | اجبار انتخاب دفترچه |
| دفترچه X یافت نشد… | id نامعتبر | فقط از `ContactNotebook` API |

---

## چک‌لیست تست — هر endpoint

### ۱. `POST /` — ایجاد پیش‌نویس

| # | سناریو | انتظار |
|---|--------|--------|
| 1 | body معتبر کامل | `201` + `success=true` + `status=Draft` |
| 2 | فقط `title` + `saveToPhonebook=false` | `201` |
| 3 | `saveToPhonebook=true` بدون `notebookIds` | `400` + `VALIDATION_FAILED` |
| 4 | `notebookIds: [999999]` | `400` + دفترچه نامعتبر |
| 5 | `slug: "slug with spaces"` | `400` + فرمت slug نامعتبر |
| 6 | `title` بیش از ۲۰۰ کاراکتر | `400` + ModelState |
| 7 | بدون توکن | `401` |

### ۲. `POST /{id}/update`

| # | سناریو | انتظار |
|---|--------|--------|
| 1 | فقط `title` | `200` — items قبلی حفظ |
| 2 | `items` با جمع ۱۰۰ و ۲–۸ آیتم | `200` |
| 3 | `items` با جمع ۹۵ | `400` + جمع درصد |
| 4 | `items` با ۱ آیتم | `400` + حداقل ۲ جایزه |
| 5 | `items: []` | `400` |
| 6 | body خالی `{}` | `400` + هیچ موردی برای به‌روزرسانی |
| 7 | `id` نامعتبر | `404` |
| 8 | گردونه کاربر دیگر | `403` |
| 9 | `saveToPhonebook=true` + `notebookIds: []` | `400` |

### ۳. `GET /{id}`

| # | سناریو | انتظار |
|---|--------|--------|
| 1 | id معتبر | `200` + data کامل |
| 2 | id نامعتبر | `404` |
| 3 | گردونه کاربر دیگر | `403` |

### ۴. `POST /{id}/publish`

| # | سناریو | انتظار |
|---|--------|--------|
| 1 | draft کامل + items جمع ۱۰۰ + title | `200` + `Published` + `publicUrl` |
| 2 | بدون items | `400` + جوایز نامعتبر |
| 3 | بدون title | `400` + عنوان الزامی |
| 4 | `slug` تکراری | `400` + slug قبلاً استفاده شده |
| 5 | body `{}` با title و items موجود | `200` + slug خودکار |
| 6 | `saveToPhonebook=true` بدون دفترچه | `400` |

### ۵. `GET /` — لیست

| # | سناریو | انتظار |
|---|--------|--------|
| 1 | `pageNumber=1&pageSize=10` | `200` + pagination |
| 2 | `pageNumber=0` | `400` + INVALID_INPUT |
| 3 | `pageSize=200` | `400` + INVALID_INPUT |

### ۶. `POST /{id}/toggle-status`

| # | سناریو | انتظار |
|---|--------|--------|
| 1 | گردونه Published | `200` + `isActive` برعکس |
| 2 | گردونه Draft | `400` + فقط منتشرشده |

### ۷. `POST /{id}/delete`

| # | سناریو | انتظار |
|---|--------|--------|
| 1 | id معتبر | `200` + `data: true` |
| 2 | id نامعتبر | `404` |
| 3 | بعد از delete → `GET /{id}` | `404` |

---

## پیشگیری از 400 سمت Flutter (توصیه)

قبل از زدن API، این‌ها را **روی UI** چک کن:

1. **مرحله ۲:** تعداد آیتم ۲–۸، هر نام پر، هر درصد > 0، `displayOrder` یکتا، **جمع = ۱۰۰**
2. **دفترچه:** اگر toggle روشن → حداقل ۱ دفترچه از لیست `ContactNotebook`
3. **slug:** regex `^[a-z0-9]+(?:-[a-z0-9]+)*$`
4. **publish:** title غیرخالی + items معتبر
5. **update:** هرگز `{}` خالی نفرست — فقط فیلدهای تغییرکرده

---

## نمونه curl (تست دستی)

```bash
BASE=http://localhost:5054
TOKEN="Bearer YOUR_JWT"

# ۱. ایجاد پیش‌نویس
curl -s -X POST "$BASE/api/LuckyWheel" \
  -H "Authorization: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"گردونه تست","saveToPhonebook":false}'

# ۲. آپدیت جوایز (wheelId=12)
curl -s -X POST "$BASE/api/LuckyWheel/12/update" \
  -H "Authorization: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"items":[{"name":"جایزه ۱","probability":50,"displayOrder":1},{"name":"جایزه ۲","probability":50,"displayOrder":2}]}'

# ۳. انتشار
curl -s -X POST "$BASE/api/LuckyWheel/12/publish" \
  -H "Authorization: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"slug":"test-wheel"}'

# ۴. validation fail — جمع درصد ≠ 100
curl -s -X POST "$BASE/api/LuckyWheel/12/update" \
  -H "Authorization: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"items":[{"name":"الف","probability":30,"displayOrder":1},{"name":"ب","probability":30,"displayOrder":2}]}'
```

فایل `Api_Vapp.http` در root پروژه هم نمونه درخواست دارد.

---

## چیزهایی که فعلاً نیست (فاز ۲)

- `GET /api/LuckyWheel/public/{slug}` — نمایش عمومی گردونه ❌
- `POST /api/LuckyWheel/public/{slug}/spin` — چرخش ❌
- ذخیره شماره در دفترچه هنگام چرخش ❌
- محدودیت یک‌بار per شماره ❌
- صفحه وب HTML/CSS/JS لینک SMS ❌

---

## Swagger

بعد از run: `/swagger` → `LuckyWheel`
