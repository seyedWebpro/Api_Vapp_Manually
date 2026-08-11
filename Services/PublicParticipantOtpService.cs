using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Auth;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Public;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace Api_Vapp.Services
{
    /// <summary>
    /// OTP شرکت‌کننده عمومی — همان الگوی AuthService (کش، محدودیت ارسال، تلاش ناموفق).
    /// هزینه از کیف پول مالک فرم/گردونه کسر می‌شود؛ کمبود موجودی عملیات را fail نمی‌کند و فقط پیامک ارسال نمی‌شود.
    /// </summary>
    public class PublicParticipantOtpService : IPublicParticipantOtpService
    {
        private const int OtpExpirationMinutes = 5;
        private const int MaxOtpAttempts = 5;
        private const int OtpLockoutMinutes = 15;
        private const int OtpRateLimitMinutes = 2;

        private readonly Api_Context _context;
        private readonly IMemoryCache _cache;
        private readonly ISmsService _smsService;
        private readonly IUserSmsBillingService _userSmsBilling;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<PublicParticipantOtpService> _logger;

        public PublicParticipantOtpService(
            Api_Context context,
            IMemoryCache cache,
            ISmsService smsService,
            IUserSmsBillingService userSmsBilling,
            IHostEnvironment environment,
            ILogger<PublicParticipantOtpService> logger)
        {
            _context = context;
            _cache = cache;
            _smsService = smsService;
            _userSmsBilling = userSmsBilling;
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

                var ownerUserId = await ResolveOwnerUserIdAsync(session);
                if (ownerUserId == null)
                {
                    _logger.LogError(
                        "Cannot resolve owner for public OTP — session {SessionId}, type {Type}, resource {ResourceId}",
                        session.Id, session.ResourceType, session.ResourceId);
                    return ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
                }

                var otpCode = await _smsService.GenerateOtpAsync();
                var otpCacheKey = OtpKey(session);
                SetCacheData(otpCacheKey, otpCode, OtpExpirationMinutes);
                SetRateLimit(mobile, OtpRateLimitMinutes);
                _cache.Remove(AttemptKey(session));

                var sendResult = await _userSmsBilling.TrySendOtpAsync(
                    ownerUserId.Value,
                    mobile,
                    otpCode,
                    "VerifyOtp",
                    SmsSourceModules.PublicParticipantOtp,
                    "کد تأیید شرکت‌کننده",
                    $"هزینه OTP عمومی — {purpose}",
                    session.Id,
                    purpose);

                var smsSent = sendResult.Sent;

                if (sendResult.SkippedInsufficientBalance)
                {
                    // عملیات ثبت‌نام/جلسه fail نمی‌شود؛ فقط پیامک ارسال نمی‌شود
                    _logger.LogInformation(
                        "Public OTP SMS skipped (insufficient wallet) — session {SessionId}, owner {OwnerUserId}",
                        session.Id, ownerUserId.Value);
                }
                else if (!smsSent)
                {
                    if (!_environment.IsDevelopment())
                    {
                        _cache.Remove(otpCacheKey);
                        _cache.Remove($"PublicOtpRateLimit_{mobile}");
                        _logger.LogError(
                            "Failed to send public OTP SMS for session {SessionId}: {Message}",
                            session.Id, sendResult.Message);
                        return ApiResponse<PublicParticipantOtpResponseDto>.Error(
                            ControlledErrorHelper.SmsFailed,
                            503,
                            errorCode: ErrorCodes.SmsFailed);
                    }

                    _logger.LogWarning(
                        "Public OTP SMS failed in Development — continuing with cached OTP for session {SessionId}, mobile {Mobile}",
                        session.Id,
                        mobile);
                }

                DevOtpLogger.Write(_logger, mobile, otpCode, purpose);

                var responseMessage = sendResult.SkippedInsufficientBalance
                    ? "ثبت انجام شد؛ ارسال پیامک به‌خاطر کمبود موجودی کیف پول کسب‌وکار انجام نشد"
                    : "کد تایید به شماره موبایل ارسال شد";

                _logger.LogInformation(
                    "Public OTP ready — session {SessionId}, mobile {Mobile}, purpose {Purpose}, smsSent {SmsSent}, skippedWallet {Skipped}, expiresInSeconds {ExpiresInSeconds}",
                    session.Id,
                    mobile,
                    purpose,
                    smsSent,
                    sendResult.SkippedInsufficientBalance,
                    OtpExpirationMinutes * 60);

                return ApiResponse<PublicParticipantOtpResponseDto>.CreateSuccess(
                    new PublicParticipantOtpResponseDto
                    {
                        ExpiresInSeconds = OtpExpirationMinutes * 60,
                        RetryAfterSeconds = OtpRateLimitMinutes * 60,
                        IsPhoneVerified = false,
                        OtpCode = otpCode
                    },
                    responseMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending public OTP for session {SessionId}", session.Id);
                return ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<int?> ResolveOwnerUserIdAsync(PublicParticipantSession session)
        {
            return session.ResourceType switch
            {
                PublicParticipantResourceType.UserForm =>
                    await _context.UserForms.AsNoTracking()
                        .Where(f => f.Id == session.ResourceId && !f.IsDeleted)
                        .Select(f => (int?)f.UserId)
                        .FirstOrDefaultAsync(),
                PublicParticipantResourceType.LuckyWheel =>
                    await _context.LuckyWheels.AsNoTracking()
                        .Where(w => w.Id == session.ResourceId && !w.IsDeleted)
                        .Select(w => (int?)w.UserId)
                        .FirstOrDefaultAsync(),
                _ => null
            };
        }

        private (bool IsRateLimited, int RetryAfterSeconds) CheckRateLimit(string mobile)
        {
            var key = $"PublicOtpRateLimit_{mobile}";
            if (_cache.TryGetValue(key, out DateTime limitedUntil) && limitedUntil > DateTime.UtcNow)
            {
                var retryAfter = (int)Math.Ceiling((limitedUntil - DateTime.UtcNow).TotalSeconds);
                return (true, Math.Max(1, retryAfter));
            }

            return (false, 0);
        }

        private void SetRateLimit(string mobile, int minutes)
        {
            var key = $"PublicOtpRateLimit_{mobile}";
            _cache.Set(
                key,
                DateTime.UtcNow.AddMinutes(minutes),
                new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(minutes))
                    .SetSize(1));
        }

        private void SetCacheData(string key, object value, int expirationMinutes)
        {
            _cache.Set(
                key,
                value,
                new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(expirationMinutes))
                    .SetSize(1));
        }

        private static string OtpKey(PublicParticipantSession session) =>
            $"PublicOtp_{session.Id}_{session.ParticipantMobile}";

        private static string AttemptKey(PublicParticipantSession session) =>
            $"PublicOtpAttempt_{session.Id}_{session.ParticipantMobile}";
    }
}
