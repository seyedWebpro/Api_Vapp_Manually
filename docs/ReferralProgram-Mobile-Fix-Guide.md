# راهنمای فیکس موبایل — پاداش و رفرال (بعد از تغییر بک‌اند)

> برای توسعه‌دهنده Flutter (`FrontMobile_Vapp`)
> تاریخ: ۱۴۰۴/۰۵/۲۲
> Base: `/api/ReferralProgram`
> Auth: `Authorization: Bearer <jwt>`

---

## خلاصه تغییر بک‌اند (چرا موبایل باید عوض شود)

منطق قبلی اشتباه بود: **یک کد مشترک برای کل برنامه**.

منطق جدید:

1. هر مخاطب در برنامه یک **کد معرف شخصی** دارد (`REF######`)
2. فروشنده همان کد را در **استعلام** می‌زند → سیستم می‌فهمد معرف کیست
3. بعد از خرید واقعی باید **ثبت مصرف (redeem)** بزند
4. اگر پاداش معرف فعال باشد → پیامک پاداش به معرف می‌رود
5. سه حالت ساخت برنامه:
   - فقط مشتری
   - فقط معرف
   - هر دو

---

## وضعیت فعلی موبایل (بر اساس سورس فعلی) — باید فیکس شود

| فایل | مشکل |
|------|------|
| `app_api.dart` | endpointهای `redeem` و `/{id}/codes` وجود ندارند |
| `new_reward_model.dart` / `CreateRewardRequest` | فیلد `isReferrerRewardActive` ارسال نمی‌شود |
| `new_reward_controller.dart` | پاداش معرف همیشه اجباری است؛ حالت «فقط مشتری» پشتیبانی نمی‌شود |
| `referral_inquiry_model.dart` | هنوز `referrerRewardValue` می‌خواند؛ فیلدهای جدید API را ندارد |
| `identification_code_inquiry_*` | فقط inquire دارد؛ **redeem ندارد**؛ معرف را نشان نمی‌دهد |
| `program_model.dart` | `isReferrerRewardActive` و `personalCodesCount` ندارد |
| `reward_additional_info_controller.dart` | بعد از confirm پیام می‌دهد `کد: publicCode` — اشتباه است (کد شخصی مخاطبین است) |
| کل feature | صفحه/فلو لیست کدهای شخصی مخاطبین وجود ندارد |

---

## ۱) APIهای لازم

### موجود (قبلاً داشتید)
- `POST /validate-step1`
- `POST /validate-step2`
- `POST /settings/save`
- `POST /confirm`
- `POST /inquire`
- `GET /`, `GET /{id}`, `POST /{id}/update`, `toggle-status`, `delete`, `history`

### جدید / اجباری برای فیکس

#### A) لیست کدهای شخصی مخاطبین
```
GET /api/ReferralProgram/{id}/codes?pageNumber=1&pageSize=20
```

در `app_api.dart` اضافه کنید:
```dart
String getContactCodes(int id) => "/api/ReferralProgram/$id/codes";
```

#### B) ثبت مصرف کد (خیلی مهم — الان در موبایل نیست)
```
POST /api/ReferralProgram/redeem
```

در `app_api.dart`:
```dart
String get redeem => "/api/ReferralProgram/redeem";
```

---

## ۲) تغییر validate-step1 (ساخت برنامه)

### Request جدید
```json
{
  "title": "پاداش نوروز",
  "isActive": true,
  "rewardType": "FixedAmount",
  "isReferrerRewardActive": true,
  "referrerRewardValue": 50000,
  "isCustomerRewardActive": true,
  "customerRewardValue": 10000
}
```

`rewardType`: `"Percentage"` | `"FixedAmount"`

### قوانین اعتبارسنجی بک‌اند
- حداقل یکی از `isReferrerRewardActive` یا `isCustomerRewardActive` باید `true` باشد
- اگر فقط مشتری: `isReferrerRewardActive=false` و `referrerRewardValue=0`
- اگر فقط معرف: `isCustomerRewardActive=false` و `customerRewardValue=null`
- اگر Percentage و هر دو فعال: جمع درصدها نباید > 100 باشد

### Response موفق
```json
{
  "statusCode": 200,
  "success": true,
  "message": "اطلاعات مرحله 1 معتبر است",
  "data": {
    "isValid": true,
    "errors": [],
    "draftId": "2_xxxx",
    "draftExpiresAt": "2026-08-14T00:00:00Z"
  }
}
```

