# Message — انتخاب گیرندگان پیامک (راهنمای فرانت)

> برای Cursor / توسعه‌دهنده کلاینت. Backend **آماده اتصال** است (لیست مخاطب + انتخاب دستی + سایر حالت‌ها).

## پیش‌نیاز

```
Base URL (Message):   /api/Message
Base URL (Contact):   /api/Contact
Base URL (Notebook):  /api/ContactNotebook
Auth:                 Authorization: Bearer <jwt>
Content:              application/json
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

| فیلد | کاربرد UI |
|------|-----------|
| `success=false` | عملیات ناموفق |
| `message` | پیام اصلی به کاربر |
| `errorCode` | منطق UI (`VALIDATION_FAILED`, `INVALID_INPUT`, …) |
| `errors[]` | جزئیات validation (در صورت وجود) |
| `traceId` | گزارش به پشتیبانی |

---

## منطق کسب‌وکار (خلاصه)

صفحه «انتخاب گیرندگان» سه گزینه رادیویی دارد:

| گزینه UI | `selectionType` | توضیح |
|----------|-----------------|--------|
| همه مخاطبین | `Notebook` | همه اعضای **همه دفترچه‌های** کاربر |
| دفترچه خاص | `Notebook` | اعضای دفترچه(های) تیک‌خورده |
| انتخاب دستی مخاطبین | `ContactIds` | فقط `id`های تیک‌خورده از لیست |

**نکات مهم:**

- قبل از انتخاب گیرنده باید **پیام** ساخته شده باشد (`messageId`)
- انتخاب گیرندگان یک **Session** می‌سازد (اعتبار **۲۴ ساعت**)
- `sessionId` را برای مراحل بعد (خلاصه / کمپین / ارسال) نگه دار
- `Individual` = وارد کردن **شماره موبایل دستی** — با UI چک‌باکس فرق دارد

---

## نگاشت صفحه → API

| بخش UI | API |
|--------|-----|
| ساخت متن پیام | `POST /api/Message` |
| **گزینه ۱ — همه مخاطبین** | `GET /api/ContactNotebook` → `POST /api/Message/recipients/select` |
| **گزینه ۲ — دفترچه خاص** | `GET /api/ContactNotebook` → `POST /api/Message/recipients/select` |
| **گزینه ۳ — انتخاب دستی** | `GET /api/Contact/mine` → `POST /api/Message/recipients/select` |
| خلاصه هزینه / تعداد | `GET /api/Message/{messageId}/campaign/summary` |
| محاسبه + تنظیم ارسال | `POST /api/Message/campaign/calculate-summary` |
| ایجاد کمپین | `POST /api/Message/campaign` |
| تأیید و ارسال | `POST /api/Message/campaign/{id}/confirm-and-send` |

---

## فلو پیشنهادی (ترتیب اجباری)

```
POST /api/Message                    →  messageId
[صفحه انتخاب گیرندگان — یکی از ۳ گزینه]
POST /api/Message/recipients/select  →  sessionId + totalCount
GET  /api/Message/{messageId}/campaign/summary  →  هزینه / تعداد
POST /api/Message/campaign/calculate-summary      →  تنظیمات ارسال
POST /api/Message/campaign                      →  campaignId
POST /api/Message/campaign/{id}/confirm-and-send
```

**State پیشنهادی در کلاینت:**

```dart
// نمونه — نام فیلدها را با convention پروژه هماهنگ کن
int? messageId;
int? sessionId;
List<int> selectedContactIds = [];   // فقط برای گزینه ۳
List<int> selectedNotebookIds = []; // برای گزینه ۲ (و گزینه ۱ = همه idها)
String selectionType = 'ContactIds'; // Notebook | ContactIds | Tag | Individual
```

---

## مرحله ۰ — ساخت پیام

`POST /api/Message`

```json
{
  "content": "سلام {FullName}، پیام تست"
}
```

پاسخ مهم: `data.id` → همان `messageId`

---

## گزینه ۱ — همه مخاطبین

### ۱) دریافت همه دفترچه‌ها

`GET /api/ContactNotebook?pageNumber=1&pageSize=100&isActive=true`

از `data.notebooks[].id` همه شناسه‌ها را جمع کن.

### ۲) ثبت گیرندگان

`POST /api/Message/recipients/select`

```json
{
  "messageId": 12,
  "selectionType": "Notebook",
  "contactNotebookIds": [1, 2, 3]
}
```

---

## گزینه ۲ — دفترچه خاص

### ۱) لیست دفترچه‌ها (با تعداد عضو)

`GET /api/ContactNotebook?pageNumber=1&pageSize=100&isActive=true`

| فیلد پاسخ | UI |
|-----------|-----|
| `data.notebooks[].id` | شناسه برای checkbox |
| `data.notebooks[].name` | عنوان دفترچه |
| `data.notebooks[].contactsCount` | متن «۱۲۴ عضو» |

### ۲) ثبت گیرندگان

```json
{
  "messageId": 12,
  "selectionType": "Notebook",
  "contactNotebookIds": [1, 2]
}
```

**اختیاری — حذف چند نفر از داخل دفترچه:**

```json
{
  "messageId": 12,
  "selectionType": "Notebook",
  "contactNotebookIds": [1],
  "contactIds": [45, 78]
}
```

> در حالت `Notebook`، فیلد `contactIds` یعنی **exclude** (به این‌ها پیام نمی‌رود).

---

## گزینه ۳ — انتخاب دستی مخاطبین ⭐

### ۱) لیست مخاطبین با جستجو و صفحه‌بندی

`GET /api/Contact/mine?pageNumber=1&pageSize=20&searchTerm=`

| Query | پیش‌فرض | حداکثر |
|-------|---------|--------|
| `pageNumber` | 1 | — |
| `pageSize` | 10 | 100 |
| `searchTerm` | — | نام یا شماره موبایل |

**نمونه پاسخ:**

```json
{
  "success": true,
  "data": {
    "contacts": [
      {
        "id": 12,
        "fullName": "علی رضایی",
        "mobileNumber": "09121234567",
        "contactNotebookId": 1,
        "contactNotebookName": "دفترچه مشتریان"
      }
    ],
    "totalCount": 124,
    "pageNumber": 1,
    "pageSize": 20,
    "totalPages": 7
  }
}
```

| فیلد | UI |
|------|-----|
| `contacts[].id` | مقدار checkbox — در state نگه دار |
| `contacts[].fullName` | عنوان ردیف |
| `contacts[].mobileNumber` | زیرعنوان |
| `contacts[].contactNotebookName` | اختیاری — نمایش دفترچه |
| `totalPages` | pagination / infinite scroll |

### ۲) UI چک‌باکس

- کاربر می‌تواند **حتی یک نفر** را انتخاب کند
- `selectedContactIds` را **بین صفحات** نگه دار (pagination فقط برای نمایش است)
- دکمه «تایید و مرحله بعد» → API زیر

### ۳) ثبت گیرندگان

`POST /api/Message/recipients/select`

```json
{
  "messageId": 12,
  "selectionType": "ContactIds",
  "contactIds": [12, 45, 78]
}
```

**پاسخ موفق:**

```json
{
  "success": true,
  "data": {
    "recipients": [
      {
        "contactId": 12,
        "mobileNumber": "09121234567",
        "fullName": "علی رضایی"
      }
    ],
    "totalCount": 3,
    "sessionId": 55
  }
}
```

| فیلد | UI |
|------|-----|
| `totalCount` | «۳ گیرنده انتخاب شد» |
| `sessionId` | برای مراحل بعد |
| `recipients` | پیش‌نمایش لیست (اختیاری) |

---

## مقادیر `selectionType`

| مقدار | کاربرد |
|-------|--------|
| `Notebook` | همه / دفترچه خاص |
| `ContactIds` | انتخاب دستی از لیست |
| `Tag` | فیلتر بر اساس تگ |
| `Individual` | شماره موبایل دستی (بدون لیست مخاطب) |

---

## مراحل بعد از انتخاب گیرنده

### خلاصه — `GET /api/Message/{messageId}/campaign/summary`

بدون body. Session فعال همان `messageId` خوانده می‌شود.

### محاسبه — `POST /api/Message/campaign/calculate-summary`

```json
{
  "messageId": 12,
  "sendType": "Quick",
  "scheduledAt": null,
  "preventDuplicate": false,
  "duplicatePreventionHours": 24,
  "sendToSpecificTags": false,
  "selectedTagIds": null
}
```

`sendType`: `"Quick"` | `"Scheduled"` — برای زمان‌دار `scheduledAt` الزامی (UTC).

### ایجاد کمپین — `POST /api/Message/campaign`

همان body بالا.

### ارسال — `POST /api/Message/campaign/{campaignId}/confirm-and-send`

بدون body (یا طبق Swagger پروژه).

---

## خطاهای رایج — انتخاب گیرنده

| statusCode | errorCode | معنی | اقدام UI |
|------------|-----------|------|----------|
| 400 | `VALIDATION_FAILED` | `contactIds` خالی یا نوع انتخاب نامعتبر | پیام + برگشت به فرم |
| 400 | `INVALID_INPUT` | بعضی `contactIds` نامعتبر یا متعلق به کاربر دیگر | پیام + پاک کردن idهای invalid |
| 400 | — | `messageId` نامعتبر | برگشت به مرحله ساخت پیام |
| 401 | `UNAUTHORIZED` | بدون توکن | login |
| 500 | `UNEXPECTED_ERROR` | خطای سرور | پیام عمومی + `traceId` |

**نمونه خطای id نامعتبر:**

```json
{
  "success": false,
  "statusCode": 400,
  "errorCode": "INVALID_INPUT",
  "message": "برخی مخاطبین انتخاب‌شده نامعتبر هستند یا متعلق به شما نیستند: [99999]"
}
```

---

## نکات پیاده‌سازی Flutter / Web

1. **جستجو:** debounce ~۳۰۰ms روی `searchTerm`، سپس `GET /api/Contact/mine`
2. **انتخاب بین صفحات:** `Set<int> selectedContactIds` در state سراسری ویزارد
3. **دکمه بعد:** اگر `selectedContactIds.isEmpty` → disable + پیام «حداقل یک مخاطب»
4. **تغییر گزینه رادیویی:** state انتخاب قبلی را پاک کن یا per-tab نگه دار (ترجیح: per-tab)
5. **Session منقضی:** اگر مرحله بعد 400 داد → دوباره `recipients/select` بزن

---

## چیزهایی که عمداً نیست

- endpoint جدا فقط برای «تیک زدن» ❌ — همان `recipients/select` با `ContactIds`
- `GET /api/Contact/all` برای این UI ❌ — بدون auth و همه کاربران (استفاده نکن)
- `Individual` برای UI چک‌باکس ❌ — از `ContactIds` استفاده کن

---

## Swagger

بعد از run پروژه: `/swagger`

- `Contact` → `GET /mine`
- `Message` → `POST /recipients/select`

---

## تست سریع (curl)

```bash
# لیست مخاطبین
curl -s "http://localhost:5054/api/Contact/mine?pageSize=20" \
  -H "Authorization: Bearer TOKEN"

# انتخاب دستی
curl -s -X POST "http://localhost:5054/api/Message/recipients/select" \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"messageId":1,"selectionType":"ContactIds","contactIds":[1,2]}'
```
