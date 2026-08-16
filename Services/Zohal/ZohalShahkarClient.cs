using Api_Vapp.Configuration;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Zohal;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api_Vapp.Services.Zohal
{
    /// <summary>
    /// کلاینت HTTP سرویس شاهکار زحل — POST /services/inquiry/shahkar
    /// </summary>
    public sealed class ZohalShahkarClient : IZohalShahkarService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private readonly ZohalApiSettings _settings;
        private readonly IZohalInquiryLogRepository _inquiryLogRepository;
        private readonly IAuditService _audit;
        private readonly ILogger<ZohalShahkarClient> _logger;

        public ZohalShahkarClient(
            HttpClient httpClient,
            IOptions<ZohalApiSettings> settings,
            IZohalInquiryLogRepository inquiryLogRepository,
            IAuditService audit,
            ILogger<ZohalShahkarClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _inquiryLogRepository = inquiryLogRepository;
            _audit = audit;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_settings.ApiToken))
            {
                _logger.LogWarning("Zohal:ApiToken is empty — Shahkar verification will fail");
            }
        }

        public bool IsEnabled => _settings.Enabled;

        public async Task<ShahkarVerificationResult> VerifyAsync(
            string nationalCode,
            string mobile,
            ShahkarVerifyContext? context = null,
            CancellationToken cancellationToken = default)
        {
            context ??= new ShahkarVerifyContext();
            var traceId = context.TraceId
                ?? Activity.Current?.Id
                ?? Activity.Current?.TraceId.ToString()
                ?? Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(_settings.ApiToken))
            {
                _logger.LogError("Zohal Shahkar called without ApiToken — TraceId={TraceId}", traceId);
                return await PersistAndReturnAsync(
                    BuildFailureLog(context, traceId, nationalCode, mobile, null, null, null, null, "missing_api_token", null,
                        ShahkarVerificationStatus.ProviderAuthFailed, ErrorCodes.IdentityVerificationUnavailable, 0),
                    ShahkarVerificationResult.ProviderAuthFailed(providerErrorCode: "missing_api_token"));
            }

            var normalizedNationalCode = nationalCode.Trim();
            var normalizedMobile = NormalizeMobile(mobile);
            var maskedMobile = MaskMobile(normalizedMobile);
            var maskedNationalCode = MaskNationalCode(normalizedNationalCode);
            var requestJson = JsonSerializer.Serialize(new
            {
                mobile = maskedMobile,
                national_code = maskedNationalCode
            });

            _logger.LogInformation(
                "شروع استعلام شاهکار — Source={Source}, Mobile={Mobile}, NationalCode={NationalCodeMasked}, TraceId={TraceId}",
                context.Source,
                maskedMobile,
                maskedNationalCode,
                traceId);

            var sw = Stopwatch.StartNew();
            var payload = new ZohalShahkarRequest
            {
                Mobile = normalizedMobile,
                NationalCode = normalizedNationalCode
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "services/inquiry/shahkar");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiToken.Trim());
                request.Content = JsonContent.Create(payload, options: JsonOptions);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                sw.Stop();
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var httpStatus = (int)response.StatusCode;

                _logger.LogInformation(
                    "پاسخ شاهکار — HTTP {StatusCode}, DurationMs={DurationMs}, BodyLength={BodyLength}, TraceId={TraceId}",
                    httpStatus,
                    sw.ElapsedMilliseconds,
                    body.Length,
                    traceId);

                if (string.IsNullOrWhiteSpace(body))
                {
                    _logger.LogWarning("Zohal Shahkar empty body — HTTP {StatusCode}, TraceId={TraceId}", httpStatus, traceId);
                    return await PersistAndReturnAsync(
                        BuildFailureLog(context, traceId, normalizedMobile, normalizedNationalCode, requestJson, null,
                            httpStatus, null, null, null,
                            ShahkarVerificationStatus.ServiceUnavailable, ErrorCodes.IdentityVerificationUnavailable, sw.ElapsedMilliseconds),
                        ShahkarVerificationResult.ServiceUnavailable(httpStatusCode: httpStatus));
                }

                ZohalShahkarApiResponse? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<ZohalShahkarApiResponse>(body, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Zohal Shahkar parse failed — HTTP {StatusCode}, TraceId={TraceId}", httpStatus, traceId);
                    return await PersistAndReturnAsync(
                        BuildFailureLog(context, traceId, normalizedMobile, normalizedNationalCode, requestJson, body,
                            httpStatus, null, null, null,
                            ShahkarVerificationStatus.ServiceUnavailable, ErrorCodes.IdentityVerificationUnavailable, sw.ElapsedMilliseconds),
                        ShahkarVerificationResult.ServiceUnavailable(httpStatusCode: httpStatus));
                }

                var responseBody = parsed?.ResponseBody;
                var zohalResult = parsed?.Result;
                var providerErrorCode = responseBody?.ErrorCode;
                var providerMessage = responseBody?.Message;
                var matched = responseBody?.Data?.Matched;

                var outcome = ZohalResponseMapper.MapOutcome(
                    httpStatus,
                    zohalResult ?? -1,
                    matched,
                    providerErrorCode,
                    providerMessage);

                var userErrorCode = MapUserFacingErrorCode(outcome);
                var succeeded = outcome == ShahkarVerificationStatus.Matched;

                var log = new ZohalInquiryLog
                {
                    InquiryType = "shahkar",
                    Source = context.Source,
                    MobileMasked = maskedMobile,
                    NationalCodeMasked = maskedNationalCode,
                    Matched = matched,
                    HttpStatusCode = httpStatus,
                    ZohalResultCode = zohalResult,
                    ProviderErrorCode = providerErrorCode,
                    ProviderMessage = providerMessage,
                    OutcomeStatus = outcome.ToString(),
                    UserFacingErrorCode = userErrorCode,
                    RequestJson = requestJson,
                    ResponseJson = Truncate(body, 8000),
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Succeeded = succeeded,
                    TraceId = traceId,
                    IpAddress = context.IpAddress,
                    CreatedAt = DateTime.UtcNow
                };

                var result = MapResult(outcome, log);
                return await PersistAndReturnAsync(log, result);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                _logger.LogError(ex, "Zohal Shahkar timeout — Mobile={Mobile}, TraceId={TraceId}", maskedMobile, traceId);
                return await PersistAndReturnAsync(
                    BuildFailureLog(context, traceId, normalizedMobile, normalizedNationalCode, requestJson, null,
                        (int)HttpStatusCode.GatewayTimeout, null, "timeout", "request timeout",
                        ShahkarVerificationStatus.ServiceUnavailable, ErrorCodes.IdentityVerificationUnavailable, sw.ElapsedMilliseconds),
                    ShahkarVerificationResult.ServiceUnavailable(httpStatusCode: (int)HttpStatusCode.GatewayTimeout, providerErrorCode: "timeout"));
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Zohal Shahkar unexpected error — Mobile={Mobile}, TraceId={TraceId}", maskedMobile, traceId);
                return await PersistAndReturnAsync(
                    BuildFailureLog(context, traceId, normalizedMobile, normalizedNationalCode, requestJson, null,
                        null, null, "unexpected_error", ex.GetType().Name,
                        ShahkarVerificationStatus.ServiceUnavailable, ErrorCodes.IdentityVerificationUnavailable, sw.ElapsedMilliseconds),
                    ShahkarVerificationResult.ServiceUnavailable(providerErrorCode: "unexpected_error"));
            }
        }

        private async Task<ShahkarVerificationResult> PersistAndReturnAsync(
            ZohalInquiryLog log,
            ShahkarVerificationResult result)
        {
            try
            {
                var saved = await _inquiryLogRepository.AddAsync(log);
                result = CopyWithLogId(result, saved.Id);
                await WriteAuditAsync(saved, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist Zohal inquiry log — TraceId={TraceId}", log.TraceId);
            }

            return result;
        }

        private async Task WriteAuditAsync(ZohalInquiryLog log, ShahkarVerificationResult result)
        {
            var action = result.Status switch
            {
                ShahkarVerificationStatus.Matched => AuditActions.ShahkarMatched,
                ShahkarVerificationStatus.NotMatched => AuditActions.ShahkarNotMatched,
                ShahkarVerificationStatus.InvalidInput => AuditActions.ShahkarInvalidInput,
                ShahkarVerificationStatus.InsufficientBalance => AuditActions.ShahkarInsufficientBalance,
                ShahkarVerificationStatus.ProviderAuthFailed => AuditActions.ShahkarProviderAuthFailed,
                ShahkarVerificationStatus.IpNotAllowed => AuditActions.ShahkarIpNotAllowed,
                _ => AuditActions.ShahkarFailed
            };

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.IdentityVerification,
                Action = action,
                EntityType = AuditEntityTypes.ZohalInquiry,
                EntityId = log.Id.ToString(),
                Succeeded = log.Succeeded,
                ErrorMessage = log.Succeeded ? null : $"{log.OutcomeStatus}:{log.ProviderErrorCode}",
                Metadata = new
                {
                    inquiryType = log.InquiryType,
                    source = log.Source,
                    mobileMasked = log.MobileMasked,
                    nationalCodeMasked = log.NationalCodeMasked,
                    matched = log.Matched,
                    httpStatusCode = log.HttpStatusCode,
                    zohalResultCode = log.ZohalResultCode,
                    providerErrorCode = log.ProviderErrorCode,
                    providerMessage = log.ProviderMessage,
                    outcomeStatus = log.OutcomeStatus,
                    userFacingErrorCode = log.UserFacingErrorCode,
                    durationMs = log.DurationMs,
                    traceId = log.TraceId,
                    zohalInquiryLogId = log.Id
                }
            });
        }

        private static ShahkarVerificationResult MapResult(ShahkarVerificationStatus outcome, ZohalInquiryLog log)
        {
            return outcome switch
            {
                ShahkarVerificationStatus.Matched => ShahkarVerificationResult.Matched(log.Id),
                ShahkarVerificationStatus.NotMatched => ShahkarVerificationResult.NotMatched(log.Id),
                ShahkarVerificationStatus.InvalidInput => ShahkarVerificationResult.InvalidInput(log.Id, log.ProviderErrorCode),
                ShahkarVerificationStatus.InsufficientBalance => ShahkarVerificationResult.InsufficientBalance(log.Id, log.ProviderErrorCode),
                ShahkarVerificationStatus.ProviderAuthFailed => ShahkarVerificationResult.ProviderAuthFailed(log.Id, log.ProviderErrorCode),
                ShahkarVerificationStatus.IpNotAllowed => ShahkarVerificationResult.IpNotAllowed(log.Id),
                _ => ShahkarVerificationResult.ServiceUnavailable(
                    log.Id,
                    log.HttpStatusCode,
                    log.ZohalResultCode,
                    log.ProviderErrorCode)
            };
        }

        private static string? MapUserFacingErrorCode(ShahkarVerificationStatus outcome) =>
            outcome switch
            {
                ShahkarVerificationStatus.Matched => null,
                ShahkarVerificationStatus.NotMatched => ErrorCodes.IdentityVerificationFailed,
                ShahkarVerificationStatus.InvalidInput => ErrorCodes.InvalidInput,
                _ => ErrorCodes.IdentityVerificationUnavailable
            };

        private static ShahkarVerificationResult CopyWithLogId(ShahkarVerificationResult result, long logId) =>
            result.Status switch
            {
                ShahkarVerificationStatus.Matched => ShahkarVerificationResult.Matched(logId),
                ShahkarVerificationStatus.NotMatched => ShahkarVerificationResult.NotMatched(logId),
                ShahkarVerificationStatus.InvalidInput => ShahkarVerificationResult.InvalidInput(logId, result.ProviderErrorCode),
                ShahkarVerificationStatus.InsufficientBalance => ShahkarVerificationResult.InsufficientBalance(logId, result.ProviderErrorCode),
                ShahkarVerificationStatus.ProviderAuthFailed => ShahkarVerificationResult.ProviderAuthFailed(logId, result.ProviderErrorCode),
                ShahkarVerificationStatus.IpNotAllowed => ShahkarVerificationResult.IpNotAllowed(logId),
                ShahkarVerificationStatus.Skipped => ShahkarVerificationResult.Skipped(),
                _ => ShahkarVerificationResult.ServiceUnavailable(
                    logId,
                    result.HttpStatusCode,
                    result.ZohalResultCode,
                    result.ProviderErrorCode)
            };

        private static ZohalInquiryLog BuildFailureLog(
            ShahkarVerifyContext context,
            string traceId,
            string mobile,
            string nationalCode,
            string? requestJson,
            string? responseJson,
            int? httpStatusCode,
            int? zohalResultCode,
            string? providerErrorCode,
            string? providerMessage,
            ShahkarVerificationStatus outcome,
            string? userFacingErrorCode,
            long durationMs) =>
            new()
            {
                InquiryType = "shahkar",
                Source = context.Source,
                MobileMasked = MaskMobile(NormalizeMobile(mobile)),
                NationalCodeMasked = MaskNationalCode(nationalCode.Trim()),
                Matched = null,
                HttpStatusCode = httpStatusCode,
                ZohalResultCode = zohalResultCode,
                ProviderErrorCode = providerErrorCode,
                ProviderMessage = providerMessage,
                OutcomeStatus = outcome.ToString(),
                UserFacingErrorCode = userFacingErrorCode,
                RequestJson = requestJson,
                ResponseJson = Truncate(responseJson, 8000),
                DurationMs = (int)durationMs,
                Succeeded = false,
                TraceId = traceId,
                IpAddress = context.IpAddress,
                CreatedAt = DateTime.UtcNow
            };

        private static string NormalizeMobile(string mobile)
        {
            var trimmed = mobile.Trim();
            if (trimmed.StartsWith("+98", StringComparison.Ordinal))
                return "0" + trimmed[3..];
            if (trimmed.StartsWith("98", StringComparison.Ordinal) && trimmed.Length == 12)
                return "0" + trimmed[2..];
            return trimmed;
        }

        private static string MaskMobile(string mobile)
        {
            if (mobile.Length < 7)
                return "***";

            return mobile[..4] + "****" + mobile[^3..];
        }

        private static string MaskNationalCode(string nationalCode)
        {
            if (nationalCode.Length < 4)
                return "****";

            return nationalCode[..2] + "******" + nationalCode[^2..];
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value[..maxLength];
        }

        private sealed class ZohalShahkarRequest
        {
            [JsonPropertyName("mobile")]
            public string Mobile { get; set; } = string.Empty;

            [JsonPropertyName("national_code")]
            public string NationalCode { get; set; } = string.Empty;
        }

        private sealed class ZohalShahkarApiResponse
        {
            [JsonPropertyName("response_body")]
            public ZohalShahkarResponseBody? ResponseBody { get; set; }

            [JsonPropertyName("result")]
            public int Result { get; set; }
        }

        private sealed class ZohalShahkarResponseBody
        {
            [JsonPropertyName("data")]
            public ZohalShahkarData? Data { get; set; }

            [JsonPropertyName("error_code")]
            public string? ErrorCode { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }
        }

        private sealed class ZohalShahkarData
        {
            [JsonPropertyName("matched")]
            public bool? Matched { get; set; }
        }
    }
}
