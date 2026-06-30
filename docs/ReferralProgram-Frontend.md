# ReferralProgram — راهنمای اتصال فرانت

> برای Cursor / توسعه‌دهنده کلاینت. Backend فاز فعلی **آماده اتصال** است.

## پیش‌نیاز

```
Base URL:  /api/ReferralProgram
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

- `success=false` → پیام را به کاربر نشان بده (`message` + در صورت وجود `errors[]`)
- `statusCode` HTTP واقعی است (200, 201, 400, 404, 500)
- تاریخ‌ها UTC هستند

---

## منطق کسب‌وکار (خلاصه)

- هر **برنامه** = **یک کد عمومی** (`PublicCode` مثل `REF482931`) برای همه
- **کی کد آورده مهم نیست** — inquire/redeem مخاطب را چک نمی‌کند
- **مرحله ۲ ویزارد** فقط تعیین می‌کند **به چه کسانی SMS** برود
- **inquire** = فقط بررسی (منقضی؟ مبلغ تخفیف؟) — چیزی ثبت نمی‌شود
- **redeem** = ثبت مصرف در تاریخچه (+ واریز اختیاری به کش‌بک مخاطب)

---

## نگاشت صفحه → API

| صفحه UI | API |
|---------|-----|
| داشبورد KPI | `GET /dashboard/stats` |
| لیست برنامه‌ها | `GET /?pageNumber=1&pageSize=10&isActive=` |
| جزئیات برنامه | `GET /{id}` |
| فعال/غیرفعال | `POST /{id}/toggle-status` |
| حذف | `POST /{id}/delete` |
| ویرایش | `POST /{id}/update` |
| تاریخچه مصرف | `GET /{id}/history?pageNumber=1&pageSize=10` |
| ویزارد — مرحله ۱ | `POST /validate-step1` |
| ویزارد — مرحله ۲ (دفترچه‌ها) | `GET /notebooks` + `POST /validate-step2` |
| ویزارد — مرحله ۳ | `POST /settings/save` |
| ویزارد — خلاصه | `GET /summary?draftId=` |
| ویزارد — تأیید نهایی | `POST /confirm` |
| فروشگاه — استعلام کد | `POST /inquire` |
| فروشگاه — ثبت مصرف | `POST /redeem` |

---

## ویزارد (ترتیب اجباری)

```
validate-step1  →  draftId
validate-step2  →  draftId + TargetAudience
settings/save   →  draftId + Settings (تاریخ)
summary         →  draftId (اختیاری قبل از confirm)
confirm         →  draftId  →  برنامه + PublicCode
```

**draftId** را در state اپ نگه دار. اعتبار پیش‌نویس: **۲۴ ساعت**.

### مرحله ۱ — `POST /validate-step1`

```json
{
  "title": "پاداش نوروز",
  "isActive": true,
  "rewardType": "FixedAmount",
  "referrerRewardValue": 50000,
  "isCustomerRewardActive": true,
  "customerRewardValue": 10000
}
```

`rewardType`: `"Percentage"` | `"FixedAmount"`

پاسخ مهم: `data.draftId`, `data.draftExpiresAt`

### مرحله ۲ — `POST /validate-step2`

```json
{
  "draftId": "123_uuid",
  "targetAudience": "All",
  "targetNotebookIds": null,
  "targetContactIds": null
}
```

`targetAudience`: `"All"` | `"SpecificNotebooks"` | `"Individual"`

### مرحله ۳ — `POST /settings/save`

```json
{
  "draftId": "123_uuid",
  "settings": {
    "startDate": "2026-06-27T00:00:00Z",
    "endDate": "2026-07-27T00:00:00Z",
    "sendToSpecificTags": false,
    "targetTagIds": null
  }
}
```

**پاسخ — تعداد مخاطب:**
```json
{
  "data": {
    "totalContactsCount": 150,
    "contactsCount": 150
  }
}
```

- `totalContactsCount` = مخاطب step2 (بدون تگ)
- `contactsCount` = گیرنده SMS (بعد از فیلتر تگ؛ اگر تگ نباشد برابر totalContactsCount)

`GET /summary?draftId=` همان فیلدها را برمی‌گرداند.

### تأیید — `POST /confirm`

```json
{ "draftId": "123_uuid" }
```

پاسخ: `data.program.publicCode`, `data.smsSentCount`

---

## فروشگاه

### استعلام — `POST /inquire`

```json
{
  "code": "REF482931",
  "purchaseAmount": 500000
}
```

| فیلد پاسخ | UI |
|-----------|-----|
| `isValid` | کد قابل استفاده |
| `isExpired` | منقضی |
| `isNotStarted` | هنوز شروع نشده |
| `customerDiscountAmount` | مبلغ تخفیف (برای درصدی → `purchaseAmount` بفرست) |
| `formattedCustomerDiscount` | متن آماده نمایش |

کد اشتباه → `success=true` ولی `isValid=false` (404 نیست).

### ثبت مصرف — `POST /redeem`

```json
{
  "code": "REF482931",
  "purchaseAmount": 500000,
  "customerContactId": null,
  "referrerContactId": null,
  "description": "فروش فاکتور ۱۲۳"
}
```

- `purchaseAmount` برای `Percentage` **الزامی**
- `customerContactId` / `referrerContactId` **اختیاری** — فقط برای واریز کش‌بک
- موفق → `statusCode: 201`

---

## ویرایش جزئی — `POST /{id}/update`

فقط فیلدهای ارسالی عوض می‌شوند. body خالی → 400.

```json
{
  "title": "نام جدید",
  "isActive": true,
  "referrerRewardValue": 60000,
  "isCustomerRewardActive": true,
  "customerRewardValue": 15000,
  "endDate": "2026-08-01T00:00:00Z"
}
```

---

## داشبورد — `GET /dashboard/stats`

```json
{
  "successfulReferrals": 12,
  "totalRewardsPaid": 850000,
  "formattedTotalRewardsPaid": "850,000 تومان",
  "activeProgramsCount": 3,
  "activeUsersCount": 8
}
```

---

## خطاهای رایج

| statusCode | معنی | اقدام UI |
|------------|------|----------|
| 400 | validation | `errors[]` یا `message` |
| 404 | برنامه/کد (در redeem) | پیام مناسب |
| 500 | سرور | پیام عمومی — retry |

---

## چیزهایی که عمداً نیست

- کد جدا per مخاطب ❌
- محدودیت «فقط مخاطبین لیست SMS» در inquire/redeem ❌
- SMS بعد از redeem ❌ (فعلاً)
- job خودکار انقضا ❌ (تاریخ در query چک می‌شود)

---

## Swagger

بعد از run پروژه: `/swagger` → `ReferralProgram`
