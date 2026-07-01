# ReferralProgram — راهنمای اتصال فرانت

> برای Cursor / توسعه‌دهنده کلاینت. Backend **آماده اتصال** است.

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

- `success=false` → `message` (+ در صورت وجود `errors[]`) را به کاربر نشان بده
- `statusCode` HTTP واقعی است (200, 201, 400, 404, 500)
- تاریخ‌ها **UTC** هستند

---

## تغییر مهم (آخرین نسخه)

**فیلتر تگ از مرحله ۳ به مرحله ۲ منتقل شد.**

| قبل | الان |
|-----|------|
| تگ در `settings/save` | تگ در `validate-step2` |
| تعداد با تگ از step3 | تعداد با تگ از step2 → `totalContactsCount` |
| step3 = تاریخ + تگ + save | step3 = **فقط تاریخ** + save |

---

## منطق کسب‌وکار (خلاصه)

- هر **برنامه** = **یک کد عمومی** (`PublicCode` مثل `REF482931`) برای همه
- **کی کد آورده مهم نیست** — inquire/redeem مخاطب را چک نمی‌کند
- **مرحله ۲** = مخاطب + **فیلتر تگ** + تعداد نهایی SMS
- **مرحله ۳** = فقط تاریخ شروع/پایان + نمایش خلاصه
- **inquire** = فقط بررسی (منقضی؟ مبلغ تخفیف؟) — چیزی ثبت نمی‌شود
- **redeem** = ثبت مصرف در تاریخچه

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
| ویزارد — مرحله ۱ (پاداش) | `POST /validate-step1` |
| ویزارد — مرحله ۲ (مخاطب + تگ) | `GET /notebooks` + `POST /validate-step2` |
| ویزارد — مرحله ۳ (تاریخ + خلاصه) | `POST /settings/save` |
| خواندن خلاصه (اختیاری) | `GET /summary?draftId=` |
| ویزارد — تأیید نهایی | `POST /confirm` |
| فروشگاه — استعلام کد | `POST /inquire` |
| فروشگاه — ثبت مصرف | `POST /redeem` |

---

## فلو ویزارد (ترتیب اجباری)

```
step1  validate-step1   →  draftId
step2  validate-step2   →  مخاطب + تگ + totalContactsCount
step3  settings/save    →  تاریخ فقط (خلاصه + contactsCount)
       confirm           →  ساخت برنامه + PublicCode + SMS
```

`draftId` را در state نگه دار. اعتبار draft: **۲۴ ساعت**.

---

## ❌ اشتباهات رایج فرانت

| اشتباه | درست |
|--------|------|
| `settings/save` = آخرین API | **`confirm`** آخرین API است |
| تگ در body مرحله ۳ | تگ فقط در **`validate-step2`** |
| `settings/save` برای آپدیت تگ | تگ عوض شد → **`validate-step2`** دوباره |
| بستن ویزارد بعد از `settings/save` | فقط بعد از **`confirm`** ببند |
| `sendToSpecificTags` در settings | **حذف شده** از step3 |

---

## مرحله ۱ — `POST /validate-step1`

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

**پاسخ:**
```json
{
  "data": {
    "draftId": "42_abc123",
    "draftExpiresAt": "2026-06-30T12:00:00Z"
  }
}
```

---

## مرحله ۲ — `POST /validate-step2` (مخاطب + تگ)

**لیست دفترچه (اختیاری):** `GET /notebooks`

```json
{
  "draftId": "42_abc123",
  "targetAudience": "All",
  "targetNotebookIds": null,
  "targetContactIds": null,
  "sendToSpecificTags": true,
  "targetTagIds": [1, 3]
}
```

`targetAudience`: `"All"` | `"SpecificNotebooks"` | `"Individual"`

- `SpecificNotebooks` → `targetNotebookIds` الزامی
- `Individual` → `targetContactIds` الزامی
- `sendToSpecificTags: true` → `targetTagIds` حداقل ۱ عدد

**پاسخ:**
```json
{
  "data": {
    "totalContactsCount": 45,
    "targetAudienceDescription": "همه مخاطبین + 2 تگ"
  }
}
```

### تعداد مخاطب — UI

- **همیشه `totalContactsCount` را نشان بده**
- تگ / مخاطب عوض شد → **دوباره `validate-step2`** (debounce 500ms)
- **`settings/save` برای تگ نزن**

---

## مرحله ۳ — `POST /settings/save` (فقط تاریخ)

```json
{
  "draftId": "42_abc123",
  "settings": {
    "startDate": "2026-06-29T00:00:00Z",
    "endDate": "2026-07-29T00:00:00Z"
  }
}
```

❌ `sendToSpecificTags` / `targetTagIds` اینجا **نیست**

**پاسخ (خلاصه کامل):**
```json
{
  "data": {
    "programTitle": "پاداش نوروز",
    "rewardType": "مبلغ ثابت",
    "referrerReward": "50,000 تومان",
    "customerReward": "10,000 تومان",
    "startDate": "1404/04/08",
    "endDate": "1404/05/08",
    "audience": "همه مخاطبین + 2 تگ",
    "contactsCount": 45
  }
}
```

- `contactsCount` = همان `totalContactsCount` از step2 (بعد از تگ)
- این API **صفحه را نمی‌بندد** — فقط تاریخ save + خلاصه

**اختیاری:** `GET /summary?draftId=42_abc123` — همان خلاصه بدون save مجدد

---

## تأیید نهایی — `POST /confirm`

```json
{
  "draftId": "42_abc123"
}
```

**پاسخ:**
```json
{
  "statusCode": 201,
  "data": {
    "program": {
      "id": 5,
      "publicCode": "REF482931"
    },
    "smsSentCount": 45,
    "smsFailedCount": 0
  }
}
```

→ **بعد از این** ویزارد تمام — برو صفحه بعد

---

## فروشگاه

### استعلام — `POST /inquire`

```json
{
  "code": "REF482931",
  "purchaseAmount": 500000
}
```

| فیلد | UI |
|------|-----|
| `isValid` | کد قابل استفاده |
| `isExpired` | منقضی |
| `isNotStarted` | هنوز شروع نشده |
| `customerDiscountAmount` | مبلغ تخفیف |

کد اشتباه → `success=true` ولی `isValid=false`

### ثبت مصرف — `POST /redeem`

```json
{
  "code": "REF482931",
  "purchaseAmount": 500000
}
```

- `Percentage` → `purchaseAmount` الزامی
- موفق → `statusCode: 201`

---

## ویرایش — `POST /{id}/update`

Partial update — body خالی → 400

```json
{
  "title": "نام جدید",
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

| statusCode | معنی |
|------------|------|
| 400 | validation — `errors[]` |
| 404 | برنامه/کد (redeem) |
| 500 | retry |

---

## Swagger

`/swagger` → `ReferralProgram`