### فیکس UI مرحله ۱ (`new_reward_page` + controller)
- یک سوییچ/چک‌باکس برای **فعال بودن پاداش معرف** اضافه کنید (`isReferrerRewardActive`)
- سوییچ مشتری (`isCustomerRewardActive`) از قبل هست
- اگر معرف خاموش شد، فیلد مقدار معرف مخفی/غیرفعال شود
- validation فعلی که می‌گوید «مقدار پاداش معرف الزامی است» را عوض کنید:
  - فقط وقتی `isReferrerRewardActive == true` اجباری باشد
- در `CreateRewardRequest.toJson()` فیلد `isReferrerRewardActive` را بفرستید

---

## ۳) confirm — دیگر یک کد عمومی برای همه نیست

بعد از `POST /confirm`:

```json
{
  "statusCode": 201,
  "success": true,
  "message": "برنامه پاداش با موفقیت ثبت شد",
  "data": {
    "program": {
      "id": 1002,
      "title": "...",
      "isReferrerRewardActive": true,
      "isCustomerRewardActive": true,
      "publicCode": "PRG123456",
      "personalCodesCount": 2,
      "notifiedContactsCount": 2
    },
    "smsSentCount": 2,
    "smsFailedCount": 0
  }
}
```

نکات مهم:
- `publicCode` دیگر کد معرف مخاطب نیست (فقط شناسه مرجع برنامه مثل `PRG...`)
- تعداد کدهای شخصی = `personalCodesCount`
- پیام موفقیت فعلی در `reward_additional_info_controller.dart` که می‌گوید:
  `کد: ${response.program.publicCode}`
  را عوض کنید به چیزی مثل:
  `برنامه ثبت شد — N کد معرف شخصی برای مخاطبین ساخته شد`

برای دیدن کدها:
```
GET /api/ReferralProgram/{programId}/codes
```

نمونه response:
```json
{
  "success": true,
  "data": {
    "codes": [
      {
        "id": 1,
        "referralProgramId": 1002,
        "contactId": 5,
        "contactName": "معرف علی",
        "contactMobile": "09121110001",
        "code": "REF956696",
        "createdAt": "2026-08-13T00:00:00Z"
      }
    ],
    "totalCount": 2,
    "pageNumber": 1,
    "pageSize": 20,
    "totalPages": 1
  }
}
```

پیشنهاد UI: در جزئیات برنامه / کارت برنامه، دکمه «کدهای معرف مخاطبین» → لیست بالا.

---

## ۴) استعلام کد (inquire) — تغییرات ریسپانس

### Request
```json
POST /api/ReferralProgram/inquire
{
  "code": "REF956696",
  "purchaseAmount": 200000
}
```

- `code`: کد شخصی معرف (نه `PRG...`)
- `purchaseAmount`: برای محاسبه درصدی توصیه/لازم است (۱ تا ۱۰۰٬۰۰۰٬۰۰۰)

### Response وقتی معتبر است
```json
{
  "statusCode": 200,
  "success": true,
  "message": "کد معتبر است",
  "data": {
    "isValid": true,
    "isExpired": false,
    "isNotStarted": false,
    "isActive": true,
    "invalidReason": null,
    "programId": 1002,
    "programName": "پاداش نوروز",
    "publicCode": "REF956696",
    "rewardType": "FixedAmount",
    "isCustomerRewardActive": true,
    "customerDiscountAmount": 10000,
    "formattedCustomerDiscount": "10,000 تومان",
    "isReferrerRewardActive": true,
    "referrerRewardAmount": 50000,
    "formattedReferrerReward": "50,000 تومان",
    "referrerContactId": 5,
    "referrerContactName": "معرف علی",
    "referrerContactMobile": null,
    "startDate": "...",
    "endDate": "..."
  }
}
```

### Breaking change برای مدل فعلی موبایل
الان در `referral_inquiry_model.dart` دارید:
```dart
referrerRewardValue: (json['referrerRewardValue'] ?? 0).toDouble(),
```

بک‌اند دیگر `referrerRewardValue` برنمی‌گرداند.
باید به این‌ها تغییر کند:

