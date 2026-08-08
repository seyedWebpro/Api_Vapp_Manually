# BookingSystem — راهنمای اتصال فرانت

> Backend **کامل** است: ویزارد ایجاد، لینک عمومی، داشبورد، تقویم/جدول، رزرو دستی، مدیریت وقت خالی، تأیید/ویرایش/لغو، SMS یادآوری.

## پیش‌نیاز

```
Base URL:  /api/BookingSystem
Auth:      Authorization: Bearer <jwt>
Feature:   اشتراک Plus با امکان online_booking
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

- `success=false` → پیام را به کاربر نشان بده
- `statusCode` HTTP واقعی است
- **همه زمان‌ها UTC** — کلاینت برای نمایش به timezone کاربر تبدیل کند

---

## نگاشت صفحه → API

| صفحه UI | API |
|---------|-----|
| لیست سیستم‌های رزرو | `GET /?pageNumber=1&pageSize=10&isActive=` |
| جزئیات / تنظیمات | `GET /{id}` |
| فعال/غیرفعال | `POST /{id}/toggle-status` |
| حذف | `POST /{id}/delete` |
| ویرایش اطلاعات کسب‌وکار | `POST /{id}/update` |
| ویزارد — مرحله ۱ | `GET /activity-types` + `GET /notebooks` + `POST /validate-step1` |
| ویزارد — مرحله ۲ (خدمات) | `POST /validate-step2` |
| ویزارد — مرحله ۳ (برنامه هفتگی) | `POST /validate-step3` |
| ویزارد — مرحله ۴ (یادآوری) | `POST /validate-step4` |
| ویزارد — خلاصه | `GET /summary?draftId=` |
| ویزارد — تأیید نهایی | `POST /confirm` |
| مدیریت خدمات | `GET /{id}/services` + CRUD زیر |
| داشبورد نوبت‌ها | `GET /{id}/dashboard` |
| تقویم ماهانه | `GET /{id}/appointments/calendar?year=&month=` |
| لیست/جدول نوبت‌ها | `GET /{id}/appointments?searchName=&status=&fromUtc=&toUtc=` |
| جزئیات نوبت | `GET /{id}/appointments/{appointmentId}` |
| فیش واریز نوبت | `GET /{id}/appointments/{appointmentId}/payment-receipt` |
| رزرو دستی | `POST /{id}/appointments/manual` |
| ویرایش نوبت | `POST /{id}/appointments/{appointmentId}/update` |
| تأیید نوبت | `POST /{id}/appointments/{appointmentId}/confirm` |
| لغو نوبت | `POST /{id}/appointments/{appointmentId}/cancel` |
| مدیریت وقت خالی | `GET /{id}/availability?date=` + `POST /{id}/availability/save` |

---

## ویزارد (ترتیب اجباری)

```
validate-step1  →  draftId
validate-step2  →  draftId + services[] (با serviceTempId)
validate-step3  →  draftId + serviceSchedules[] (per service)
validate-step4  →  draftId + serviceSettings[] (per service)
summary         →  draftId
confirm         →  draftId  →  سیستم + publicUrl
```

**draftId** را در state اپ نگه دار. اعتبار پیش‌نویس: **۲۴ ساعت**.

---

## مرحله ۱ — `POST /validate-step1`

```json
{
  "title": "سالن زیبایی",
  "activityType": "beauty_salon",
  "description": "توضیحات اختیاری",
  "location": "تهران، سعادت‌آباد",
  "customSlug": "beauty-salon",
  "saveToPhonebook": true,
  "notebookIds": [12, 15]
}
```

- `location` اختیاری — مکان نمایشی در لیست
- `customSlug` اختیاری — فقط `a-z0-9-`
- اگر `saveToPhonebook=true` → `notebookIds` الزامی
- `activityType` از `GET /activity-types`

پاسخ: `data.draftId`, `data.draftExpiresAt`

---

## مرحله ۲ — `POST /validate-step2`

```json
{
  "draftId": "123_uuid",
  "services": [
    {
      "serviceTempId": "svc-001",
      "title": "فیشیال تخصصی",
      "durationMinutes": 60,
      "hasCost": true,
      "price": 500000,
      "depositAmount": 100000
    }
  ]
}
```

- حداقل **یک خدمت**
- `serviceTempId` را کلاینت بسازد (UUID) — در مراحل ۳ و ۴ همین ID استفاده می‌شود
- `hasCost=true` → فقط **`price`** الزامی است (یک فیلد قیمت)
- `depositAmount` اختیاری است؛ اگر ارسال شود نباید منفی یا بیشتر از `price` باشد
- فیلد `serviceCost` حذف شده — ارسال نکنید

---

## مرحله ۳ — `POST /validate-step3`

برنامه هفتگی **برای هر خدمت جدا**:

```json
{
  "draftId": "123_uuid",
  "serviceSchedules": [
    {
      "serviceTempId": "svc-001",
      "weeklyDays": [
        {
          "dayOfWeek": 6,
          "isOpen": true,
          "startTimeUtc": "05:30:00",
          "endTimeUtc": "14:30:00"
        }
      ],
      "exceptions": [
        {
          "exceptionDate": "2026-07-15",
          "type": "Holiday",
          "label": "۱۵ تیر - تعطیل"
        }
      ]
    }
  ]
}
```

### UTC

| نمایش UI (تهران UTC+3:30) | ارسال به API |
|---------------------------|--------------|
| 09:00 | `05:30:00` |
| 18:00 | `14:30:00` |

### dayOfWeek (.NET)

| روز | مقدار |
|-----|-------|
| یکشنبه | 0 |
| دوشنبه | 1 |
| … | … |
| شنبه | 6 |

### Bulk apply (سمت کلاینت)

- **اعمال به همه روزهای تیک‌خورده:** قبل از ارسال، start/end را روی روزهای `isOpen=true` کپی کن
- **ساعات پیش‌فرض 08:00–17:00:** تبدیل به UTC و روی همه روزهای فعال اعمال کن

---

## مرحله ۴ — `POST /validate-step4`

تنظیمات یادآوری **برای هر خدمت جدا**:

```json
{
  "draftId": "123_uuid",
  "serviceSettings": [
    {
      "serviceTempId": "svc-001",
      "bufferMinutesBetweenAppointments": 10,
      "maxDailyReservations": 20,
      "reminderOffsetMinutes": 1440
    }
  ]
}
```

### reminderOffsetMinutes (نمونه)

| UI | مقدار |
|----|-------|
| ۱ ساعت قبل | 60 |
| ۲ ساعت قبل | 120 |
| ۱ روز قبل | 1440 |
| ۲ روز قبل | 2880 |

> SMS یادآوری توسط Background job ارسال می‌شود (هر ۱ دقیقه).

---

## تأیید — `POST /confirm`

```json
{ "draftId": "123_uuid" }
```

پاسخ نمونه:

```json
{
  "data": {
    "system": {
      "id": 12,
      "title": "سالن زیبایی",
      "slug": "beauty-salon",
      "publicUrl": "https://app.com/book/beauty-salon",
      "isActive": true,
      "services": [ ... ]
    }
  }
}
```

---

## مدیریت بعد از ایجاد

| عملیات | Endpoint |
|--------|----------|
| لیست خدمات | `GET /{id}/services` |
| افزودن خدمت | `POST /{id}/services/add` |
| ویرایش خدمت | `POST /{id}/services/{serviceId}/update` |
| حذف خدمت | `POST /{id}/services/{serviceId}/delete` |
| دریافت برنامه | `GET /{id}/services/{serviceId}/schedule` |
| ذخیره برنامه | `POST /{id}/services/{serviceId}/schedule/save` |
| افزودن استثنا | `POST /{id}/services/{serviceId}/exceptions/add` |
| حذف استثنا | `POST /{id}/services/{serviceId}/exceptions/{exceptionId}/delete` |

---

## activity-types

`GET /activity-types` → لیست `{ code, title }`

| code | title |
|------|-------|
| beauty_salon | سالن زیبایی |
| medical | پزشکی و درمان |
| consulting | مشاوره |
| fitness | ورزش و تناسب اندام |
| education | آموزش |
| vip_services | خدمات VIP |
| other | سایر |

---

## خطاهای رایج

| HTTP | علت |
|------|-----|
| 400 | اعتبارسنجی — `errors[]` را نشان بده |
| 403 | اشتراک بدون `online_booking` |
| 404 | سیستم/خدمت یافت نشد |
| 400 | draft منقضی — از مرحله ۱ دوباره شروع کن |

---

## فاز ۲ — API عمومی (بدون Auth)

Base: `/api/BookingPublic` — **AllowAnonymous**

| صفحه UI | API |
|---------|-----|
| صفحه عمومی رزرو | `GET /{slug}` |
| اسلات‌های خالی | `GET /{slug}/services/{serviceId}/slots?date=2026-07-01` |
| ثبت نوبت | `POST /{slug}/book` |
| پیگیری وضعیت نوبت | `POST /{slug}/status` |

### `GET /{slug}`

اطلاعات کسب‌وکار + لیست خدمات (بدون داده‌های داخلی)

### `GET /{slug}/services/{serviceId}/slots?date=`

`date` = تاریخ UTC (ISO: `yyyy-MM-dd`)

```json
{
  "serviceId": 5,
  "date": "2026-07-01",
  "slots": [
    { "startUtc": "2026-07-01T05:30:00Z", "endUtc": "2026-07-01T06:30:00Z" }
  ]
}
```

### `POST /{slug}/book`

پشتیبانی از **JSON** یا **multipart/form-data** (برای آپلود فیش اختیاری).

**JSON** (بدون فایل — سازگار با قبل):

```json
{
  "serviceId": 5,
  "startUtc": "2026-07-01T05:30:00Z",
  "customerFullName": "علی رضایی",
  "customerMobile": "09121234567",
  "customerNote": "درخواست مشاوره حضوری"
}
```

**multipart** (فیش اختیاری — فقط برای خدمات `hasCost=true`):

| فیلد | نوع | توضیح |
|------|-----|--------|
| `ServiceId` | int | الزامی |
| `StartUtc` | datetime | الزامی |
| `CustomerFullName` | string | الزامی |
| `CustomerMobile` | string | الزامی |
| `CustomerNote` | string | اختیاری |
| `PaymentReceiptFile` | file | اختیاری — تصویر یا PDF، حداکثر ۱۰MB |

- وضعیت اولیه نوبت عمومی: **`Pending`** (منتظر تأیید مالک)
- `customerNote` اختیاری
- فیش واریز **اجباری نیست**؛ بیعانه بودن/نبودن سرویس هم شرط نیست
- اگر خدمت رایگان باشد و فایل ارسال شود → `400 VALIDATION_FAILED`
- اگر `saveToPhonebook=true` → شماره در دفترچه‌های انتخاب‌شده ذخیره می‌شود
- `startUtc` باید دقیقاً یکی از اسلات‌های برگشتی باشد
- در پاسخ جزئیات نوبت، `hasPaymentReceipt` نشان می‌دهد فیش آپلود شده یا نه

### `POST /{slug}/status` — پیگیری وضعیت

```json
{
  "appointmentNumber": 5,
  "customerMobile": "09121234567"
}
```

پاسخ نمونه:

```json
{
  "appointmentNumber": 5,
  "status": "Pending",
  "statusTitle": "در انتظار تأیید",
  "businessTitle": "سالن زیبایی آناهیتا",
  "serviceTitle": "کوتاهی و استایل",
  "customerFullName": "علی رضایی",
  "customerMobileMasked": "0912***4567",
  "startUtc": "2026-07-31T08:15:00Z",
  "endUtc": "2026-07-31T09:00:00Z"
}
```

- بدون Auth — فقط با **شماره نوبت + موبایل** همان سیستم
- اگر مطابقت نباشد → `404`
- وضعیت‌ها: `Pending` | `Confirmed` | `Cancelled` | `Completed`
- بعد از **تأیید / لغو** توسط مالک اپ، SMS وضعیت برای مشتری ارسال می‌شود (ماژول `BookingStatus`)

---

## فاز ۳ — داشبورد و مدیریت نوبت‌ها (Auth)

| عملیات | API |
|--------|-----|
| داشبورد (آمار امروز + برنامه امروز) | `GET /{id}/dashboard?date=` |
| خلاصه تقویم ماهانه | `GET /{id}/appointments/calendar?year=2026&month=7` |
| لیست/جدول نوبت‌ها | `GET /{id}/appointments?pageNumber=1&status=&searchName=&fromUtc=&toUtc=&serviceId=` |
| جزئیات نوبت | `GET /{id}/appointments/{appointmentId}` |
| فیش واریز نوبت | `GET /{id}/appointments/{appointmentId}/payment-receipt` |
| رزرو دستی | `POST /{id}/appointments/manual` |
| ویرایش نوبت | `POST /{id}/appointments/{appointmentId}/update` |
| تأیید نوبت Pending | `POST /{id}/appointments/{appointmentId}/confirm` |
| لغو نوبت | `POST /{id}/appointments/{appointmentId}/cancel` |
| مدیریت وقت خالی — مشاهده | `GET /{id}/availability?date=2026-07-01&serviceId=` |
| مدیریت وقت خالی — ذخیره | `POST /{id}/availability/save` |

### وضعیت‌ها (`status`)

`Pending` | `Confirmed` | `Cancelled` | `Completed`

- رزرو عمومی → `Pending`
- رزرو دستی مالک → `Confirmed`
- `appointmentNumber` در پاسخ همان `id` است (نمایش `#۲۵۴۸`)
- در DTO نوبت فیلد `hasPaymentReceipt` وجود دارد (لیست/جزئیات)

