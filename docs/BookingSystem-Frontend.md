# BookingSystem — راهنمای اتصال فرانت

> Backend **فاز ۱** (ویزارد + لینک) و **فاز ۲** (رزرو مشتری + SMS یادآوری) آماده اتصال است.

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
  "customSlug": "beauty-salon",
  "saveToPhonebook": true,
  "notebookIds": [12, 15]
}
```

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
      "serviceCost": 350000,
      "depositAmount": 100000
    }
  ]
}
```

- حداقل **یک خدمت**
- `serviceTempId` را کلاینت بسازد (UUID) — در مراحل ۳ و ۴ همین ID استفاده می‌شود

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

> ارسال SMS در **فاز ۲** پیاده‌سازی می‌شود؛ الان فقط ذخیره می‌شود.

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

```json
{
  "serviceId": 5,
  "startUtc": "2026-07-01T05:30:00Z",
  "customerFullName": "علی رضایی",
  "customerMobile": "09121234567"
}
```

- اگر `saveToPhonebook=true` → شماره در دفترچه‌های انتخاب‌شده ذخیره می‌شود
- `startUtc` باید دقیقاً یکی از اسلات‌های برگشتی باشد

---

## فاز ۲ — مدیریت نوبت‌ها (Auth)

| عملیات | API |
|--------|-----|
| لیست رزروها | `GET /api/BookingSystem/{id}/appointments?pageNumber=1&status=Confirmed&fromUtc=&toUtc=&serviceId=` |
| لغو نوبت | `POST /api/BookingSystem/{id}/appointments/{appointmentId}/cancel` |

### وضعیت‌ها (`status`)

`Confirmed` | `Cancelled` | `Completed`

### SMS یادآوری

- Background job هر **۱ دقیقه**
- زمان ارسال: `StartUtc - ReminderOffsetMinutes` (پنجره ۲ دقیقه‌ای)
- ماژول گزارش SMS: `BookingReminder`