```dart
final bool isReferrerRewardActive;
final double? referrerRewardAmount;
final String? formattedReferrerReward;
final int? referrerContactId;
final String? referrerContactName;
// referrerContactMobile عمداً معمولاً null است (امنیت)
```

از JSON:
- `isReferrerRewardActive`
- `referrerRewardAmount`  ← جایگزین `referrerRewardValue`
- `formattedReferrerReward`
- `referrerContactId`
- `referrerContactName`

### Response وقتی نامعتبر/منقضی
فقط فلگ‌ها برمی‌گردد؛ جزئیات برنامه و معرف **لو نمی‌رود**:
```json
{
  "success": true,
  "message": "کد منقضی شده است",
  "data": {
    "isValid": false,
    "isExpired": true,
    "isNotStarted": false,
    "isActive": true,
    "invalidReason": "کد منقضی شده است"
  }
}
```

### فیکس UI استعلام
در `_InquiryResultCard`:
- نام معرف را نشان بده (`referrerContactName`)
- پاداش معرف را فقط اگر `isReferrerRewardActive == true` نشان بده
- تخفیف مشتری را فقط اگر `isCustomerRewardActive == true` نشان بده
- بعد از استعلام موفق، دکمه **«ثبت مصرف / ثبت خرید»** بگذار → می‌رود به redeem

---

## ۵) ثبت مصرف (redeem) — فلو فروشگاه (الزامی)

بدون redeem هیچ پاداشی ثبت نمی‌شود و پیامک معرف نمی‌رود.

### Request
```json
POST /api/ReferralProgram/redeem
{
  "code": "REF956696",
  "purchaseAmount": 200000,
  "customerContactId": 6,
  "idempotencyKey": "receipt-2026-08-13-001",
  "description": "خرید حضوری"
}
```

### فیلدهای اجباری
| فیلد | اجباری؟ | توضیح |
|------|---------|--------|
| `code` | بله | کد شخصی معرف |
| `purchaseAmount` | بله | ۱ تا ۱۰۰٬۰۰۰٬۰۰۰ |
| `customerContactId` | بله | مخاطب خریدار (نه معرف) |
| `idempotencyKey` | خیلی توصیه‌شده | کلید یکتای رسید؛ جلوی ثبت تکراری را می‌گیرد |
| `referrerContactId` | نه | نادیده گرفته می‌شود؛ معرف از روی کد مشخص می‌شود |

### Response موفق (201)
```json
{
  "statusCode": 201,
  "success": true,
  "message": "مصرف کد با موفقیت ثبت شد",
  "data": {
    "usageId": 10,
    "programId": 1002,
    "programName": "پاداش نوروز",
    "publicCode": "REF956696",
    "purchaseAmount": 200000,
    "customerDiscountAmount": 10000,
    "formattedCustomerDiscount": "10,000 تومان",
    "referrerRewardAmount": 50000,
    "formattedReferrerReward": "50,000 تومان",
    "referrerContactId": 5,
    "referrerContactName": "معرف علی",
    "customerRewardCredited": true,
    "referrerRewardCredited": true,
    "referrerRewardSmsSent": true
  }
}
```

### خطاهای مهم که UI باید هندل کند
| وضعیت | پیام تقریبی |
|-------|-------------|
| 400 | استفاده از کد معرف خود خریدار مجاز نیست |
| 400 | این کد امروز برای این خریدار قبلاً ثبت شده است |
| 400 | این تراکنش قبلاً ثبت شده است (`idempotencyKey` تکراری) |
| 400 | سقف مجاز مصرف این کد در امروز تکمیل شده است |
| 400 | مبلغ خرید الزامی / نامعتبر |
| 400 | شناسه مخاطب خریدار الزامی است |
| 404 | کد یافت نشد |

### پیشنهاد UI فلو فروشگاه
1. صفحه استعلام (فعلی)
2. اگر `isValid==true` → فرم تکمیل:
   - انتخاب مخاطب خریدار (`customerContactId`) از دفترچه
   - مبلغ خرید (از قبل وارد شده)
   - دکمه «ثبت مصرف»
3. نتیجه:
   - تخفیف مشتری: `formattedCustomerDiscount`
   - پاداش معرف: `formattedReferrerReward`
   - وضعیت پیامک: `referrerRewardSmsSent`

`idempotencyKey` پیشنهاد:  
`"${programId}-${code}-${customerContactId}-${purchaseAmount}-${timestamp}"`  
یا شماره فاکتور واقعی فروشگاه.

