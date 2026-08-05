using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace Api_Vapp.Services
{
    /// <summary>
    /// مدیریت تعرفه SMS و قواعد پارت — خواندن کش‌شده، به‌روزرسانی ادمین، preview دقیق.
    /// </summary>
    public class SmsPricingService : ISmsPricingService
    {
        private const string SettingsCacheKey = "sms_pricing_settings_v1";
        private static readonly TimeSpan SettingsCacheTtl = TimeSpan.FromMinutes(5);

        private readonly Api_Context _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IAuditService _audit;
        private readonly ILogger<SmsPricingService> _logger;

        public SmsPricingService(
            Api_Context context,
            IMemoryCache cache,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            IAuditService audit,
            ILogger<SmsPricingService> logger)
        {
            _context = context;
            _cache = cache;
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
            _audit = audit;
            _logger = logger;
        }

        public async Task<SmsPricingRuntime> GetRuntimeAsync(CancellationToken cancellationToken = default)
        {
            var setting = await GetOrCreateSettingsAsync(useCache: true, cancellationToken);
            return ToRuntime(setting);
        }

        public async Task<ApiResponse<SmsPricingSettingResponseDto>> GetAdminSettingsAsync()
        {
            try
            {
                var setting = await GetOrCreateSettingsAsync();
                return ApiResponse<SmsPricingSettingResponseDto>.CreateSuccess(MapSetting(setting));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت تنظیمات تعرفه پیامک");
                return ApiResponse<SmsPricingSettingResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsPricingSettingResponseDto>> UpdateAdminSettingsAsync(UpdateSmsPricingSettingDto dto)
        {
            try
            {
                var validationError = ValidateBusinessRules(dto);
                if (validationError != null)
                {
                    return ApiResponse<SmsPricingSettingResponseDto>.BadRequest(
                        validationError,
                        errorCode: ErrorCodes.InvalidInput);
                }

                var setting = await GetOrCreateSettingsForUpdateAsync();
                var before = SnapshotForAudit(setting);

                ApplyDto(setting, dto);
                setting.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _cache.Remove(SettingsCacheKey);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.SmsPricingSettingUpdated,
                    EntityType = AuditEntityTypes.SmsPricingSetting,
                    EntityId = setting.Id.ToString(),
                    Before = before,
                    After = SnapshotForAudit(setting)
                });

                _logger.LogInformation(
                    "تنظیمات تعرفه پیامک به‌روز شد — Billing: {Billing}, CostPerPart: {Cost}, SpaceWeight: {SpaceWeight}",
                    setting.IsBillingEnabled, setting.CostPerPart, setting.SpaceCharWeight);

                return ApiResponse<SmsPricingSettingResponseDto>.CreateSuccess(
                    MapSetting(setting),
                    "تنظیمات تعرفه پیامک با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی تنظیمات تعرفه پیامک");
                return ApiResponse<SmsPricingSettingResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsPricingPreviewResponseDto>> PreviewAsync(SmsPricingPreviewRequestDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Content))
                {
                    return ApiResponse<SmsPricingPreviewResponseDto>.BadRequest(
                        "متن پیام الزامی است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                SmsPartsRules rules;
                decimal costPerPart;
                bool billingEnabled;
                bool serverDisabled;
                bool effectiveBilling;

                if (dto.DraftSettings != null)
                {
                    var draftError = ValidateBusinessRules(dto.DraftSettings);
                    if (draftError != null)
                    {
                        return ApiResponse<SmsPricingPreviewResponseDto>.BadRequest(
                            draftError,
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    rules = RulesFromDto(dto.DraftSettings);
                    costPerPart = Math.Round(dto.DraftSettings.CostPerPart, 2, MidpointRounding.AwayFromZero);
                    billingEnabled = dto.DraftSettings.IsBillingEnabled;
                    serverDisabled = IsServerWalletCheckDisabled();
                    effectiveBilling = billingEnabled && !serverDisabled;
                }
                else
                {
                    var runtime = await GetRuntimeAsync();
                    rules = runtime.Rules;
                    costPerPart = runtime.CostPerPart;
                    billingEnabled = runtime.IsBillingEnabled;
                    serverDisabled = runtime.ServerWalletCheckDisabled;
                    effectiveBilling = runtime.IsBillingEffectivelyEnabled;
                }

                var recipients = dto.RecipientsCount < 1 ? 1 : dto.RecipientsCount;
                var analysis = SmsPartsCalculator.Analyze(
                    dto.Content,
                    rules,
                    throwOnMaxPages: false,
                    includeOptOutOverride: dto.IncludeOptOutSuffix);

                var total = costPerPart * analysis.PartsCount * recipients;
                var preparedPreview = analysis.PreparedContent.Length > 400
                    ? analysis.PreparedContent[..400] + "…"
                    : analysis.PreparedContent;

                var note = !billingEnabled
                    ? "صورتحساب در تنظیمات ادمین خاموش است؛ هزینه فقط به‌صورت تخمینی نمایش داده می‌شود."
                    : serverDisabled
                        ? "صورتحساب ادمین روشن است اما kill-switch سرور (DisableWalletCheck) فعال است؛ کسر از کیف پول انجام نمی‌شود."
                        : "صورتحساب فعال است؛ هنگام ارسال موفق، مبلغ از کیف پول کسر می‌شود.";

                return ApiResponse<SmsPricingPreviewResponseDto>.CreateSuccess(new SmsPricingPreviewResponseDto
                {
                    Language = analysis.IsPersian ? "Persian" : "English",
                    IsPersian = analysis.IsPersian,
                    WeightedCharacterCount = analysis.WeightedCharacterCount,
                    RawTextElementCount = analysis.RawTextElementCount,
                    SpaceElementCount = analysis.SpaceElementCount,
                    EmojiElementCount = analysis.EmojiElementCount,
                    RegularElementCount = analysis.RegularElementCount,
                    PartsCount = analysis.PartsCount,
                    MaxPages = analysis.MaxPages,
                    ExceedsMaxPages = analysis.ExceedsMaxPages,
                    OptOutApplied = analysis.OptOutApplied,
                    PreparedContentPreview = preparedPreview,
                    CostPerPart = costPerPart,
                    RecipientsCount = recipients,
                    EstimatedTotalCost = total,
                    IsBillingEffectivelyEnabled = effectiveBilling,
                    BillingNote = note
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در پیش‌نمایش تعرفه پیامک");
                return ApiResponse<SmsPricingPreviewResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        #region Helpers

        private bool IsServerWalletCheckDisabled()
        {
            var environmentName = _hostEnvironment.EnvironmentName;
            return _configuration.GetValue<bool>($"{environmentName}:DisableWalletCheck", false);
        }

        private SmsPricingRuntime ToRuntime(SmsPricingSetting setting)
        {
            var serverDisabled = IsServerWalletCheckDisabled();
            return new SmsPricingRuntime
            {
                CostPerPart = setting.CostPerPart,
                IsBillingEnabled = setting.IsBillingEnabled,
                ServerWalletCheckDisabled = serverDisabled,
                IsBillingEffectivelyEnabled = setting.IsBillingEnabled && !serverDisabled,
                Rules = RulesFromSetting(setting)
            };
        }

        private static SmsPartsRules RulesFromSetting(SmsPricingSetting s) => new()
        {
            PersianFirstPageChars = s.PersianFirstPageChars,
            PersianSecondPageChars = s.PersianSecondPageChars,
            PersianOtherPagesChars = s.PersianOtherPagesChars,
            EnglishFirstPageChars = s.EnglishFirstPageChars,
            EnglishOtherPagesChars = s.EnglishOtherPagesChars,
            MaxPages = s.MaxPages,
            RegularCharWeight = s.RegularCharWeight,
            SpaceCharWeight = s.SpaceCharWeight,
            EmojiCharWeight = s.EmojiCharWeight,
            TrimContentBeforeCount = s.TrimContentBeforeCount,
            CountLeadingTrailingSpaces = s.CountLeadingTrailingSpaces,
            LanguageDetectionSampleLength = s.LanguageDetectionSampleLength,
            DefaultLanguageIsPersian = s.DefaultLanguageIsPersian,
            IncludeOptOutSuffixInCalculation = s.IncludeOptOutSuffixInCalculation,
            OptOutSuffix = s.OptOutSuffix
        };

        private static SmsPartsRules RulesFromDto(UpdateSmsPricingSettingDto d) => new()
        {
            PersianFirstPageChars = d.PersianFirstPageChars,
            PersianSecondPageChars = d.PersianSecondPageChars,
            PersianOtherPagesChars = d.PersianOtherPagesChars,
            EnglishFirstPageChars = d.EnglishFirstPageChars,
            EnglishOtherPagesChars = d.EnglishOtherPagesChars,
            MaxPages = d.MaxPages,
            RegularCharWeight = d.RegularCharWeight,
            SpaceCharWeight = d.SpaceCharWeight,
            EmojiCharWeight = d.EmojiCharWeight,
            TrimContentBeforeCount = d.TrimContentBeforeCount,
            CountLeadingTrailingSpaces = d.CountLeadingTrailingSpaces,
            LanguageDetectionSampleLength = d.LanguageDetectionSampleLength,
            DefaultLanguageIsPersian = d.DefaultLanguageIsPersian,
            IncludeOptOutSuffixInCalculation = d.IncludeOptOutSuffixInCalculation,
            OptOutSuffix = d.OptOutSuffix?.Trim() ?? "لغو11"
        };

        private SmsPricingSettingResponseDto MapSetting(SmsPricingSetting setting)
        {
            var serverDisabled = IsServerWalletCheckDisabled();
            return new SmsPricingSettingResponseDto
            {
                Id = setting.Id,
                IsBillingEnabled = setting.IsBillingEnabled,
                ServerWalletCheckDisabled = serverDisabled,
                IsBillingEffectivelyEnabled = setting.IsBillingEnabled && !serverDisabled,
                CostPerPart = setting.CostPerPart,
                PersianFirstPageChars = setting.PersianFirstPageChars,
                PersianSecondPageChars = setting.PersianSecondPageChars,
                PersianOtherPagesChars = setting.PersianOtherPagesChars,
                EnglishFirstPageChars = setting.EnglishFirstPageChars,
                EnglishOtherPagesChars = setting.EnglishOtherPagesChars,
                MaxPages = setting.MaxPages,
                RegularCharWeight = setting.RegularCharWeight,
                SpaceCharWeight = setting.SpaceCharWeight,
                EmojiCharWeight = setting.EmojiCharWeight,
                TrimContentBeforeCount = setting.TrimContentBeforeCount,
                CountLeadingTrailingSpaces = setting.CountLeadingTrailingSpaces,
                LanguageDetectionSampleLength = setting.LanguageDetectionSampleLength,
                DefaultLanguageIsPersian = setting.DefaultLanguageIsPersian,
                IncludeOptOutSuffixInCalculation = setting.IncludeOptOutSuffixInCalculation,
                OptOutSuffix = setting.OptOutSuffix,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };
        }

        private static string? ValidateBusinessRules(UpdateSmsPricingSettingDto dto)
        {
            if (dto.RegularCharWeight == 0 && dto.SpaceCharWeight == 0 && dto.EmojiCharWeight == 0)
                return "حداقل یکی از وزن‌های کاراکتر باید بزرگ‌تر از صفر باشد";

            if (string.IsNullOrWhiteSpace(dto.OptOutSuffix))
                return "پسوند لغو نمی‌تواند خالی باشد";

            return null;
        }

        private static void ApplyDto(SmsPricingSetting setting, UpdateSmsPricingSettingDto dto)
        {
            setting.IsBillingEnabled = dto.IsBillingEnabled;
            setting.CostPerPart = Math.Round(dto.CostPerPart, 2, MidpointRounding.AwayFromZero);
            setting.PersianFirstPageChars = dto.PersianFirstPageChars;
            setting.PersianSecondPageChars = dto.PersianSecondPageChars;
            setting.PersianOtherPagesChars = dto.PersianOtherPagesChars;
            setting.EnglishFirstPageChars = dto.EnglishFirstPageChars;
            setting.EnglishOtherPagesChars = dto.EnglishOtherPagesChars;
            setting.MaxPages = dto.MaxPages;
            setting.RegularCharWeight = dto.RegularCharWeight;
            setting.SpaceCharWeight = dto.SpaceCharWeight;
            setting.EmojiCharWeight = dto.EmojiCharWeight;
            setting.TrimContentBeforeCount = dto.TrimContentBeforeCount;
            setting.CountLeadingTrailingSpaces = dto.CountLeadingTrailingSpaces;
            setting.LanguageDetectionSampleLength = dto.LanguageDetectionSampleLength;
            setting.DefaultLanguageIsPersian = dto.DefaultLanguageIsPersian;
            setting.IncludeOptOutSuffixInCalculation = dto.IncludeOptOutSuffixInCalculation;
            setting.OptOutSuffix = dto.OptOutSuffix.Trim();
        }

        private static object SnapshotForAudit(SmsPricingSetting s) => new
        {
            s.IsBillingEnabled,
            s.CostPerPart,
            s.PersianFirstPageChars,
            s.PersianSecondPageChars,
            s.PersianOtherPagesChars,
            s.EnglishFirstPageChars,
            s.EnglishOtherPagesChars,
            s.MaxPages,
            s.RegularCharWeight,
            s.SpaceCharWeight,
            s.EmojiCharWeight,
            s.TrimContentBeforeCount,
            s.CountLeadingTrailingSpaces,
            s.LanguageDetectionSampleLength,
            s.DefaultLanguageIsPersian,
            s.IncludeOptOutSuffixInCalculation,
            s.OptOutSuffix
        };

        private async Task<SmsPricingSetting> GetOrCreateSettingsAsync(
            bool useCache = true,
            CancellationToken cancellationToken = default)
        {
            if (useCache && _cache.TryGetValue(SettingsCacheKey, out SmsPricingSetting? cached) && cached != null)
                return cached;

            var setting = await _context.SmsPricingSettings
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null)
            {
                var created = CreateDefaultEntity();
                await _context.SmsPricingSettings.AddAsync(created, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                setting = created;
            }

            var snapshot = Clone(setting);
            _cache.Set(SettingsCacheKey, snapshot, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SettingsCacheTtl,
                Size = 1
            });
            return snapshot;
        }

        private async Task<SmsPricingSetting> GetOrCreateSettingsForUpdateAsync()
        {
            _cache.Remove(SettingsCacheKey);

            var setting = await _context.SmsPricingSettings
                .AsTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (setting != null)
                return setting;

            setting = CreateDefaultEntity();
            await _context.SmsPricingSettings.AddAsync(setting);
            await _context.SaveChangesAsync();
            return setting;
        }

        private static SmsPricingSetting CreateDefaultEntity() => new()
        {
            IsBillingEnabled = true,
            CostPerPart = 160m,
            PersianFirstPageChars = 70,
            PersianSecondPageChars = 64,
            PersianOtherPagesChars = 67,
            EnglishFirstPageChars = 160,
            EnglishOtherPagesChars = 153,
            MaxPages = 10,
            RegularCharWeight = 1,
            SpaceCharWeight = 1,
            EmojiCharWeight = 3,
            TrimContentBeforeCount = true,
            CountLeadingTrailingSpaces = true,
            LanguageDetectionSampleLength = 50,
            DefaultLanguageIsPersian = true,
            IncludeOptOutSuffixInCalculation = true,
            OptOutSuffix = "لغو11",
            CreatedAt = DateTime.UtcNow
        };

        private static SmsPricingSetting Clone(SmsPricingSetting s) => new()
        {
            Id = s.Id,
            IsBillingEnabled = s.IsBillingEnabled,
            CostPerPart = s.CostPerPart,
            PersianFirstPageChars = s.PersianFirstPageChars,
            PersianSecondPageChars = s.PersianSecondPageChars,
            PersianOtherPagesChars = s.PersianOtherPagesChars,
            EnglishFirstPageChars = s.EnglishFirstPageChars,
            EnglishOtherPagesChars = s.EnglishOtherPagesChars,
            MaxPages = s.MaxPages,
            RegularCharWeight = s.RegularCharWeight,
            SpaceCharWeight = s.SpaceCharWeight,
            EmojiCharWeight = s.EmojiCharWeight,
            TrimContentBeforeCount = s.TrimContentBeforeCount,
            CountLeadingTrailingSpaces = s.CountLeadingTrailingSpaces,
            LanguageDetectionSampleLength = s.LanguageDetectionSampleLength,
            DefaultLanguageIsPersian = s.DefaultLanguageIsPersian,
            IncludeOptOutSuffixInCalculation = s.IncludeOptOutSuffixInCalculation,
            OptOutSuffix = s.OptOutSuffix,
            IsDeleted = s.IsDeleted,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };

        #endregion
    }
}
