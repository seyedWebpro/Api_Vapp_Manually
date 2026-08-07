# محدودیت امکانات اشتراک (Feature Gating)

بکند با `[RequireSubscriptionFeature]` روی API ماژول‌ها دسترسی را قفل می‌کند.
امکانات واقعی هر پلن را ادمین در پنل تعیین می‌کند (`SubscriptionPlanFeatures`).

## رفتار

| وضعیت کاربر | منبع امکانات |
|-------------|--------------|
| اشتراک Active منقضی‌نشده | فقط امکانات همان پلن (لینک پلن) |
| بدون اشتراک پولی | امکانات پلن `free` |

`IsActive` روی Feature فقط برای **تخصیص جدید به پلن** است؛ دسترسی کاربران فعلی بر اساس عضویت امکان در پلن قطع نمی‌شود.

پاسخ رد دسترسی:

```json
{
  "statusCode": 403,
  "success": false,
  "message": "این امکان در اشتراک فعال شما موجود نیست",
  "errorCode": "FORBIDDEN"
}
```

Endpointهای `[AllowAnonymous]` (صفحات عمومی فرم/رزرو/کارت ویزیت) چک نمی‌شوند.

## ارتقا / تعویض پلن

اشتراک قبلی Cancel می‌شود؛ اشتراک جدید با:
`مدت پلن جدید + روزهای باقی‌مانده اشتراک قبلی`

## ویرایش پلن در ادمین

| تغییر | اثر روی کاربران فعلی |
|-------|----------------------|
| امکانات پلن | فوری (چون entitlement از پلن زنده می‌خواند) |
| قیمت | فقط خریدهای جدید |
| مدت | فقط فعال‌سازی‌های جدید |
| غیرفعال کردن پلن | توقف فروش؛ اشتراک جاری قطع نمی‌شود |

## نگاشت ماژول → featureCode

| ماژول / Controller | Feature |
|--------------------|---------|
| NumberSeeker | `number_seeker` |
| Contact / ContactNotebook | `phonebook` |
| Message (پایه) / Template | `messaging` |
| Message `quick-send` / QuickAction | `free_quick_send` |
| SocialMediaLink / BusinessCard `quick-send` | `free_quick_send` (+ ماژول مربوطه) |
| Message `campaign/*` | `bulk_campaign` |
| AutomatedMessage / SpecialOccasion | `message_automation` |
| UserForm | `form_builder` |
| BookingSystem | `online_booking` |
| BusinessCard | `business_card` |
| Cashback | `cashback_wallet` |
| SmsDeliveryReport / Message `report/comprehensive` | `advanced_analytics` |

بدون قفل (عمدی): Auth، Wallet، Payment، UserSubscription، SupportTicket، EducationalVideo، Referral، LuckyWheel، Public controllers.
