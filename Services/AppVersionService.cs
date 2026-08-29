using System.Text.Json;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.AppVersion;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api_Vapp.Services
{
    /// <summary>
    /// چک آپدیت اپ موبایل + مدیریت سیاست نسخه در پنل ادمین.
    /// </summary>
    public class AppVersionService : IAppVersionService
    {
        private const string CacheKeyPrefix = "app_version_policy_";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly Api_Context _context;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _audit;
        private readonly ILogger<AppVersionService> _logger;

        public AppVersionService(
            Api_Context context,
            IMemoryCache cache,
            IAuditService audit,
            ILogger<AppVersionService> logger)
        {
            _context = context;
            _cache = cache;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<AppVersionCheckResponseDto>> CheckAsync(string platform, string currentVersion)
        {
            try
            {
                _logger.LogInformation(
                    "شروع چک آپدیت اپ — Platform: {Platform}, Current: {Current}",
                    platform,
                    currentVersion);

                if (!AppVersionPlatforms.IsValid(platform))
                {
                    return ApiResponse<AppVersionCheckResponseDto>.BadRequest(
                        "پلتفرم باید android یا ios باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (string.IsNullOrWhiteSpace(currentVersion))
                {
                    return ApiResponse<AppVersionCheckResponseDto>.BadRequest(
                        "ورژن فعلی اپ الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (!AppVersionComparer.TryParse(currentVersion, out _))
                {
                    return ApiResponse<AppVersionCheckResponseDto>.BadRequest(
                        "فرمت ورژن نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalizedPlatform = AppVersionPlatforms.Normalize(platform);
                var policy = await GetOrCreatePolicyAsync(normalizedPlatform, useCache: true);

                var updateType = policy.IsActive
                    ? AppVersionComparer.ResolveUpdateType(
                        currentVersion,
                        policy.MinSupportedVersion,
                        policy.LatestVersion)
                    : AppUpdateTypes.None;

                var response = new AppVersionCheckResponseDto
                {
                    UpdateType = updateType,
                    LatestVersion = policy.LatestVersion,
                    MinSupportedVersion = policy.MinSupportedVersion,
                    StoreUrl = policy.StoreUrl,
                    Title = string.IsNullOrWhiteSpace(policy.Title)
                        ? DefaultTitle(updateType)
                        : policy.Title,
                    Message = string.IsNullOrWhiteSpace(policy.Message)
                        ? DefaultMessage(updateType)
                        : policy.Message,
                    Changelog = ParseChangelog(policy.ChangelogJson)
                };

                _logger.LogInformation(
                    "پایان چک آپدیت اپ — Platform: {Platform}, UpdateType: {UpdateType}, Latest: {Latest}",
                    normalizedPlatform,
                    updateType,
                    policy.LatestVersion);

                return ApiResponse<AppVersionCheckResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در چک آپدیت اپ — Platform: {Platform}, Current: {Current}", platform, currentVersion);
                return ApiResponse<AppVersionCheckResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<List<AppVersionPolicyResponseDto>>> GetAllPoliciesAsync()
        {
            try
            {
                await EnsureDefaultPoliciesAsync();

                var policies = await _context.AppVersionPolicies.AsNoTracking()
                    .Where(p => !p.IsDeleted)
                    .OrderBy(p => p.Platform)
                    .ToListAsync();

                return ApiResponse<List<AppVersionPolicyResponseDto>>.CreateSuccess(
                    policies.Select(MapPolicy).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت سیاست‌های نسخه اپ");
                return ApiResponse<List<AppVersionPolicyResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AppVersionPolicyResponseDto>> GetPolicyByPlatformAsync(string platform)
        {
            try
            {
                if (!AppVersionPlatforms.IsValid(platform))
                {
                    return ApiResponse<AppVersionPolicyResponseDto>.BadRequest(
                        "پلتفرم باید android یا ios باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var policy = await GetOrCreatePolicyAsync(AppVersionPlatforms.Normalize(platform), useCache: false);
                return ApiResponse<AppVersionPolicyResponseDto>.CreateSuccess(MapPolicy(policy));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت سیاست نسخه اپ — Platform: {Platform}", platform);
                return ApiResponse<AppVersionPolicyResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AppVersionPolicyResponseDto>> UpdatePolicyAsync(
            string platform,
            UpdateAppVersionPolicyDto dto)
        {
            try
            {
                if (!AppVersionPlatforms.IsValid(platform))
                {
                    return ApiResponse<AppVersionPolicyResponseDto>.BadRequest(
                        "پلتفرم باید android یا ios باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var validationError = ValidateUpdateDto(dto);
                if (validationError != null)
                {
                    return ApiResponse<AppVersionPolicyResponseDto>.BadRequest(
                        validationError,
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var normalizedPlatform = AppVersionPlatforms.Normalize(platform);
                var policy = await GetOrCreatePolicyForUpdateAsync(normalizedPlatform);
                var before = Snapshot(policy);

                policy.LatestVersion = AppVersionComparer.Normalize(dto.LatestVersion);
                policy.MinSupportedVersion = AppVersionComparer.Normalize(dto.MinSupportedVersion);
                policy.StoreUrl = string.IsNullOrWhiteSpace(dto.StoreUrl) ? null : dto.StoreUrl.Trim();
                policy.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
                policy.Message = string.IsNullOrWhiteSpace(dto.Message) ? null : dto.Message.Trim();
                policy.ChangelogJson = SerializeChangelog(dto.Changelog);
                policy.IsActive = dto.IsActive ?? true;
                policy.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                InvalidateCache(normalizedPlatform);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AppVersionPolicyUpdated,
                    EntityType = AuditEntityTypes.AppVersionPolicy,
                    EntityId = policy.Id.ToString(),
                    Before = before,
                    After = Snapshot(policy)
                });

                _logger.LogInformation(
                    "سیاست نسخه اپ به‌روز شد — Platform: {Platform}, Latest: {Latest}, Min: {Min}",
                    policy.Platform, policy.LatestVersion, policy.MinSupportedVersion);

                return ApiResponse<AppVersionPolicyResponseDto>.CreateSuccess(
                    MapPolicy(policy),
                    "سیاست نسخه به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی سیاست نسخه اپ — Platform: {Platform}", platform);
                return ApiResponse<AppVersionPolicyResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<AppVersionPolicy> GetOrCreatePolicyAsync(string platform, bool useCache)
        {
            var cacheKey = CacheKeyPrefix + platform;
            if (useCache && _cache.TryGetValue(cacheKey, out AppVersionPolicy? cached) && cached != null)
                return cached;

            await EnsureDefaultPoliciesAsync();

            var policy = await _context.AppVersionPolicies.AsNoTracking()
                .FirstAsync(p => p.Platform == platform && !p.IsDeleted);

            if (useCache)
            {
                _cache.Set(cacheKey, policy, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl,
                    Size = 1
                });
            }

            return policy;
        }

        private async Task<AppVersionPolicy> GetOrCreatePolicyForUpdateAsync(string platform)
        {
            await EnsureDefaultPoliciesAsync();

            return await _context.AppVersionPolicies
                .FirstAsync(p => p.Platform == platform && !p.IsDeleted);
        }

        private async Task EnsureDefaultPoliciesAsync()
        {
            foreach (var platform in AppVersionPlatforms.All)
            {
                var exists = await _context.AppVersionPolicies
                    .AnyAsync(p => p.Platform == platform && !p.IsDeleted);

                if (exists)
                    continue;

                var softDeleted = await _context.AppVersionPolicies
                    .FirstOrDefaultAsync(p => p.Platform == platform);

                if (softDeleted != null)
                {
                    softDeleted.IsDeleted = false;
                    softDeleted.IsActive = true;
                    softDeleted.LatestVersion = "1.0.0";
                    softDeleted.MinSupportedVersion = "1.0.0";
                    softDeleted.UpdatedAt = DateTime.UtcNow;
                    continue;
                }

                _context.AppVersionPolicies.Add(CreateDefaultPolicy(platform));
            }

            if (_context.ChangeTracker.HasChanges())
                await _context.SaveChangesAsync();
        }

        private static AppVersionPolicy CreateDefaultPolicy(string platform) => new()
        {
            Platform = platform,
            LatestVersion = "1.0.0",
            MinSupportedVersion = "1.0.0",
            Title = "نسخه جدید آماده است",
            Message = "نسخه جدید اپ در دسترس است. می‌توانید هر زمان به‌روزرسانی کنید.",
            ChangelogJson = "[]",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        private static string? ValidateUpdateDto(UpdateAppVersionPolicyDto dto)
        {
            if (!AppVersionComparer.TryParse(dto.LatestVersion, out _))
                return "فرمت LatestVersion نامعتبر است";

            if (!AppVersionComparer.TryParse(dto.MinSupportedVersion, out _))
                return "فرمت MinSupportedVersion نامعتبر است";

            if (AppVersionComparer.Compare(dto.MinSupportedVersion, dto.LatestVersion) > 0)
                return "MinSupportedVersion نمی‌تواند بیشتر از LatestVersion باشد";

            if (!string.IsNullOrWhiteSpace(dto.StoreUrl) && dto.StoreUrl.Trim().Length > 1000)
                return "StoreUrl بیش از حد طولانی است";

            if (!string.IsNullOrWhiteSpace(dto.Title) && dto.Title.Trim().Length > 200)
                return "Title بیش از حد طولانی است";

            if (!string.IsNullOrWhiteSpace(dto.Message) && dto.Message.Trim().Length > 1000)
                return "Message بیش از حد طولانی است";

            if (dto.Changelog != null && dto.Changelog.Count > 50)
                return "تعداد آیتم‌های Changelog بیش از حد مجاز است";

            return null;
        }

        private static AppVersionPolicyResponseDto MapPolicy(AppVersionPolicy policy) => new()
        {
            Id = policy.Id,
            Platform = policy.Platform,
            LatestVersion = policy.LatestVersion,
            MinSupportedVersion = policy.MinSupportedVersion,
            StoreUrl = policy.StoreUrl,
            Title = policy.Title,
            Message = policy.Message,
            Changelog = ParseChangelog(policy.ChangelogJson),
            IsActive = policy.IsActive,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };

        private static List<string> ParseChangelog(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                var items = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                return items?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList()
                    ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private static string SerializeChangelog(List<string>? changelog)
        {
            var items = changelog?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Take(50)
                .ToList()
                ?? [];

            return JsonSerializer.Serialize(items);
        }

        private static string DefaultTitle(string updateType) =>
            updateType == AppUpdateTypes.Forced
                ? "به‌روزرسانی الزامی است"
                : "نسخه جدید آماده است";

        private static string DefaultMessage(string updateType) =>
            updateType == AppUpdateTypes.Forced
                ? "برای ادامه استفاده لطفاً اپ را به‌روزرسانی کنید."
                : "نسخه جدید اپ در دسترس است. می‌توانید هر زمان به‌روزرسانی کنید.";

        private static object Snapshot(AppVersionPolicy policy) => new
        {
            policy.Id,
            policy.Platform,
            policy.LatestVersion,
            policy.MinSupportedVersion,
            policy.StoreUrl,
            policy.Title,
            policy.Message,
            policy.ChangelogJson,
            policy.IsActive
        };

        private void InvalidateCache(string platform) =>
            _cache.Remove(CacheKeyPrefix + platform);
    }
}
