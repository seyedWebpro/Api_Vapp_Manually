# تأیید ادمین برای پیام و قالب — راهنمای موبایل (Flutter)

> برای برنامه‌نویس `Front_Vapp`. بک‌اند صف تأیید را اجباری کرده است. موبایل باید وضعیت را درست نشان دهد (نه اینکه همیشه «ارسال شد» بگوید).

## پیش‌نیاز

```
Base URL (Message):   /api/Message
Base URL (Template):  /api/Message/templates  (طبق endpointهای موجود قالب)
Auth:                 Authorization: Bearer <jwt>
Content:              application/json
```

---

## شکل پاسخ استاندارد

```json
{
  "statusCode": 202,
  "success": true,
  "message": "درخواست ارسال در صف تأیید ادمین قرار گرفت",
  "errorCode": null,
  "data": { },
  "errors": null,
  "traceId": "..."
}
```

| فیلد | کاربرد UI |
|------|-----------|
| `success=true` + `statusCode=202` | **ارسال واقعی نشده** — فقط رفته صف ادمین |
| `success=true` + `statusCode=200` | عملیات کامل شد (مثلاً ساخت پیام / قالب) |
| `success=false` | خطا — `message` را نشان بده |
| `message` | متن کاربرپسند (فارسی) |

**قانون حیاتی:** اگر `statusCode == 202` بود، toast را **«در انتظار تأیید ادمین»** بگذار — نه «پیام با موفقیت ارسال شد».

---

## ۱) قالب پیام (`MessageTemplate`)

### رفتار بک‌اند

| رویداد | `approvalStatus` |
|--------|------------------|
| ساخت قالب جدید توسط کاربر | `Pending` |
| قالب پیش‌فرض سیستم (`isDefault=true`) | از اول `Approved` |
| ویرایش **نام یا محتوا** | دوباره `Pending` |
| تأیید ادمین | `Approved` |
| رد ادمین | `Rejected` (+ `rejectionReason`) |

بعد از یک‌بار تأیید، تا وقتی ویرایش نشود دوباره نیاز به تأیید ندارد.

### فیلدهای پاسخ قالب (از قبل در API هست)

```json
{
  "id": 12,
  "name": "...",
  "content": "...",
  "isDefault": false,
  "approvalStatus": "Pending",
  "rejectionReason": null,
  "approvedAt": null,
  "createdAt": "..."
}
```

مقادیر `approvalStatus`: `Pending` | `Approved` | `Rejected`

### کار موبایل

1. مدل `MessageTemplateModel` را گسترش بده:
   - `approvalStatus` (String)
   - `rejectionReason` (String?)
   - `approvedAt` (String?)
2. در لیست / انتخاب قالب:
   - `Pending` → badge «در انتظار تأیید» — **انتخاب برای ارسال ممنوع** (یا با توضیح غیرفعال)
   - `Rejected` → badge «رد شده» + نمایش `rejectionReason`
   - `Approved` → قابل استفاده
3. بعد از ساخت/ویرایش قالب: اگر `approvalStatus == Pending` بگو «قالب برای تأیید ادمین ارسال شد».

ارسال پیام با `templateId` وقتی قالب تأیید نشده → API خطای ۴۰۰:

`قالب پیام هنوز توسط ادمین تأیید نشده است`

---

## ۲) ارسال پیام (کمپین / مستقیم / quick-send)

### مسیرهای رایج موبایل

| UI | API تقریبی |
|----|------------|
| خلاصه و ارسال | `POST /api/Message/campaign/calculate-summary` |
| quick send | `POST /api/Message/quick-send` |
| تأیید کمپین | `POST /api/Message/campaign/{id}/confirm-and-send` |
| QuickAction | `.../quick-send` |

همهٔ این‌ها **بدون ارسال فوری** درخواست `SmsApprovalRequest` می‌سازند و معمولاً پاسخ این‌شکلی برمی‌گردد:

