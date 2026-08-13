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
    /// پیاده‌سازی کلاینت زرین‌پال بر اساس REST API v4
    /// https://www.zarinpal.com/docs/paymentGateway/connectToGateway.html
    /// </summary>
    public sealed class ZarinPalGatewayClient : IZarinPalGatewayClient
    {
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
            if (string.IsNullOrWhiteSpace(_options.MerchantId))
            {
                _logger.LogError("ZarinPal MerchantId is not configured");
                return FailRequest("تنظیمات درگاه پرداخت ناقص است");
            }

            if (amountToman < 1000)
            {
                return FailRequest("مبلغ پرداخت کمتر از حد مجاز درگاه است");
            }

            if (string.IsNullOrWhiteSpace(callbackUrl))
            {
                return FailRequest("آدرس بازگشت پرداخت تنظیم نشده است");
            }

            var currency = NormalizeCurrency(_options.Currency);
            var payload = new ZarinPalRequestPayload
            {
                MerchantId = _options.MerchantId,
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
                    "ZarinPal request — Amount: {Amount}, Currency: {Currency}, Sandbox: {Sandbox}",
                    amountToman, currency, _options.Sandbox);

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

                var (dataCode, authority, errorCode) = ParseRequestBody(body);
                var code = dataCode ?? errorCode ?? -1;

                if (code == 100 && !string.IsNullOrWhiteSpace(authority))
                {
                    var paymentUrl = BuildStartPayUrl(authority);
                    _logger.LogInformation("ZarinPal request success — Authority received");
                    return new ZarinPalRequestResult
                    {
                        Success = true,
                        Code = code,
                        Authority = authority,
                        PaymentUrl = paymentUrl
                    };
                }

                _logger.LogWarning(
                    "ZarinPal request rejected — Code: {Code}, Errors: {Errors}",
                    code,
                    Truncate(body));
                return FailRequest(ControlledErrorHelper.PaymentFailed, code);
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
            if (string.IsNullOrWhiteSpace(_options.MerchantId))
            {
                _logger.LogError("ZarinPal MerchantId is not configured");
                return FailVerify("تنظیمات درگاه پرداخت ناقص است");
            }

            if (string.IsNullOrWhiteSpace(authority))
            {
                return FailVerify("کد مرجع پرداخت نامعتبر است");
            }

            // تست خودکار سندباکس — فقط با هر دو فلگ Sandbox + AllowSandboxAutoVerify
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

            var payload = new ZarinPalVerifyPayload
            {
                MerchantId = _options.MerchantId,
                Amount = amountToman,
                Authority = authority
            };

            try
            {
                var endpoint = GetApiBaseUrl() + "/pg/v4/payment/verify.json";
                _logger.LogInformation("ZarinPal verify — Amount: {Amount}", amountToman);

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

                var (dataCode, refId, cardPan, cardHash, errorCode) = ParseVerifyBody(body);
                var code = dataCode ?? errorCode ?? -1;

                // 100 = اولین Verify موفق | 101 = قبلاً Verify شده (idempotent success)
                if (code is 100 or 101)
                {
                    _logger.LogInformation(
                        "ZarinPal verify success — Code: {Code}, RefId: {RefId}",
                        code,
                        refId);
                    return new ZarinPalVerifyResult
                    {
                        Success = true,
                        AlreadyVerified = code == 101,
                        Code = code,
                        RefId = refId,
                        CardPan = cardPan,
                        CardHash = cardHash
                    };
                }

                _logger.LogWarning(
                    "ZarinPal verify rejected — Code: {Code}, Body: {Body}",
                    code,
                    Truncate(body));
                return FailVerify(ControlledErrorHelper.PaymentFailed, code);
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

        /// <summary>
        /// errors در موفقیت [] و در خطا object است — با JsonDocument هر دو را پوشش می‌دهیم.
        /// </summary>
        private static (int? DataCode, string? Authority, int? ErrorCode) ParseRequestBody(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            int? dataCode = null;
            string? authority = null;
            int? errorCode = null;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out var c))
                    dataCode = c;
                if (data.TryGetProperty("authority", out var authEl) && authEl.ValueKind == JsonValueKind.String)
                    authority = authEl.GetString();
            }

            errorCode = TryReadErrorCode(root);
            return (dataCode, authority, errorCode);
        }

        private static (int? DataCode, string? RefId, string? CardPan, string? CardHash, int? ErrorCode) ParseVerifyBody(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            int? dataCode = null;
            string? refId = null;
            string? cardPan = null;
            string? cardHash = null;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out var c))
                    dataCode = c;
                if (data.TryGetProperty("ref_id", out var refEl))
                    refId = refEl.ToString();
                if (data.TryGetProperty("card_pan", out var panEl) && panEl.ValueKind == JsonValueKind.String)
                    cardPan = panEl.GetString();
                if (data.TryGetProperty("card_hash", out var hashEl) && hashEl.ValueKind == JsonValueKind.String)
                    cardHash = hashEl.GetString();
            }

            return (dataCode, refId, cardPan, cardHash, TryReadErrorCode(root));
        }

        private static int? TryReadErrorCode(JsonElement root)
        {
            if (!root.TryGetProperty("errors", out var errors))
                return null;

            if (errors.ValueKind == JsonValueKind.Object &&
                errors.TryGetProperty("code", out var codeEl) &&
                codeEl.TryGetInt32(out var code))
                return code;

            if (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                if (first.ValueKind == JsonValueKind.Object &&
                    first.TryGetProperty("code", out var arrCode) &&
                    arrCode.TryGetInt32(out var c))
                    return c;
            }

            return null;
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

            [JsonPropertyName("amount")]
            public int Amount { get; set; }

            [JsonPropertyName("authority")]
            public string Authority { get; set; } = string.Empty;
        }

        #endregion
    }
}
