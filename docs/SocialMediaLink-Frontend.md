# SocialMediaLink — راهنمای اتصال فرانت (موبایل)

Base: `/api/SocialMediaLink`  
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

## Endpoints

| کار | Method + Path |
|-----|----------------|
| لیست | `GET /api/SocialMediaLink?pageNumber=1&pageSize=10` |
| جزئیات | `GET /api/SocialMediaLink/{id}` |
| ایجاد | `POST /api/SocialMediaLink` |
| ویرایش | `POST /api/SocialMediaLink/{id}/update` |
| حذف | `POST /api/SocialMediaLink/{id}/delete` |
| پیش‌فرض | `POST /api/SocialMediaLink/{id}/set-default` |
| ارسال سریع SMS | `POST /api/SocialMediaLink/quick-send` |

`pageSize` حداکثر ۱۰۰.

---

## Bodyها

### Create
```json
{
  "platform": "Instagram",
  "linkUrl": "https://instagram.com/yourpage",
  "isDefault": true
}
```
- `platform` و `linkUrl` الزامی
- `linkUrl` باید `http`/`https` معتبر باشد
- اگر اولین لینک فعال باشد یا `isDefault=true` → پیش‌فرض می‌شود

### Update (partial)
```json
{
  "platform": "Telegram",
  "linkUrl": "https://t.me/yourpage",
  "isActive": true
}
```

### Quick-send
```json
{
  "contactId": 2,
  "linkId": 10
}
```

---

## نمونه پاسخ لیست

```json
{
  "statusCode": 200,
  "success": true,
  "message": "عملیات با موفقیت انجام شد",
  "data": {
    "socialMediaLinks": [
      {
        "id": 2,
        "platform": "WhatsApp",
        "linkUrl": "https://wa.me/989120000000",
        "isActive": true,
        "isDefault": true,
        "createdAt": "2026-07-31T14:18:48.828698Z",
        "linkType": "WhatsApp"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1
  }
}
```

`linkType` از روی URL تشخیص داده می‌شود (Instagram/Telegram/WhatsApp/Rubika/...).

---

## خطاهای رایج

| HTTP | errorCode | معنی |
|------|-----------|------|
| 400 | `VALIDATION_FAILED` / `INVALID_INPUT` | بدنه نامعتبر |
| 401 | `UNAUTHORIZED` | بدون توکن |
| 403 | `FORBIDDEN` | اشتراک `free_quick_send` ندارد / مخاطب مال شما نیست |
| 404 | `NOT_FOUND` | لینک/مخاطب نیست |

Quick-send ممکن است `202` برگرداند اگر ارسال نیاز به تأیید ادمین داشته باشد (`success=true`).