### فیش واریز — `GET /{id}/appointments/{appointmentId}/payment-receipt`

Auth: Bearer JWT (مالک سیستم)

```json
{
  "appointmentId": 42,
  "appointmentNumber": 42,
  "hasPaymentReceipt": true,
  "paymentReceiptUrl": "/uploads/bookingappointment/42/payment-receipt/....jpg",
  "customerFullName": "علی رضایی",
  "serviceTitle": "کوتاهی مو"
}
```

- اگر فیش آپلود نشده باشد: `hasPaymentReceipt=false` و `paymentReceiptUrl=null` با وضعیت ۲۰۰
- URL نسبی است؛ با base API / static uploads باز شود

### داشبورد — `GET /{id}/dashboard`

```json
{
  "systemId": 12,
  "title": "سالن زیبایی",
  "activityType": "beauty_salon",
  "activityTypeTitle": "سالن زیبایی",
  "location": "تهران، سعادت‌آباد",
  "publicUrl": "https://app.com/book/beauty",
  "isActive": true,
  "stats": {
    "todayTotal": 24,
    "confirmed": 18,
    "pending": 5,
    "cancelled": 1
  },
  "todaySchedule": [ /* نوبت‌های Confirmed امروز به ترتیب زمان */ ]
}
```

### تقویم — `GET /{id}/appointments/calendar`

