using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services.ZarinPal
{
    /// <summary>
    /// کلاینت رسمی زرین‌پال — REST API v4
    /// مرجع: https://www.zarinpal.com/docs/paymentGateway/connectToGateway.html
    /// خطاها: https://www.zarinpal.com/docs/paymentGateway/errorList.html
    ///
    /// جریان:
    /// 1) POST /pg/v4/payment/request.json → authority + code=100
    /// 2) Redirect کاربر به https://payment.zarinpal.com/pg/StartPay/{authority}
    /// 3) Callback با QueryString Authority و Status (OK|NOK)
    /// 4) فقط اگر Status=OK → POST /pg/v4/payment/verify.json
    ///    code 100 = اولین verify موفق | 101 = قبلاً verify شده (موفق idempotent)
    /// </summary>
    public sealed class ZarinPalGatewayClient : IZarinPalGatewayClient
    {
        /// <summary>حداقل مبلغ مجاز (تومان) — مطابق محدودیت رایج درگاه</summary>
        public const int MinAmountToman = 1000;

        /// <summary>حداکثر مبلغ — خطای -41 زرین‌پال: ۱۰۰ میلیون تومان</summary>
        public const int MaxAmountToman = 100_000_000;

        /// <summary>حد description در request — خطای -9</summary>
        public const int MaxDescriptionLength = 500;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private readonly ZarinPalOptions _options;
        private readonly ILogger<ZarinPalGatewayClient> _logger;

        public ZarinPalGatewayClient(
            HttpClient httpClient,
            IOptions<ZarinPalOptions> options,
            ILogger<ZarinPalGatewayClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ZarinPalRequestResult> RequestPaymentAsync(
            int amountToman,
            string description,
            string callbackUrl,
            string? mobile = null,
            string? email = null,
            string? orderId = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.MerchantId) || _options.MerchantId.Trim().Length != 36)
            {
                _logger.LogError("ZarinPal MerchantId missing or not 36 chars");
                return FailRequest("تنظیمات درگاه پرداخت ناقص است", -9);
            }

            if (amountToman < MinAmountToman)
                return FailRequest("مبلغ پرداخت کمتر از حد مجاز درگاه است", -9);

            if (amountToman > MaxAmountToman)
                return FailRequest("مبلغ پرداخت بیشتر از حد مجاز درگاه است", -41);

            if (string.IsNullOrWhiteSpace(callbackUrl))
                return FailRequest("آدرس بازگشت پرداخت تنظیم نشده است", -9);

            callbackUrl = callbackUrl.Trim();
            if (!_options.Sandbox &&
                !callbackUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("ZarinPal production callback must be HTTPS: {CallbackHost}",
                    TryHost(callbackUrl));
                return FailRequest("آدرس بازگشت پرداخت نامعتبر است", -9);
            }

            description = NormalizeDescription(description);
            if (string.IsNullOrWhiteSpace(description))
                return FailRequest("توضیحات پرداخت الزامی است", -9);

            var currency = NormalizeCurrency(_options.Currency);
            var payload = new ZarinPalRequestPayload
            {
                MerchantId = _options.MerchantId.Trim(),
                Amount = amountToman,
                Description = description,
                CallbackUrl = callbackUrl,
                Currency = currency,
                Metadata = BuildMetadata(mobile, email, orderId)
            };

            try
            {
                var endpoint = GetApiBaseUrl() + "/pg/v4/payment/request.json";
                _logger.LogInformation(
                    "ZarinPal request — Amount={Amount} Currency={Currency} Sandbox={Sandbox} CallbackHost={CallbackHost}",
                    amountToman, currency, _options.Sandbox, TryHost(callbackUrl));

                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, JsonOptions, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "ZarinPal request HTTP {StatusCode}: {Body}",
                        (int)response.StatusCode,
                        Truncate(body));
                    return FailRequest(ControlledErrorHelper.PaymentFailed);
                }

                var (dataCode, authority, fee, feeType, errorCode, errorMessage) = ParseRequestBody(body);
                var code = dataCode ?? errorCode ?? -1;

                if (code == 100 && !string.IsNullOrWhiteSpace(authority))
                {
                    var paymentUrl = BuildStartPayUrl(authority);
                    _logger.LogInformation(
                        "ZarinPal request success — Authority issued, Fee={Fee}, FeeType={FeeType}",
                        fee, feeType);
                    return new ZarinPalRequestResult
                    {
                        Success = true,
                        Code = code,
                        Authority = authority,
                        PaymentUrl = paymentUrl,
                        Fee = fee,
                        FeeType = feeType
                    };
                }

                _logger.LogWarning(
                    "ZarinPal request rejected — Code={Code} Hint={Hint} Body={Body}",
                    code,
                    ExplainCode(code),
                    Truncate(body));
                return FailRequest(
                    MapPublicError(code, errorMessage),
                    code);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ZarinPal request failed unexpectedly");
                return FailRequest(ControlledErrorHelper.PaymentFailed);
            }
        }

        public async Task<ZarinPalVerifyResult> VerifyPaymentAsync(
            int amountToman,
            string authority,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.MerchantId) || _options.MerchantId.Trim().Length != 36)
            {
                _logger.LogError("ZarinPal MerchantId missing or not 36 chars");
                return FailVerify("تنظیمات درگاه پرداخت ناقص است", -9);
            }

            if (string.IsNullOrWhiteSpace(authority))
                return FailVerify("کد مرجع پرداخت نامعتبر است", -54);

            authority = authority.Trim();

            // فقط تست خودکار لوکال/سندباکس — هرگز در Production
            if (_options.Sandbox &&
                _options.AllowSandboxAutoVerify &&
                authority.StartsWith("S", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ZarinPal sandbox auto-verify enabled — Authority accepted without live gateway verify");
                return new ZarinPalVerifyResult
                {
                    Success = true,
                    AlreadyVerified = false,
                    Code = 100,
                    RefId = $"SBX{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}",
                    CardPan = "5022********1234"
                };
            }

            if (amountToman < MinAmountToman || amountToman > MaxAmountToman)
                return FailVerify("مبلغ پرداخت نامعتبر است", -50);

            // مبلغ verify باید دقیقاً همان واحد request باشد (IRT=تومان / IRR=ریال)
            var payload = new ZarinPalVerifyPayload
            {
                MerchantId = _options.MerchantId.Trim(),
                Amount = amountToman,
                Authority = authority
            };

            try
            {
                var endpoint = GetApiBaseUrl() + "/pg/v4/payment/verify.json";
                _logger.LogInformation(
                    "ZarinPal verify — Amount={Amount} AuthorityPrefix={AuthorityPrefix}",
                    amountToman,
                    authority.Length <= 8 ? authority : authority[..8]);

                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, JsonOptions, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "ZarinPal verify HTTP {StatusCode}: {Body}",
                        (int)response.StatusCode,
                        Truncate(body));
                    return FailVerify(ControlledErrorHelper.PaymentFailed);
                }

                var (dataCode, refId, cardPan, cardHash, fee, feeType, errorCode, errorMessage) = ParseVerifyBody(body);
                var code = dataCode ?? errorCode ?? -1;

                // 100 = اولین Verify موفق | 101 = قبلاً Verify شده (idempotent success)
                if (code is 100 or 101)
                {
                    _logger.LogInformation(
                        "ZarinPal verify success — Code={Code} RefId={RefId} AlreadyVerified={Already}",
                        code, refId, code == 101);
                    return new ZarinPalVerifyResult
                    {
                        Success = true,
                        AlreadyVerified = code == 101,
                        Code = code,
                        RefId = refId,
                        CardPan = cardPan,
                        CardHash = cardHash,
                        Fee = fee,
                        FeeType = feeType
                    };
                }

                _logger.LogWarning(
                    "ZarinPal verify rejected — Code={Code} Hint={Hint} Body={Body}",
                    code,
                    ExplainCode(code),
                    Truncate(body));
                return FailVerify(MapPublicError(code, errorMessage), code);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ZarinPal verify failed unexpectedly");
                return FailVerify(ControlledErrorHelper.PaymentFailed);
            }
        }

        public string BuildStartPayUrl(string authority)
        {
            var baseUrl = _options.Sandbox
                ? "https://sandbox.zarinpal.com/pg/StartPay/"
                : "https://payment.zarinpal.com/pg/StartPay/";
            return baseUrl + authority.Trim();
        }

        private string GetApiBaseUrl() =>
            _options.Sandbox
                ? "https://sandbox.zarinpal.com"
                : "https://payment.zarinpal.com";

        private static string NormalizeCurrency(string? currency)
        {
            if (string.Equals(currency, "IRR", StringComparison.OrdinalIgnoreCase))
                return "IRR";
            return "IRT";
        }

        private static string NormalizeDescription(string? description)
        {
            var text = (description ?? string.Empty).Trim();
            if (text.Length <= MaxDescriptionLength)
                return text;
            return text[..MaxDescriptionLength];
        }

        private static Dictionary<string, string>? BuildMetadata(string? mobile, string? email, string? orderId)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(mobile))
                metadata["mobile"] = mobile.Trim();
            if (!string.IsNullOrWhiteSpace(email))
                metadata["email"] = email.Trim();
            if (!string.IsNullOrWhiteSpace(orderId))
                metadata["order_id"] = orderId.Trim();
            return metadata.Count == 0 ? null : metadata;
        }

        private static ZarinPalRequestResult FailRequest(string message, int code = -1) =>
            new() { Success = false, Code = code, ErrorMessage = message };

        private static ZarinPalVerifyResult FailVerify(string message, int code = -1) =>
            new() { Success = false, Code = code, ErrorMessage = message };

        private static string Truncate(string? value, int max = 500)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Length <= max ? value : value[..max] + "...";
        }

        private static string? TryHost(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

        /// <summary>پیام کنترل‌شده برای کلاینت — جزئیات فنی فقط در لاگ</summary>
        private static string MapPublicError(int code, string? gatewayMessage)
        {
            // به کاربر پیام خام درگاه را نشان نده؛ فقط کدهای شناخته‌شده را برای لاگ نگه می‌داریم
            _ = gatewayMessage;
            return code switch
            {
                -14 => ControlledErrorHelper.PaymentFailed, // domain mismatch
                -50 => ControlledErrorHelper.PaymentFailed, // amount mismatch
                -51 => ControlledErrorHelper.PaymentFailed, // unpaid
                -54 => ControlledErrorHelper.PaymentFailed, // invalid authority
                _ => ControlledErrorHelper.PaymentFailed
            };
        }

        private static string ExplainCode(int code) => code switch
        {
            -9 => "validation",
            -10 => "invalid_merchant_or_ip",
            -11 => "terminal_inactive",
            -12 => "rate_limited",
            -14 => "callback_domain_mismatch",
            -18 => "referrer_domain_mismatch",
            -41 => "amount_too_high",
            -50 => "verify_amount_mismatch",
            -51 => "payment_failed",
            -53 => "wrong_merchant",
            -54 => "invalid_authority",
            100 => "success",
            101 => "already_verified",
            _ => "unknown"
        };

        private static (int? DataCode, string? Authority, int? Fee, string? FeeType, int? ErrorCode, string? ErrorMessage)
            ParseRequestBody(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            int? dataCode = null;
            string? authority = null;
            int? fee = null;
            string? feeType = null;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out var c))
                    dataCode = c;
                if (data.TryGetProperty("authority", out var authEl) && authEl.ValueKind == JsonValueKind.String)
                    authority = authEl.GetString();
                if (data.TryGetProperty("fee", out var feeEl) && feeEl.TryGetInt32(out var f))
                    fee = f;
                if (data.TryGetProperty("fee_type", out var ftEl) && ftEl.ValueKind == JsonValueKind.String)
                    feeType = ftEl.GetString();
            }

            var (errorCode, errorMessage) = TryReadErrors(root);
            return (dataCode, authority, fee, feeType, errorCode, errorMessage);
        }

        private static (int? DataCode, string? RefId, string? CardPan, string? CardHash, int? Fee, string? FeeType, int? ErrorCode, string? ErrorMessage)
            ParseVerifyBody(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            int? dataCode = null;
            string? refId = null;
            string? cardPan = null;
            string? cardHash = null;
            int? fee = null;
            string? feeType = null;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out var c))
                    dataCode = c;
                if (data.TryGetProperty("ref_id", out var refEl))
                    refId = refEl.ValueKind == JsonValueKind.String ? refEl.GetString() : refEl.ToString();
                if (data.TryGetProperty("card_pan", out var panEl) && panEl.ValueKind == JsonValueKind.String)
                    cardPan = panEl.GetString();
                if (data.TryGetProperty("card_hash", out var hashEl) && hashEl.ValueKind == JsonValueKind.String)
                    cardHash = hashEl.GetString();
                if (data.TryGetProperty("fee", out var feeEl) && feeEl.TryGetInt32(out var f))
                    fee = f;
                if (data.TryGetProperty("fee_type", out var ftEl) && ftEl.ValueKind == JsonValueKind.String)
                    feeType = ftEl.GetString();
            }

            var (errorCode, errorMessage) = TryReadErrors(root);
            return (dataCode, refId, cardPan, cardHash, fee, feeType, errorCode, errorMessage);
        }

        /// <summary>
        /// errors در موفقیت [] و در خطا object/{code,message} یا array است.
        /// </summary>
        private static (int? Code, string? Message) TryReadErrors(JsonElement root)
        {
            if (!root.TryGetProperty("errors", out var errors))
                return (null, null);

            if (errors.ValueKind == JsonValueKind.Object)
            {
                int? code = null;
                string? message = null;
                if (errors.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out var c))
                    code = c;
                if (errors.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                    message = msgEl.GetString();
                return (code, message);
            }

            if (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                if (first.ValueKind == JsonValueKind.Object)
                {
                    int? code = null;
                    string? message = null;
                    if (first.TryGetProperty("code", out var arrCode) && arrCode.TryGetInt32(out var c))
                        code = c;
                    if (first.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                        message = msgEl.GetString();
                    return (code, message);
                }
            }

            return (null, null);
        }

        #region Wire DTOs

        private sealed class ZarinPalRequestPayload
        {
            [JsonPropertyName("merchant_id")]
            public string MerchantId { get; set; } = string.Empty;

            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("callback_url")]
            public string CallbackUrl { get; set; } = string.Empty;

            [JsonPropertyName("currency")]
            public string? Currency { get; set; }

            [JsonPropertyName("metadata")]
            public Dictionary<string, string>? Metadata { get; set; }
        }

        private sealed class ZarinPalVerifyPayload
        {
            [JsonPropertyName("merchant_id")]
            public string MerchantId { get; set; } = string.Empty;

            /// <summary>
            /// همان مبلغ request با همان واحد (IRT تومان / IRR ریال).
            /// عدم تطابق → خطای -50.
            /// </summary>
            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("authority")]
            public string Authority { get; set; } = string.Empty;
        }

        #endregion
    }
}