---

## ۶) ProgramModel — فیلدهای جدید

در `program_model.dart` اضافه کنید:
```dart
final bool isReferrerRewardActive;
final int personalCodesCount;
```

از JSON:
```dart
isReferrerRewardActive: json['isReferrerRewardActive'] ?? true,
personalCodesCount: json['personalCodesCount'] ?? 0,
```

در کارت لیست:
- اگر معرف غیرفعال است، به‌جای عدد بگذارید «غیرفعال»
- `personalCodesCount` را نشان دهید (مثلاً «۳ کد معرف»)

در `UpdateRewardRequest` هم `isReferrerRewardActive` را پشتیبانی کنید.

---

## ۷) چک‌لیست فیکس موبایل

### مدل‌ها / API
- [ ] `CreateRewardRequest` → `isReferrerRewardActive`
- [ ] `ProgramModel` → `isReferrerRewardActive`, `personalCodesCount`
- [ ] `ReferralInquiryModel` → فیلدهای جدید inquire (`referrerRewardAmount` و ...)
- [ ] مدل جدید `ReferralContactCode` + list
- [ ] مدل جدید `RedeemRequest` / `RedeemResponse`
- [ ] endpointهای `codes` و `redeem` در `app_api.dart`

### UI/Logic
- [ ] مرحله ۱: سوییچ پاداش معرف + حالت فقط‌مشتری / فقط‌معرف / هر دو
- [ ] validation مرحله ۱ مطابق قوانین بک‌اند
- [ ] بعد از confirm: پیام درست (نه نمایش `publicCode` به‌عنوان کد معرف)
- [ ] صفحه/باتم‌شیت لیست کدهای شخصی مخاطبین
- [ ] استعلام: نمایش نام معرف + مبالغ درست
- [ ] بعد از استعلام معتبر: فلو redeem با انتخاب خریدار
- [ ] هندل خطاهای امنیتی (خودمعرفی، تکراری روزانه، idempotency)

### تست دستی
1. ساخت برنامه با هر دو پاداش → confirm → GET codes → ۲ کد متفاوت
2. inquire با کد شخصی → معرف درست + مبالغ درست
3. redeem با خریدار متفاوت → ۲۰۱ + پاداش‌ها
4. redeem دوباره همان خریدار همان روز → ۴۰۰
5. redeem با `customerContactId == referrer` → ۴۰۰
6. برنامه فقط‌مشتری → redeem بدون پیامک معرف
7. برنامه فقط‌معرف → تخفیف مشتری ۰ + پیامک معرف

---

## ۸) نکته واحد پول
در UI استعلام نوشته شده «مبلغ خرید (ریال)»، ولی بک‌اند و پیام‌ها بر حسب **تومان** هستند.  
با محصول هماهنگ کنید و لیبل را درست کنید تا مبلغ اشتباه ۱۰ برابر نشود.

---

## ۹) فایل‌های موبایل که احتمالاً باید تغییر کنند

```
lib/app_config/app_constants/app_string/app_api.dart
lib/app_feature/feature_rewards_and_referrals/models/new_reward_model.dart
lib/app_feature/feature_rewards_and_referrals/models/program_model.dart
lib/app_feature/feature_rewards_and_referrals/models/referral_inquiry_model.dart
lib/app_feature/feature_rewards_and_referrals/data/data_source/referral_program_remote_data_source.dart
lib/app_feature/feature_rewards_and_referrals/data/repository/referral_program_repository.dart
lib/app_feature/feature_rewards_and_referrals/controllers/new_reward_controller.dart
lib/app_feature/feature_rewards_and_referrals/controllers/identification_code_inquiry_controller.dart
lib/app_feature/feature_rewards_and_referrals/controllers/reward_additional_info_controller.dart
lib/app_feature/feature_rewards_and_referrals/views/new_reward_page.dart
lib/app_feature/feature_rewards_and_referrals/views/identification_code_inquiry_page.dart
lib/app_feature/feature_rewards_and_referrals/widgets/rewards_and_referrals_card_widget.dart
+ صفحه/فلو جدید redeem و لیست codes
```

اگر سوالی از شکل دقیق UI بود، از بک‌اند همین قرارداد را مبنا بگیرید؛ رفتار فروشگاه بدون `redeem` کامل نیست.