هر روز: `totalCount` + تا ۵ اسلات نمونه (`startUtc`, `status`, `customerFullName`, `serviceTitle`)

### رزرو دستی — `POST /{id}/appointments/manual`

```json
{
  "customerFullName": "سمیه کریمی",
  "customerMobile": "09131234567",
  "customerNote": "توضیحات اختیاری",
  "serviceId": 5,
  "startUtc": "2026-07-01T05:30:00Z"
}
```

### مدیریت وقت خالی

`GET /{id}/availability?date=yyyy-MM-dd&serviceId=`

هر اسلات: `status` = `Reserved` | `Empty` | `Blocked` + `isEnabled`

```json
// POST /{id}/availability/save
{
  "date": "2026-07-01",
  "serviceId": 5,
  "slots": [
    { "startUtc": "2026-07-01T08:00:00Z", "isEnabled": false }
  ]
}
```

- اسلات‌های `Reserved` قابل بلاک نیستند
- اسلات‌های بلاک‌شده از لیست عمومی حذف می‌شوند

### SMS یادآوری

- Background job هر **۱ دقیقه**
- فقط برای نوبت‌های `Confirmed` با `remindersEnabled=true`
- چند زمان یادآوری per service: `reminderOffsetsMinutes` (مثلاً `[1440,60]`)
- فیلد قدیمی `reminderOffsetMinutes` = Max لیست (سازگاری)
- زمان ارسال هر offset: از `StartUtc - offset` تا قبل از شروع نوبت (catch-up)
- در صورت کسری کیف‌پول، offset علامت‌گذاری نمی‌شود تا بعد از شارژ دوباره تلاش شود
- متن پیام ثابت است و **نیاز به تأیید ادمین ندارد** — `GET /api/BookingSystem/reminder-info`
- ماژول گزارش SMS: `BookingReminder`
- مشتری می‌تواند با `remindersEnabled: false` در رزرو عمومی/دستی opt-out کند
