# BankAccount — راهنمای اتصال فرانت (موبایل)

Base: `/api/BankAccount`  
Auth: `Authorization: Bearer <jwt>`  
Content-Type: `application/json`  
Feature اشتراک: `free_quick_send`

همه پاسخ‌ها در قالب `ApiResponse`:

```json
{
  "statusCode": 200,
  "success": true,
  "message": "...",
  "errorCode": null,
  "data": {},
  "errors": null,
  "traceId": "..."
}
```

فقط `message` (+ در صورت وجود `errors`) را به کاربر نشان بده. `errorCode` برای منطق UI (مثل `TOKEN_EXPIRED`).

---

## فیکس موبایل — «توضیحات ارسال»

| UI | مقدار |
|----|--------|
| Label | `توضیحات ارسال (اختیاری)` |
| Placeholder نمونه | `مثلاً: برای واریز به شماره کارت زیر اقدام کنید` |

کلید JSON: **`smsDescription`** (نه `smsCaption`)

| ماژول | Create | Update |
|-------|--------|--------|
| شماره حساب / کارت / شبا | `POST /api/BankAccount` | `POST /api/BankAccount/{id}/update` |

قوانین:
- اختیاری
- حداکثر ۱۰۰ کاراکتر
- `""` = حذف توضیحات
- `null` در update = بدون تغییر
- ≠ `title` (عنوان نمایشی در لیست اپ)
- تغییر `smsDescription` تأیید ادمین ارسال سریع را به `Pending` برمی‌گرداند

---

## Endpoints

| کار | Method + Path |
|-----|----------------|
| لیست | `GET /api/BankAccount?pageNumber=1&pageSize=10` |
| جزئیات | `GET /api/BankAccount/{id}` |
| ایجاد | `POST /api/BankAccount` |
| ویرایش | `POST /api/BankAccount/{id}/update` |
| حذف | `POST /api/BankAccount/{id}/delete` |
| پیش‌فرض | `POST /api/BankAccount/{id}/set-default` |
| ارسال سریع SMS | `POST /api/BankAccount/quick-send` |

`pageSize` حداکثر ۱۰۰.

---

## Bodyها

### Create
```json
{
  "title": "حساب ملت",
  "smsDescription": "برای واریز به شماره کارت زیر اقدام کنید",
  "accountNumber": "1234567890",
  "cardNumber": "6037991234567890",
  "shebaNumber": "IR120170000000123456789001",
  "isDefault": true
}
```

- `title` الزامی (حداکثر ۱۰۰) — برچسب نمایشی در لیست
- حداقل یکی از `accountNumber` / `cardNumber` / `shebaNumber` الزامی
- `smsDescription` اختیاری (حداکثر ۱۰۰)
- اگر اولین حساب فعال باشد یا `isDefault=true` → پیش‌فرض می‌شود
- بعد از ایجاد: `approvalStatus = "Pending"` تا تأیید ادمین

### Update (partial)
```json
{
  "smsDescription": "لطفاً مبلغ را به کارت زیر واریز کنید"
}
```

یا حذف توضیحات:
```json
{
  "smsDescription": ""
}
```

فیلدهای قابل ارسال: `title`, `smsDescription`, `accountNumber`, `cardNumber`, `shebaNumber`, `isActive`  
فقط فیلدهای ارسال‌شده تغییر می‌کنند (`null` = بدون تغییر).

### Quick-send
```json
{
  "contactId": 2,
  "bankAccountId": 12
}
```

---

## Response نمونه — Create / Update / Get

```json
{
  "statusCode": 201,
  "success": true,
  "message": "شماره حساب با موفقیت ایجاد شد",
  "data": {
    "id": 12,
    "title": "حساب ملت",
    "smsDescription": "برای واریز به شماره کارت زیر اقدام کنید",
    "accountNumber": "1234567890",
    "cardNumber": "6037991234567890",
    "shebaNumber": "IR120170000000123456789001",
    "isActive": true,
    "isDefault": true,
    "createdAt": "2026-08-29T18:30:00.000Z",
    "approvalStatus": "Pending",
    "rejectionReason": null,
    "approvedAt": null
  }
}
```

### لیست
```json
{
  "statusCode": 200,
  "success": true,
  "message": "عملیات با موفقیت انجام شد",
  "data": {
    "bankAccounts": [
      {
        "id": 12,
        "title": "حساب ملت",
        "smsDescription": "برای واریز به شماره کارت زیر اقدام کنید",
        "accountNumber": "1234567890",
        "cardNumber": "6037991234567890",
        "shebaNumber": "IR120170000000123456789001",
        "isActive": true,
        "isDefault": true,
        "createdAt": "2026-08-29T18:30:00.000Z",
        "approvalStatus": "Approved",
        "rejectionReason": null,
        "approvedAt": "2026-08-29T19:00:00.000Z"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

---

## متن SMS ارسالی

اگر `smsDescription` پر باشد:
```
برای واریز به شماره کارت زیر اقدام کنید
حساب ملت
شماره حساب: 1234567890
شماره کارت: 6037991234567890
شماره شبا: IR120170000000123456789001
```

اگر `smsDescription` خالی/null باشد (مثل قبل):
```
حساب ملت
شماره حساب: 1234567890
شماره کارت: 6037991234567890
شماره شبا: IR120170000000123456789001
```

فقط فیلدهای پرشده (حساب / کارت / شبا) در SMS می‌آیند.

---

## مدل Flutter

```dart
final String title;
final String? smsDescription; // json['smsDescription']
final String? accountNumber;
final String? cardNumber;
final String? shebaNumber;
final String approvalStatus; // Pending | Approved | Rejected
```

صفحات: create + edit شماره حساب — parse + send + TextField برای `smsDescription`.

---

## تأیید ادمین (ارسال سریع)

| وضعیت | معنی UI |
|-------|---------|
| `Pending` | در انتظار تأیید — quick-send ممکن است `202` برگرداند |
| `Approved` | ارسال فوری مجاز |
| `Rejected` | نمایش `rejectionReason` |

ویرایش `title` / `smsDescription` / شماره‌ها → دوباره `Pending`.  
فقط تغییر `isActive` تأیید را باطل نمی‌کند.

اگر quick-send با `statusCode == 202` برگشت → toast متن `message` بک‌اند.

---

## خطاهای رایج

| HTTP | errorCode | معنی |
|------|-----------|------|
| 400 | `VALIDATION_FAILED` / `INVALID_INPUT` | بدنه نامعتبر / هیچ شماره‌ای نیست |
| 401 | `UNAUTHORIZED` | بدون توکن |
| 403 | `FORBIDDEN` | اشتراک `free_quick_send` ندارد / مخاطب مال شما نیست |
| 404 | `NOT_FOUND` | حساب/مخاطب نیست |
| 202 | — | آیتم هنوز تأیید نشده (صف تأیید یک‌باره) |
