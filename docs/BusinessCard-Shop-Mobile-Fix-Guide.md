# BusinessCard — راهنمای فیکس موبایل (بخش فروشگاه)

> برای توسعه‌دهنده Flutter — بخش دکمه «فروشگاه» در کارت ویزیت

## خلاصه

بک‌اند فیلدهای زیر را اضافه کرده:

| فیلد | نوع | توضیح |
|------|-----|-------|
| `shopEnabled` | `bool` | فعال/غیرفعال بودن بخش فروشگاه |
| `shopUrl` | `string?` | لینک فروشگاه آنلاین (اختیاری) |
| `shopButtonLabel` | `string` | متن ثابت دکمه: **«فروشگاه»** |
| `shopNoStoreHint` | `string` | راهنمای زیر فیلد لینک در ویرایشگر |
| `shopContactPhone` | `string` | شماره تماس پشتیبانی: `02151091000` |

---

## UI ویرایشگر (صفحه بخش‌ها)

1. یک **سوئیچ** برای `shopEnabled` (مثل `bankingEnabled`)
2. اگر سوئیچ روشن بود، یک **TextField** برای `shopUrl` با placeholder مثل `https://myshop.com`
3. **زیر TextField** این متن را نمایش بده (از API بخوان، hardcode نکن):

```
shopNoStoreHint
→ «اگر فروشگاه ندارید برای ایجاد کردن فروشگاه با ما تماس بگیرید، ۰۲۱۵۱۰۹۱۰۰۰»
```

4. شماره `shopContactPhone` را **قابل کلیک** کن (`tel:02151091000`) تا کاربر بتواند تماس بگیرد

---

## UI پیش‌نمایش / کارت عمومی

- اگر `shopEnabled == true` **و** `shopUrl != null`:
  - دکمه با متن `shopButtonLabel` («فروشگاه») نمایش بده
  - کلیک → باز کردن `shopUrl` در مرورگر (`url_launcher`)
- اگر `shopEnabled == true` ولی `shopUrl` خالی است:
  - دکمه را **نمایش نده** (فقط در ویرایشگر راهنمای تماس نشان داده می‌شود)

---

## API — ذخیره

### `POST /api/BusinessCard/{id}/update-sections`

```json
{
  "shopEnabled": true,
  "shopUrl": "https://myshop.com"
}
```

- `shopUrl` اختیاری — اگر scheme نداشت، بک‌اند `https://` اضافه می‌کند
- برای **پاک کردن** لینک: `"shopUrl": ""`
- فقط فیلدهای ارسال‌شده تغییر می‌کنند

### `POST /api/BusinessCard` (ایجاد پیش‌نویس)

```json
{
  "templateKey": "shop",
  "title": "فروشگاه من",
  "shopEnabled": true,
  "shopUrl": "myshop.ir",
  "descriptionEnabled": true,
  "descriptionText": "..."
}
```

---

## نمونه Response

### `GET /api/BusinessCard/{id}`

```json
{
  "statusCode": 200,
  "success": true,
  "message": null,
  "errorCode": null,
  "data": {
    "id": 42,
    "title": "فروشگاه من",
    "templateKey": "shop",
    "status": "Draft",
    "shopEnabled": true,
    "shopUrl": "https://myshop.ir/",
    "shopButtonLabel": "فروشگاه",
    "shopNoStoreHint": "اگر فروشگاه ندارید برای ایجاد کردن فروشگاه با ما تماس بگیرید، ۰۲۱۵۱۰۹۱۰۰۰",
    "shopContactPhone": "02151091000",
    "sliderEnabled": false,
    "descriptionEnabled": true,
    "servicesEnabled": false,
    "mapEnabled": false,
    "contactEnabled": true,
    "bankingEnabled": false
  },
  "errors": null
}
```

### `GET /api/BusinessCardPublic/{slug}` (کارت عمومی)

```json
{
  "statusCode": 200,
  "success": true,
  "data": {
    "title": "فروشگاه من",
    "templateKey": "shop",
    "shopEnabled": true,
    "shopUrl": "https://myshop.ir/",
    "shopButtonLabel": "فروشگاه",
    "descriptionEnabled": true,
    "descriptionText": "..."
  }
}
```

> در API عمومی، `shopNoStoreHint` و `shopContactPhone` برنمی‌گردند — فقط در API احراز هویت‌شده (ویرایشگر).

---

## مدل Flutter — فیلدهای جدید

```dart
class BusinessCardModel {
  // ...existing fields...
  final bool shopEnabled;
  final String? shopUrl;
  final String shopButtonLabel;
  final String shopNoStoreHint;
  final String shopContactPhone;
}
```

### JSON parsing

```dart
shopEnabled: json['shopEnabled'] as bool? ?? false,
shopUrl: json['shopUrl'] as String?,
shopButtonLabel: json['shopButtonLabel'] as String? ?? 'فروشگاه',
shopNoStoreHint: json['shopNoStoreHint'] as String? ?? '',
shopContactPhone: json['shopContactPhone'] as String? ?? '02151091000',
```

### Request (update-sections)

```dart
Map<String, dynamic> toUpdateSectionsJson() => {
  // ...existing fields...
  if (shopEnabled != null) 'shopEnabled': shopEnabled,
  if (shopUrl != null) 'shopUrl': shopUrl,
};
```

---

## اعتبارسنجی

| خطا | شرط |
|-----|-----|
| `آدرس فروشگاه نامعتبر است` | URL نامعتبر |
| `آدرس فروشگاه نمی‌تواند بیشتر از 500 کاراکتر باشد` | طول بیش از حد |

---

## انتشار (Publish)

- `shopEnabled: true` به‌عنوان **یک بخش فعال** برای publish کافی است (لینk اجباری نیست)
- اگر فقط بخش فروشگاه فعال باشد و `shopUrl` خالی باشد، publish موفق است ولی دکمه در کارت عمومی نمایش داده **نمی‌شود**

---

## پیشنهاد برای قالب shop

هنگام انتخاب قالب `shop` در wizard:

```dart
shopEnabled: templateType == BusinessCardTemplateType.shop,
```

---

## چک‌لیست فیکس

- [ ] مدل response/request آپدیت شود
- [ ] صفحه ویرایش بخش‌ها: سوئیچ + TextField + hint
- [ ] `BusinessCardPreviewData`: فیلد `shopUrl` + `showShop`
- [ ] پیش‌نمایش shop: دکمه «فروشگاه» با `url_launcher`
- [ ] mapper بین API ↔ preview data
- [ ] ارسال `shopEnabled` و `shopUrl` در `update-sections`
