using Api_Vapp.DTOs.Auth;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Public;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace Api_Vapp.Services
{
    /// <summary>
    /// OTP شرکت‌کننده عمومی — همان الگوی AuthService (کش، محدودیت ارسال، تلاش ناموفق)
    /// </summary>
    public class PublicParticipantOtpService : IPublicParticipantOtpService
    {
        private const int OtpExpirationMinutes = 5;
        private const int MaxOtpAttempts = 5;
        private const int OtpLockoutMinutes = 15;
        private const int OtpRateLimitMinutes = 2;

        private readonly IMemoryCache _cache;
        private readonly ISmsService _smsService;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<PublicParticipantOtpService> _logger;

        public PublicParticipantOtpService(
            IMemoryCache cache,
            ISmsService smsService,
            IHostEnvironment environment,
            ILogger<PublicParticipantOtpService> logger)
        {
            _cache = cache;
            _smsService = smsService;
            _environment = environment;
            _logger = logger;
        }

        public async Task<ApiResponse<PublicParticipantOtpResponseDto>> SendAsync(
            PublicParticipantSession session,
            string purpose)
        {
            return await SendInternalAsync(session, purpose, respectRateLimit: true);
        }

        public async Task<ApiResponse<PublicParticipantOtpResponseDto>> ResendAsync(
            PublicParticipantSession session,
            string purpose)
        {
            if (session.PhoneVerifiedAt.HasValue)
            {
                return ApiResponse<PublicParticipantOtpResponseDto>.BadRequest(
                    "شماره موبایل قبلاً تأیید شده است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            return await SendInternalAsync(session, purpose, respectRateLimit: true);
        }

        public Task<ApiResponse<PublicParticipantOtpResponseDto>> VerifyAsync(
            PublicParticipantSession session,
            string otpCode)
        {
            try
            {
                if (session.PhoneVerifiedAt.HasValue)
                {
                    return Task.FromResult(ApiResponse<PublicParticipantOtpResponseDto>.CreateSuccess(
                        new PublicParticipantOtpResponseDto
                        {
                            IsPhoneVerified = true,
                            ExpiresInSeconds = 0
                        },
                        "شماره موبایل قبلاً تأیید شده است"));
                }

                var attemptKey = AttemptKey(session);
                var attemptData = _cache.Get<OtpAttemptCacheDto>(attemptKey);

                if (attemptData?.LockedUntil != null && attemptData.LockedUntil > DateTime.UtcNow)
                {
                    var remainingMinutes = Math.Max(
                        1,
                        (int)Math.Ceiling((attemptData.LockedUntil.Value - DateTime.UtcNow).TotalMinutes));

                    _logger.LogWarning(
                        "Public OTP locked for session {SessionId} mobile {Mobile}",
                        session.Id,
                        session.ParticipantMobile);

                    return Task.FromResult(ApiResponse<PublicParticipantOtpResponseDto>.Error(
                        $"به دلیل تلاش‌های ناموفق، تا {remainingMinutes} دقیقه امکان تأیید وجود ندارد",
                        423,
                        errorCode: ErrorCodes.OtpLocked));
                }

                var otpCacheKey = OtpKey(session);
                if (!_cache.TryGetValue(otpCacheKey, out string? cachedOtp) || string.IsNullOrWhiteSpace(cachedOtp))
                {
                    return Task.FromResult(ApiResponse<PublicParticipantOtpResponseDto>.BadRequest(
                        ControlledErrorHelper.OtpExpired,
                        errorCode: ErrorCodes.OtpExpired));
                }

                var userOtp = otpCode?.Trim() ?? string.Empty;
                if (!string.Equals(cachedOtp.Trim(), userOtp, StringComparison.Ordinal))
                {
                    attemptData ??= new OtpAttemptCacheDto
                    {
                        AttemptCount = 0,
                        FirstAttemptTime = DateTime.UtcNow
                    };
                    attemptData.AttemptCount++;

                    if (attemptData.AttemptCount >= MaxOtpAttempts)
                    {
                        attemptData.LockedUntil = DateTime.UtcNow.AddMinutes(OtpLockoutMinutes);
                        _logger.LogWarning(
                            "Public OTP attempts exceeded for session {SessionId}",
                            session.Id);
                    }

                    SetCacheData(attemptKey, attemptData, OtpLockoutMinutes + 5);

                    return Task.FromResult(ApiResponse<PublicParticipantOtpResponseDto>.BadRequest(
                        ControlledErrorHelper.OtpIncorrect,
                        errorCode: ErrorCodes.OtpIncorrect));
                }

                _cache.Remove(otpCacheKey);
                _cache.Remove(attemptKey);

                return Task.FromResult(ApiResponse<PublicParticipantOtpResponseDto>.CreateSuccess(
                    new PublicParticipantOtpResponseDto
                    {
                        IsPhoneVerified = true,
                        ExpiresInSeconds = 0
                    },
                    "شماره موبایل با موفقیت تأیید شد"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying public OTP for session {SessionId}", session.Id);
                return Task.FromResult(ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected));
            }
        }

        private async Task<ApiResponse<PublicParticipantOtpResponseDto>> SendInternalAsync(
            PublicParticipantSession session,
            string purpose,
            bool respectRateLimit)
        {
            try
            {
                var mobile = session.ParticipantMobile;

                if (respectRateLimit)
                {
                    var (isRateLimited, retryAfterSeconds) = CheckRateLimit(mobile);
                    if (isRateLimited)
                    {
                        return new ApiResponse<PublicParticipantOtpResponseDto>
                        {
                            StatusCode = 429,
                            Success = false,
                            Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                            ErrorCode = ErrorCodes.OtpRateLimited,
                            Data = new PublicParticipantOtpResponseDto
                            {
                                ExpiresInSeconds = 0,
                                RetryAfterSeconds = retryAfterSeconds
                            }
                        };
                    }
                }

                var otpCode = await _smsService.GenerateOtpAsync();
                var otpCacheKey = OtpKey(session);
                SetCacheData(otpCacheKey, otpCode, OtpExpirationMinutes);
                SetRateLimit(mobile, OtpRateLimitMinutes);
                _cache.Remove(AttemptKey(session));

                var sent = await _smsService.SendOtpAsync(mobile, otpCode, "VerifyOtp");
                if (!sent)
                {
                    if (!_environment.IsDevelopment())
                    {
                        _cache.Remove(otpCacheKey);
                        _cache.Remove($"PublicOtpRateLimit_{mobile}");
                        _logger.LogError("Failed to send public OTP SMS for session {SessionId}", session.Id);
                        return ApiResponse<PublicParticipantOtpResponseDto>.Error(
                            ControlledErrorHelper.SmsFailed,
                            503,
                            errorCode: ErrorCodes.SmsFailed);
                    }

                    // Development: پنل SMS در دسترس نیست (مثلاً DNS ایران) — OTP در کش می‌ماند برای تست محلی
                    _logger.LogWarning(
                        "Public OTP SMS failed in Development — continuing with cached OTP for session {SessionId}, mobile {Mobile}",
                        session.Id,
                        mobile);
                }

                DevOtpLogger.Write(_logger, mobile, otpCode, purpose);

                _logger.LogInformation(
                    "Public OTP ready — session {SessionId}, mobile {Mobile}, purpose {Purpose}, smsSent {SmsSent}, expiresInSeconds {ExpiresInSeconds}",
                    session.Id,
                    mobile,
                    purpose,
                    sent,
                    OtpExpirationMinutes * 60);

                return ApiResponse<PublicParticipantOtpResponseDto>.CreateSuccess(
                    new PublicParticipantOtpResponseDto
                    {
                        ExpiresInSeconds = OtpExpirationMinutes * 60,
                        RetryAfterSeconds = OtpRateLimitMinutes * 60,
                        IsPhoneVerified = false,
                        OtpCode = otpCode
                    },
                    sent ? "کد تایید ارسال شد" : "کد تایید آماده است (پیامک در Development ارسال نشد)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending public OTP for session {SessionId}", session.Id);
                return ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private (bool isRateLimited, int? retryAfterSeconds) CheckRateLimit(string phoneNumber)
        {
            var rateLimitKey = $"PublicOtpRateLimit_{phoneNumber}";
            if (_cache.TryGetValue(rateLimitKey, out RateLimitInfoDto? rateLimitInfo) && rateLimitInfo != null)
            {
                if (rateLimitInfo.ExpiresAt > DateTime.UtcNow)
                {
                    var remainingSeconds = (int)Math.Ceiling((rateLimitInfo.ExpiresAt - DateTime.UtcNow).TotalSeconds);
                    if (remainingSeconds > 0)
                    {
                        return (true, remainingSeconds);
                    }
                }

                _cache.Remove(rateLimitKey);
            }

            return (false, null);
        }

        private void SetRateLimit(string phoneNumber, int minutes)
        {
            var rateLimitKey = $"PublicOtpRateLimit_{phoneNumber}";
            var rateLimitInfo = new RateLimitInfoDto
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(minutes),
                IsActive = true
            };

            SetCacheData(rateLimitKey, rateLimitInfo, minutes);
        }

        private void SetCacheData<T>(string key, T data, int expirationMinutes)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expirationMinutes),
                Priority = CacheItemPriority.Normal,
                Size = 1
            };
            _cache.Set(key, data, cacheOptions);
        }

        private static string OtpKey(PublicParticipantSession session) =>
            $"PublicFormOtp_{session.ResourceType}_{session.ResourceId}_{session.ParticipantMobile}";

        private static string AttemptKey(PublicParticipantSession session) =>
            $"PublicOtpAttempt_{session.ResourceType}_{session.ResourceId}_{session.ParticipantMobile}";
    }
}
