using System.Security.Cryptography;
using System.Text;
using Api_Vapp.Configuration;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    public class PublicParticipantSessionService : IPublicParticipantSessionService
    {
        private readonly Api_Context _context;
        private readonly PublicParticipantOptions _options;
        private readonly string _pepper;
        private readonly ILogger<PublicParticipantSessionService> _logger;

        public PublicParticipantSessionService(
            Api_Context context,
            IOptions<PublicParticipantOptions> options,
            IConfiguration configuration,
            ILogger<PublicParticipantSessionService> logger)
        {
            _context = context;
            _options = options.Value;
            _logger = logger;

            var configuredPepper = _options.TokenPepper;
            _pepper = string.IsNullOrWhiteSpace(configuredPepper)
                ? configuration["Jwt:Secret"] ?? "VappPublicParticipantPepper"
                : configuredPepper;
        }

        public async Task<ApiResponse<PublicParticipantSessionTokenResult>> CreateOrRefreshAsync(
            PublicParticipantResourceType resourceType,
            int resourceId,
            string fullName,
            string mobile)
        {
            try
            {
                var now = DateTime.UtcNow;
                var sessionMinutes = Math.Clamp(_options.SessionMinutes, 5, 24 * 60);
                var expiresAt = now.AddMinutes(sessionMinutes);

                var existing = await _context.PublicParticipantSessions
                    .FirstOrDefaultAsync(s =>
                        !s.IsDeleted &&
                        s.ResourceType == resourceType &&
                        s.ResourceId == resourceId &&
                        s.ParticipantMobile == mobile);

                if (existing != null)
                {
                    // ConsumedAt یعنی این توکن قبلاً برای عمل نهایی استفاده شده؛
                    // اما «تکرار مجاز نیست» فقط وقتی معنا دارد که خود سرویس منبع
                    // (فرم: submission / گردونه: participant) تکمیل واقعی را چک کند.
                    // اگر کاربر بدون ثبت نهایی برگردد، نباید اینجا برای همیشه قفل شود.
                    if (existing.ConsumedAt.HasValue || existing.ExpiresAt <= now)
                    {
                        existing.IsDeleted = true;
                        existing.UpdatedAt = now;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        var (rotatedToken, rotatedHash) = GenerateTokenPair();
                        existing.ParticipantFullName = fullName;
                        existing.TokenHash = rotatedHash;
                        existing.ExpiresAt = expiresAt;
                        existing.PhoneVerifiedAt = null;
                        existing.UpdatedAt = now;
                        await _context.SaveChangesAsync();

                        _logger.LogInformation(
                            "Public participant session refreshed {SessionId} for {ResourceType}/{ResourceId}, mobile {Mobile}",
                            existing.Id,
                            resourceType,
                            resourceId,
                            mobile);

                        return ApiResponse<PublicParticipantSessionTokenResult>.CreateSuccess(
                            new PublicParticipantSessionTokenResult
                            {
                                Session = existing,
                                AccessToken = rotatedToken
                            });
                    }
                }

                var (accessToken, tokenHash) = GenerateTokenPair();
                var session = new PublicParticipantSession
                {
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    ParticipantFullName = fullName,
                    ParticipantMobile = mobile,
                    TokenHash = tokenHash,
                    ExpiresAt = expiresAt,
                    PhoneVerifiedAt = null,
                    CreatedAt = now
                };

                await _context.PublicParticipantSessions.AddAsync(session);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Public participant session created {SessionId} for {ResourceType}/{ResourceId}, mobile {Mobile}",
                    session.Id,
                    resourceType,
                    resourceId,
                    mobile);

                return ApiResponse<PublicParticipantSessionTokenResult>.CreateSuccess(
                    new PublicParticipantSessionTokenResult
                    {
                        Session = session,
                        AccessToken = accessToken
                    },
                    "جلسه با موفقیت ایجاد شد",
                    201);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating public participant session for {ResourceType}/{ResourceId}", resourceType, resourceId);
                return ApiResponse<PublicParticipantSessionTokenResult>.BadRequest(
                    "امکان ایجاد جلسه جدید وجود ندارد. لطفاً دوباره تلاش کنید",
                    errorCode: ErrorCodes.ValidationFailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating public participant session for {ResourceType}/{ResourceId}", resourceType, resourceId);
                return ApiResponse<PublicParticipantSessionTokenResult>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PublicParticipantSession>> ValidateActiveAsync(
            string accessToken,
            PublicParticipantResourceType resourceType,
            int resourceId,
            bool requirePhoneVerified = false)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 200)
            {
                return ApiResponse<PublicParticipantSession>.Unauthorized(
                    "توکن دسترسی نامعتبر است",
                    ErrorCodes.TokenInvalid);
            }

            try
            {
                var tokenHash = HashToken(accessToken.Trim());
                var now = DateTime.UtcNow;

                var session = await _context.PublicParticipantSessions
                    .FirstOrDefaultAsync(s =>
                        !s.IsDeleted &&
                        s.TokenHash == tokenHash &&
                        s.ResourceType == resourceType &&
                        s.ResourceId == resourceId);

                if (session == null)
                {
                    return ApiResponse<PublicParticipantSession>.Unauthorized(
                        "توکن دسترسی نامعتبر است",
                        ErrorCodes.TokenInvalid);
                }

                if (session.ConsumedAt.HasValue)
                {
                    return ApiResponse<PublicParticipantSession>.BadRequest(
                        "این جلسه دیگر معتبر نیست. لطفاً دوباره مشخصات را وارد کنید",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (session.ExpiresAt <= now)
                {
                    return ApiResponse<PublicParticipantSession>.Unauthorized(
                        "جلسه منقضی شده است. لطفاً دوباره مشخصات را وارد کنید",
                        ErrorCodes.TokenExpired);
                }

                if (requirePhoneVerified && !session.PhoneVerifiedAt.HasValue)
                {
                    return ApiResponse<PublicParticipantSession>.BadRequest(
                        "ابتدا شماره موبایل خود را تأیید کنید",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                return ApiResponse<PublicParticipantSession>.CreateSuccess(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating public participant session for {ResourceType}/{ResourceId}", resourceType, resourceId);
                return ApiResponse<PublicParticipantSession>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task MarkPhoneVerifiedAsync(PublicParticipantSession session)
        {
            var now = DateTime.UtcNow;
            var sessionMinutes = Math.Clamp(_options.SessionMinutes, 5, 24 * 60);
            session.PhoneVerifiedAt = now;
            session.ExpiresAt = now.AddMinutes(sessionMinutes);
            session.UpdatedAt = now;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> TryMarkConsumedAsync(int sessionId)
        {
            var now = DateTime.UtcNow;
            var affected = await _context.PublicParticipantSessions
                .Where(s => s.Id == sessionId && !s.IsDeleted && s.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.ConsumedAt, now)
                    .SetProperty(s => s.UpdatedAt, now));

            return affected == 1;
        }

        private (string AccessToken, string TokenHash) GenerateTokenPair()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var accessToken = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return (accessToken, HashToken(accessToken));
        }

        private string HashToken(string accessToken)
        {
            var payload = Encoding.UTF8.GetBytes(_pepper + ":" + accessToken);
            var hash = SHA256.HashData(payload);
            return Convert.ToHexString(hash);
        }
    }
}