```json
{
  "statusCode": 202,
  "success": true,
  "message": "درخواست ارسال در صف تأیید ادمین قرار گرفت"
}
```

بعد از Approve در پنل ادمین، SMS واقعاً ارسال می‌شود.

### کار موبایل (اجباری برای UX)

در کنترلرهایی مثل:

- `summary_and_setting_controller.dart`
- `quick_send_message_controller.dart`

الان بعد از موفق بودن همیشه «پیام با موفقیت ارسال شد» نشان داده می‌شود. اصلاح:

```dart
// شبه‌کد
if (response.success) {
  final code = response.statusCode; // یا از body: statusCode
  if (code == 202) {
    showToastSuccess(message: response.message ?? 'درخواست در صف تأیید ادمین قرار گرفت');
  } else {
    showToastSuccess(message: response.message ?? 'پیام با موفقیت ارسال شد');
  }
}
```

اگر لایهٔ Dio فقط `data` را برمی‌گرداند و `statusCode` بدنه را دور می‌ریزد، آن را از `BaseResponse.statusCode` حفظ کن.

---

## ۳) پیام خودکار (Birthday / SpecialOccasion / …)

بک‌اند دیگر SMS را مستقیم نمی‌فرستد.

جریان:

```
زمان اجرا (background)
  → ساخت کمپین با Status=PendingApproval
  → ثبت در صف تأیید ادمین (همان صفحه message-approvals)
  → فقط بعد از Approve ادمین ارسال می‌شود
```

### کار موبایل (پیشنهادی)

- در تنظیمات پیام خودکار متن راهنما بگذار:  
  «ارسال خودکار پس از تأیید ادمین انجام می‌شود.»
- اگر وضعیت اجرا/`AutomationExecution` جایی نشان می‌دهی، وضعیت `PendingApproval` را معنی کن: «در انتظار تأیید ادمین».

نیازی به endpoint جدید موبایل نیست؛ همان UI ادمین صف را می‌بیند.

---

## ۴) چیزهایی که موبایل نباید با API خام بزند

این endpointها فقط برای **Admin** باز هستند (دور زدن صف تأیید بسته شده):

- `POST /api/Sms/send`
- `POST /api/Sms/send-bulk`
- `POST /api/Sms/send-array`

اپ موبایل از مسیر `Message` / `QuickAction` استفاده کند.

---

## چک‌لیست پیاده‌سازی موبایل

- [ ] parse کردن `approvalStatus` / `rejectionReason` در مدل قالب
- [ ] UI وضعیت قالب Pending / Rejected / Approved
- [ ] بلاک انتخاب قالب تأییدنشده هنگام ارسال
- [ ] تشخیص `statusCode == 202` روی ارسال و پیام «صف تأیید»
- [ ] حذف toast اشتباه «ارسال شد» وقتی فقط صف شده
- [ ] (اختیاری) متن راهنما در صفحه پیام خودکار

---

## نمونه پیام‌های بک‌اند

| وضعیت | نمونه `message` |
|-------|-----------------|
| صف تأیید ارسال | `درخواست ارسال در صف تأیید ادمین قرار گرفت` |
| قبلاً صف شده | `درخواست ارسال در صف تأیید ادمین قرار دارد` |
| قالب تأیید نشده | `قالب پیام هنوز توسط ادمین تأیید نشده است` |

---

## خلاصه برای برنامه‌نویس

1. **امنیت در بک‌اند است** — موبایل نمی‌تواند تأیید را دور بزند اگر فقط APIهای Message را بزند.
2. **UX موبایل لازم است** تا کاربر فکر نکند پیام ارسال شده وقتی هنوز Pending است.
3. قالب = یک‌بار تأیید؛ ویرایش = دوباره Pending.
4. هر بار ارسال پیام محتوایی = یک درخواست در صف ادمین (حتی اگر از قالب Approved استفاده شود؛ متن نهایی بازبینی می‌شود).
