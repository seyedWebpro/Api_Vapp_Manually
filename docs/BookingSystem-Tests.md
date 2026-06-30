# BookingSystem — راهنمای تست endpoint (فرانت / Swagger)

> اجرای تست‌های خودکار: `dotnet test Tests/Api_Vapp.Tests.csproj --filter "FullyQualifiedName~BookingSystem"`

## پوشش تست‌ها

| فایل | نقش |
|------|-----|
| `BookingModuleEndpointTests.cs` | **پوشش کامل endpoint** — happy path + 400/404 + smoke |
| `BookingSystemServiceTests.cs` | ویزارد و CRUD سیستم |
| `BookingAppointmentServiceTests.cs` | رزرو عمومی و نوبت‌ها |
| `BookingApiAssertions.cs` | بدون 500، پیام فارسی کنترل‌شده |

## Endpoint → سناریوهای تست‌شده

### `/api/BookingSystem` (Auth)

| Endpoint | 200/201 | 400 | 404 |
|----------|---------|-----|-----|
| GET `/activity-types` | ✅ | — | — |
| GET `/notebooks` | ✅ | — | — |
| GET `/` | ✅ | — | — |
| GET `/{id}` | ✅ | — | ✅ (ناموجود / کاربر دیگر) |
| POST `/validate-step1` | ✅ | ✅ عنوان/نوع/slug | — |
| POST `/validate-step2` | ✅ | ✅ بدون خدمت | — |
| POST `/validate-step3` | ✅ | ✅ schedule ناقص | — |
| POST `/validate-step4` | ✅ | ✅ settings ناقص | — |
| GET `/summary` | ✅ | — | — |
| POST `/confirm` | ✅ 201 | ✅ draft نامعتبر | — |
| POST `/{id}/update` | ✅ | ✅ body خالی | ✅ |
| POST `/{id}/toggle-status` | ✅ | — | — |
| POST `/{id}/delete` | ✅ | — | — |
| GET `/{id}/services` | ✅ | — | — |
| POST `/{id}/services/add` | ✅ 201 | — | — |
| POST `/{id}/services/{id}/update` | ✅ | — | — |
| POST `/{id}/services/{id}/schedule/save` | ✅ | — | — |
| POST `/{id}/services/{id}/exceptions/add` | ✅ 201 | — | — |
| POST `/{id}/services/{id}/exceptions/{id}/delete` | ✅ | — | — |
| POST `/{id}/services/{id}/delete` | ✅ | — | — |
| GET `/{id}/appointments` | ✅ | — | ✅ کاربر دیگر |
| POST `/{id}/appointments/{id}/cancel` | ✅ | ✅ لغو تکراری | ✅ |

### `/api/BookingPublic` (بدون Auth)

| Endpoint | 200/201 | 400 | 404 |
|----------|---------|-----|-----|
| GET `/{slug}` | ✅ | — | ✅ slug نامعتبر / غیرفعال |
| GET `/{slug}/services/{id}/slots` | ✅ | ✅ تاریخ گذشته | ✅ خدمت نامعتبر |
| POST `/{slug}/book` | ✅ 201 | ✅ موبایل/slot تکراری | ✅ |

## تضمین‌ها

- هیچ تستی **500** یا پیام `ControlledErrorHelper.Unexpected` در سناریوی عادی نمی‌گیرد
- خطاهای 400/404 پیام **فارسی کنترل‌شده** دارند (بدون exception/stack/sql)
- تست `SmokeTest_AllPrimaryEndpoints_No500` همه endpointهای اصلی را یکجا چک می‌کند

## اجرای دستی (Swagger / curl)

نمونه‌ها در `Api_Vapp.http` بخش BookingSystem.

```bash
dotnet test Tests/Api_Vapp.Tests.csproj --filter "FullyQualifiedName~BookingModuleEndpointTests"
```
