# اعلان درون‌برنامه‌ای (زنگوله) — تأیید قالب و پیام

بک‌اند هنگام **تأیید/رد قالب** و **تأیید/رد پیام (SMS)** هم ردیف inbox می‌سازد و هم Push می‌فرستد.

## Endpointهای موبایل

Base: `/api/Notifications` — `Authorization: Bearer <jwt>`

| Method | Path | کاربرد |
|--------|------|--------|
| GET | `/api/Notifications?page&pageSize&isRead&type` | لیست زنگوله |
| GET | `/api/Notifications/unread-count` | badge |
| POST | `/api/Notifications/mark-read` | body: `{ "notificationId": 1 }` |
| POST | `/api/Notifications/mark-all-read` | همه خوانده |
| POST | `/api/Notifications/delete/{id}` | حذف نرم |

## انواع (`type`)

| type | معنی |
|------|------|
| `TemplateApproved` | قالب تأیید شد |
| `TemplateRejected` | قالب رد شد (+ دلیل در `body` و `metadata`) |
| `MessageApproved` | پیام تأیید/ارسال شد |
| `MessageRejected` | پیام رد شد (+ دلیل) |

## شکل آیتم

```json
{
  "id": 1,
  "title": "قالب تأیید شد",
  "body": "«نام قالب» تأیید شد و می‌توانید از آن برای ارسال پیامک استفاده کنید.",
  "type": "TemplateApproved",
  "category": "Suggestions",
  "isRead": false,
  "readAt": null,
  "actionUrl": "/sms/templates",
  "relatedEntityId": 12,
  "relatedEntityType": "MessageTemplate",
  "metadata": "{\"decision\":\"Approved\",\"templateName\":\"...\"}",
  "createdAt": "..."
}
```

برای رد: `metadata.rejectionReason` و متن `body` شامل دلیل است.

`actionUrl` پیشنهادی deep-link:

- قالب‌ها → `/sms/templates`
- پیام‌ها → `/sms/reports`
