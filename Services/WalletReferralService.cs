using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api_Vapp.Services
{
    /// <summary>
    /// منطق مالی و امن سیستم معرفی برای شارژ کیف پول
    /// </summary>
    public class WalletReferralService : IWalletReferralService
    {
        private const string SettingsCacheKey = "wallet_referral_settings_v1";
        private static readonly TimeSpan SettingsCacheTtl = TimeSpan.FromMinutes(5);
        private const decimal MinPayableAmount = 1000m;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly Api_Context _context;
        private readonly IUserRepository _userRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _audit;
        private readonly ILogger<WalletReferralService> _logger;

        public WalletReferralService(
            Api_Context context,
            IUserRepository userRepository,
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            IAuditService audit,
            ILogger<WalletReferralService> logger)
        {
            _context = context;
            _userRepository = userRepository;
            _serviceProvider = serviceProvider;
            _cache = cache;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<WalletReferralInfoDto>> GetReferralInfoAsync(int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return ApiResponse<WalletReferralInfoDto>.NotFound("کاربر یافت نشد");

                var code = await EnsureReferralCodeAsync(user);
                var setting = await GetOrCreateSettingsAsync();

                return ApiResponse<WalletReferralInfoDto>.CreateSuccess(new WalletReferralInfoDto
                {
                    ReferralCode = code,
                    IsEnabled = setting.IsEnabled,
                    DiscountPercent = setting.DiscountPercent,
                    BonusPercent = setting.BonusPercent,
                    Description = BuildDescription(setting)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت اطلاعات رفرال کاربر {UserId}", userId);
                return ApiResponse<WalletReferralInfoDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ValidateWalletReferralResponseDto>> ValidateReferralAsync(
            int userId,
            ValidateWalletReferralRequestDto request)
        {
            try
            {
                var resolved = await ResolveReferralForChargeAsync(userId, request.Amount, request.ReferralCode);
                if (!resolved.Success)
                {
                    return ApiResponse<ValidateWalletReferralResponseDto>.BadRequest(
                        resolved.Message,
                        resolved.Errors,
                        resolved.ErrorCode);
                }

                var meta = resolved.Data;
                if (meta == null)
                {
                    return ApiResponse<ValidateWalletReferralResponseDto>.BadRequest(
                        "کد معرفی نامعتبر است",
                        errorCode: ErrorCodes.ReferralInvalid);
                }

                return ApiResponse<ValidateWalletReferralResponseDto>.CreateSuccess(new ValidateWalletReferralResponseDto
                {
                    IsValid = true,
                    ReferralCode = meta.ReferralCode,
                    RequestedAmount = meta.RequestedAmount,
                    DiscountPercent = meta.DiscountPercent,
                    DiscountAmount = meta.DiscountAmount,
                    PayableAmount = meta.PayableAmount,
                    BonusPercent = meta.BonusPercent,
                    BonusAmount = meta.BonusAmount,
                    FormattedRequestedAmount = FormatAmount(meta.RequestedAmount),
                    FormattedDiscountAmount = FormatAmount(meta.DiscountAmount),
                    FormattedPayableAmount = FormatAmount(meta.PayableAmount),
                    FormattedBonusAmount = FormatAmount(meta.BonusAmount),
                    Message = $"با اعمال کد معرفی، {FormatAmount(meta.DiscountAmount)} تخفیف دریافت می‌کنید"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در اعتبارسنجی کد رفرال کاربر {UserId}", userId);
                return ApiResponse<ValidateWalletReferralResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<WalletReferralPaymentMetaDto?>> ResolveReferralForChargeAsync(
            int userId,
            decimal requestedAmount,
            string? referralCode)
        {
            if (string.IsNullOrWhiteSpace(referralCode))
                return ApiResponse<WalletReferralPaymentMetaDto?>.CreateSuccess(null);

            var setting = await GetOrCreateSettingsAsync();
            if (!setting.IsEnabled)
            {
                return ApiResponse<WalletReferralPaymentMetaDto?>.BadRequest(
                    "سیستم معرفی در حال حاضر غیرفعال است",
                    errorCode: ErrorCodes.ReferralDisabled);
            }

            var normalized = NormalizeReferralCode(referralCode);
            if (string.IsNullOrEmpty(normalized))
            {
                return ApiResponse<WalletReferralPaymentMetaDto?>.BadRequest(
                    "کد معرفی نامعتبر است",
                    errorCode: ErrorCodes.ReferralInvalid);
            }

            var referrer = await _userRepository.GetByReferralCodeAsync(normalized);
            if (referrer == null || !referrer.IsActive || referrer.IsDeleted)
            {
                return ApiResponse<WalletReferralPaymentMetaDto?>.BadRequest(
                    "کد معرفی نامعتبر است",
                    errorCode: ErrorCodes.ReferralInvalid);
            }

            if (referrer.Id == userId)
            {
                return ApiResponse<WalletReferralPaymentMetaDto?>.BadRequest(
                    "نمی‌توانید از کد معرفی خودتان استفاده کنید",
                    errorCode: ErrorCodes.ReferralSelfUse);
            }

            var discountAmount = CalculatePercentAmount(requestedAmount, setting.DiscountPercent);
            var bonusAmount = CalculatePercentAmount(requestedAmount, setting.BonusPercent);
            var payableAmount = requestedAmount - discountAmount;

            if (setting.DiscountPercent < 0 || setting.DiscountPercent > 100
                || setting.BonusPercent < 0 || setting.BonusPercent > 100)
            {
                return ApiResponse<WalletReferralPaymentMetaDto?>.BadRequest(
                    "تنظیمات معرفی نامعتبر است. لطفاً با پشتیبانی تماس بگیرید",
                    errorCode: ErrorCodes.Unexpected);
            }

            if (payableAmount < MinPayableAmount)
            {
                return ApiResponse<WalletReferralPaymentMetaDto?>.BadRequest(
                    "مبلغ قابل پرداخت پس از تخفیف معتبر نیست. مبلغ شارژ را افزایش دهید",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var meta = new WalletReferralPaymentMetaDto
            {
                ReferralCode = normalized,
                ReferrerUserId = referrer.Id,
                RequestedAmount = requestedAmount,
                PayableAmount = payableAmount,
                DiscountAmount = discountAmount,
                DiscountPercent = setting.DiscountPercent,
                BonusAmount = bonusAmount,
                BonusPercent = setting.BonusPercent
            };

            return ApiResponse<WalletReferralPaymentMetaDto?>.CreateSuccess(meta);
        }

        public async Task FulfillWalletChargeWithReferralAsync(Payment payment)
        {
            if (payment.PaymentType != PaymentTypes.WalletCharge)
                return;

            var rawMeta = TryParseReferralMeta(payment.MetaData);
            var meta = rawMeta != null && IsIntegrityValidReferralMeta(payment, rawMeta)
                ? rawMeta
                : null;

            if (rawMeta != null && meta == null)
            {
                _logger.LogWarning(
                    "MetaData رفرال نامعتبر نادیده گرفته شد — PaymentId: {PaymentId}, UserId: {UserId}",
                    payment.Id, payment.UserId);
            }

            // فقط در صورت MetaData معتبر، مبلغ درخواستی (با تخفیف) واریز می‌شود؛ وگرنه مبلغ درگاه
            var creditAmount = meta?.RequestedAmount > 0 ? meta.RequestedAmount : payment.Amount;

            if (creditAmount <= 0)
            {
                _logger.LogWarning("مبلغ اعتبار شارژ کیف پول نامعتبر است — PaymentId: {PaymentId}", payment.Id);
                return;
            }

            var walletService = _serviceProvider.GetRequiredService<IWalletService>();

            // جلوگیری از واریز تکراری شارژ به ذینفع
            var alreadyCredited = await _context.WalletTransactions.AsNoTracking()
                .AnyAsync(t =>
                    t.PaymentId == payment.Id
                    && t.UserId == payment.UserId
                    && t.TransactionType == WalletTransactionTypes.Deposit
                    && t.Status == TransactionStatuses.Completed);

            if (!alreadyCredited)
            {
                var depositTitle = meta != null
                    ? "شارژ کیف پول با کد معرفی"
                    : "شارژ کیف پول";
                var depositDesc = meta != null
                    ? $"پرداخت {payment.Amount:N0} تومان از درگاه + تخفیف معرفی {meta.DiscountAmount:N0} تومان"
                    : $"پرداخت از طریق درگاه — مبلغ {payment.Amount:N0} تومان";

                var depositResult = await walletService.AddBalanceAsync(
                    payment.UserId,
                    creditAmount,
                    WalletTransactionTypes.Deposit,
                    depositTitle,
                    depositDesc,
                    payment.Id,
                    null,
                    payment.ReferenceNumber);

                if (!depositResult.Success)
                {
                    _logger.LogError(
                        "واریز شارژ کیف پول ناموفق — PaymentId: {PaymentId}, UserId: {UserId}, Message: {Message}",
                        payment.Id, payment.UserId, depositResult.Message);
                    throw new InvalidOperationException("Wallet deposit failed during payment fulfillment");
                }
            }

            if (meta == null || meta.BonusAmount <= 0)
                return;

            await FulfillReferrerBonusSafelyAsync(payment, meta, walletService);
        }

        /// <summary>
        /// پاداش معرف: ابتدا اسلات یکتا (PaymentId) رزرو می‌شود تا race منجر به واریز دوبل نشود.
        /// </summary>
        private async Task FulfillReferrerBonusSafelyAsync(
            Payment payment,
            WalletReferralPaymentMetaDto meta,
            IWalletService walletService)
        {
            var existingReward = await _context.WalletReferralRewards
                .FirstOrDefaultAsync(r => r.PaymentId == payment.Id && !r.IsDeleted);

            if (existingReward != null)
            {
                // تعمیر: اگر رکورد هست ولی واریز پاداش جا مانده
                if (existingReward.ReferrerWalletTransactionId == null)
                    await CreditReferrerBonusIfMissingAsync(payment, meta, existingReward, walletService);
                return;
            }

            var alreadyBonused = await _context.WalletTransactions.AsNoTracking()
                .AnyAsync(t =>
                    t.PaymentId == payment.Id
                    && t.UserId == meta.ReferrerUserId
                    && t.TransactionType == WalletTransactionTypes.ReferralBonus
                    && t.Status == TransactionStatuses.Completed);
            if (alreadyBonused)
                return;

            var referrer = await _userRepository.GetByIdAsync(meta.ReferrerUserId);
            if (referrer == null || !referrer.IsActive)
            {
                _logger.LogWarning(
                    "معرف برای پاداش یافت نشد یا غیرفعال است — PaymentId: {PaymentId}, ReferrerUserId: {ReferrerUserId}",
                    payment.Id, meta.ReferrerUserId);
                return;
            }

            // رزرو اسلات یکتا قبل از واریز — بازندهٔ race بدون واریز خارج می‌شود
            var reward = new WalletReferralReward
            {
                PaymentId = payment.Id,
                BeneficiaryUserId = payment.UserId,
                ReferrerUserId = meta.ReferrerUserId,
                ReferralCode = meta.ReferralCode,
                RequestedAmount = meta.RequestedAmount,
                PayableAmount = meta.PayableAmount,
                DiscountAmount = meta.DiscountAmount,
                DiscountPercent = meta.DiscountPercent,
                BonusAmount = meta.BonusAmount,
                BonusPercent = meta.BonusPercent,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _context.WalletReferralRewards.AddAsync(reward);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex,
                    "رزرو پاداش معرفی تکراری نادیده گرفته شد — PaymentId: {PaymentId}", payment.Id);
                return;
            }

            await CreditReferrerBonusIfMissingAsync(payment, meta, reward, walletService);
        }

        private async Task CreditReferrerBonusIfMissingAsync(
            Payment payment,
            WalletReferralPaymentMetaDto meta,
            WalletReferralReward reward,
            IWalletService walletService)
        {
            if (reward.ReferrerWalletTransactionId.HasValue)
                return;

            var alreadyBonused = await _context.WalletTransactions.AsNoTracking()
                .AnyAsync(t =>
                    t.PaymentId == payment.Id
                    && t.UserId == meta.ReferrerUserId
                    && t.TransactionType == WalletTransactionTypes.ReferralBonus
                    && t.Status == TransactionStatuses.Completed);

            if (alreadyBonused)
            {
                var existingTxId = await _context.WalletTransactions.AsNoTracking()
                    .Where(t =>
                        t.PaymentId == payment.Id
                        && t.UserId == meta.ReferrerUserId
                        && t.TransactionType == WalletTransactionTypes.ReferralBonus
                        && t.Status == TransactionStatuses.Completed)
                    .Select(t => (int?)t.Id)
                    .FirstOrDefaultAsync();

                if (existingTxId.HasValue && reward.ReferrerWalletTransactionId != existingTxId)
                {
                    reward.ReferrerWalletTransactionId = existingTxId;
                    reward.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                return;
            }

            var bonusResult = await walletService.AddBalanceAsync(
                meta.ReferrerUserId,
                meta.BonusAmount,
                WalletTransactionTypes.ReferralBonus,
                "پاداش معرفی",
                $"پاداش معرفی بابت شارژ کاربر با کد {meta.ReferralCode}",
                payment.Id,
                null,
                payment.ReferenceNumber);

            if (!bonusResult.Success)
            {
                _logger.LogError(
                    "واریز پاداش معرفی ناموفق — PaymentId: {PaymentId}, ReferrerUserId: {ReferrerUserId}, Message: {Message}",
                    payment.Id, meta.ReferrerUserId, bonusResult.Message);
                throw new InvalidOperationException("Referral bonus deposit failed");
            }

            reward.ReferrerWalletTransactionId = bonusResult.Data?.Id;
            reward.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Wallet,
                Action = AuditActions.WalletReferralRewardPaid,
                EntityType = AuditEntityTypes.WalletReferralReward,
                EntityId = reward.Id.ToString(),
                TargetUserId = meta.ReferrerUserId,
                After = new
                {
                    paymentId = payment.Id,
                    beneficiaryUserId = payment.UserId,
                    referrerUserId = meta.ReferrerUserId,
                    referralCode = meta.ReferralCode,
                    requestedAmount = meta.RequestedAmount,
                    payableAmount = meta.PayableAmount,
                    discountAmount = meta.DiscountAmount,
                    bonusAmount = meta.BonusAmount
                }
            });

            _logger.LogInformation(
                "پاداش معرفی واریز شد — PaymentId: {PaymentId}, ReferrerUserId: {ReferrerUserId}, Bonus: {BonusAmount}",
                payment.Id, meta.ReferrerUserId, meta.BonusAmount);
        }

        public async Task<ApiResponse<WalletReferralSettingResponseDto>> GetAdminSettingsAsync()
        {
            try
            {
                var setting = await GetOrCreateSettingsAsync();
                return ApiResponse<WalletReferralSettingResponseDto>.CreateSuccess(MapSetting(setting));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت تنظیمات رفرال ادمین");
                return ApiResponse<WalletReferralSettingResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<WalletReferralSettingResponseDto>> UpdateAdminSettingsAsync(
            UpdateWalletReferralSettingDto dto)
        {
            try
            {
                if (dto.DiscountPercent + dto.BonusPercent > 100)
                {
                    return ApiResponse<WalletReferralSettingResponseDto>.BadRequest(
                        "جمع درصد تخفیف و پاداش نمی‌تواند بیشتر از ۱۰۰ باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var setting = await GetOrCreateSettingsForUpdateAsync();
                var before = new
                {
                    setting.IsEnabled,
                    setting.DiscountPercent,
                    setting.BonusPercent,
                    setting.DescriptionTemplate
                };

                setting.IsEnabled = dto.IsEnabled;
                setting.DiscountPercent = Math.Round(dto.DiscountPercent, 2, MidpointRounding.AwayFromZero);
                setting.BonusPercent = Math.Round(dto.BonusPercent, 2, MidpointRounding.AwayFromZero);
                if (!string.IsNullOrWhiteSpace(dto.DescriptionTemplate))
                    setting.DescriptionTemplate = dto.DescriptionTemplate.Trim();
                setting.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _cache.Remove(SettingsCacheKey);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.WalletReferralSettingUpdated,
                    EntityType = AuditEntityTypes.WalletReferralSetting,
                    EntityId = setting.Id.ToString(),
                    Before = before,
                    After = new
                    {
                        setting.IsEnabled,
                        setting.DiscountPercent,
                        setting.BonusPercent,
                        setting.DescriptionTemplate
                    }
                });

                _logger.LogInformation(
                    "تنظیمات رفرال به‌روز شد — Enabled: {IsEnabled}, Discount: {Discount}, Bonus: {Bonus}",
                    setting.IsEnabled, setting.DiscountPercent, setting.BonusPercent);

                return ApiResponse<WalletReferralSettingResponseDto>.CreateSuccess(
                    MapSetting(setting),
                    "تنظیمات معرفی با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی تنظیمات رفرال ادمین");
                return ApiResponse<WalletReferralSettingResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<string> EnsureReferralCodeAsync(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.ReferralCode))
                return user.ReferralCode;

            for (var attempt = 0; attempt < 12; attempt++)
            {
                var candidate = GenerateReferralCodeCandidate(user, attempt);
                var exists = await _userRepository.ExistsByReferralCodeAsync(candidate);
                if (exists)
                    continue;

                user.ReferralCode = candidate;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return candidate;
            }

            // fallback قطعی یکتا
            var fallback = $"@u{user.Id}{DateTime.UtcNow:HHmmss}";
            user.ReferralCode = fallback;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return fallback;
        }

        public string BuildDescription(WalletReferralSetting setting)
        {
            var template = string.IsNullOrWhiteSpace(setting.DescriptionTemplate)
                ? "کافیه کاربر معرفی‌شده این کد رو موقع شارژ کیف پول وارد کنه؛ در این صورت {DiscountPercent}٪ تخفیف براشون اعمال می‌شه و {BonusPercent}٪ پاداش هم به شما واریز می‌شه."
                : setting.DescriptionTemplate;

            return template
                .Replace("{DiscountPercent}", FormatPercent(setting.DiscountPercent), StringComparison.OrdinalIgnoreCase)
                .Replace("{BonusPercent}", FormatPercent(setting.BonusPercent), StringComparison.OrdinalIgnoreCase);
        }

        #region Helpers

        private async Task<WalletReferralSetting> GetOrCreateSettingsAsync(bool useCache = true)
        {
            if (useCache && _cache.TryGetValue(SettingsCacheKey, out WalletReferralSetting? cached) && cached != null)
                return cached;

            var setting = await _context.WalletReferralSettings
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (setting == null)
            {
                var created = new WalletReferralSetting
                {
                    IsEnabled = true,
                    DiscountPercent = 10m,
                    BonusPercent = 10m,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.WalletReferralSettings.AddAsync(created);
                await _context.SaveChangesAsync();
                setting = created;
            }

            // اسنپ‌شات جدا از DbContext تا entity track‌شده کش نشود
            var snapshot = new WalletReferralSetting
            {
                Id = setting.Id,
                IsEnabled = setting.IsEnabled,
                DiscountPercent = setting.DiscountPercent,
                BonusPercent = setting.BonusPercent,
                DescriptionTemplate = setting.DescriptionTemplate,
                IsDeleted = setting.IsDeleted,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };

            _cache.Set(SettingsCacheKey, snapshot, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SettingsCacheTtl,
                Size = 1
            });
            return snapshot;
        }

        /// <summary>
        /// برای به‌روزرسانی ادمین باید entity قابل track لود شود.
        /// </summary>
        private async Task<WalletReferralSetting> GetOrCreateSettingsForUpdateAsync()
        {
            _cache.Remove(SettingsCacheKey);

            var setting = await _context.WalletReferralSettings
                .AsTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (setting != null)
                return setting;

            setting = new WalletReferralSetting
            {
                IsEnabled = true,
                DiscountPercent = 10m,
                BonusPercent = 10m,
                CreatedAt = DateTime.UtcNow
            };
            await _context.WalletReferralSettings.AddAsync(setting);
            await _context.SaveChangesAsync();
            return setting;
        }

        /// <summary>
        /// صحت مالی MetaData در برابر مبلغ درگاه و مالک پرداخت — جلوگیری از دستکاری
        /// </summary>
        public static bool IsIntegrityValidReferralMeta(Payment payment, WalletReferralPaymentMetaDto meta)
        {
            if (meta.ReferrerUserId <= 0 || meta.ReferrerUserId == payment.UserId)
                return false;

            if (meta.RequestedAmount <= 0 || meta.PayableAmount <= 0)
                return false;

            if (meta.DiscountAmount < 0 || meta.BonusAmount < 0)
                return false;

            if (meta.DiscountPercent < 0 || meta.DiscountPercent > 100)
                return false;

            if (meta.BonusPercent < 0 || meta.BonusPercent > 100)
                return false;

            // مبلغ درگاه باید دقیقاً با payable قفل‌شده یکی باشد
            if (meta.PayableAmount != payment.Amount)
                return false;

            // requested = payable + discount
            if (meta.RequestedAmount != meta.PayableAmount + meta.DiscountAmount)
                return false;

            // پاداش نباید از مبلغ شارژ بیشتر باشد
            if (meta.BonusAmount > meta.RequestedAmount)
                return false;

            // هم‌خوانی درصد با مبلغ (با همان فرمول گرد کردن)
            var expectedDiscount = CalculatePercentAmount(meta.RequestedAmount, meta.DiscountPercent);
            var expectedBonus = CalculatePercentAmount(meta.RequestedAmount, meta.BonusPercent);
            if (meta.DiscountAmount != expectedDiscount || meta.BonusAmount != expectedBonus)
                return false;

            return true;
        }

        public static string NormalizeReferralCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            var trimmed = code.Trim();
            // حذف @ اضافی و فاصله
            trimmed = Regex.Replace(trimmed, @"\s+", "");
            if (!trimmed.StartsWith('@'))
                trimmed = "@" + trimmed.TrimStart('@');

            return trimmed.ToLowerInvariant();
        }

        public static decimal CalculatePercentAmount(decimal amount, decimal percent)
        {
            if (amount <= 0 || percent <= 0)
                return 0m;

            return Math.Round(amount * percent / 100m, 0, MidpointRounding.AwayFromZero);
        }

        public static WalletReferralPaymentMetaDto? TryParseReferralMeta(string? metaData)
        {
            if (string.IsNullOrWhiteSpace(metaData))
                return null;

            try
            {
                var wrapper = JsonSerializer.Deserialize<WalletChargePaymentMetaDto>(metaData, JsonOptions);
                var meta = wrapper?.WalletReferral;
                if (meta == null || meta.ReferrerUserId <= 0 || meta.RequestedAmount <= 0)
                    return null;
                return meta;
            }
            catch
            {
                return null;
            }
        }

        public static string SerializeChargeMeta(WalletReferralPaymentMetaDto? referralMeta)
        {
            if (referralMeta == null)
                return JsonSerializer.Serialize(new WalletChargePaymentMetaDto(), JsonOptions);

            return JsonSerializer.Serialize(new WalletChargePaymentMetaDto
            {
                WalletReferral = referralMeta
            }, JsonOptions);
        }

        private static string GenerateReferralCodeCandidate(User user, int attempt)
        {
            var slug = BuildSlugFromName(user.FullName);
            if (string.IsNullOrEmpty(slug))
                slug = "user" + user.Id;

            if (slug.Length > 12)
                slug = slug[..12];

            var suffix = Random.Shared.Next(1000, 9999);
            if (attempt > 0)
                suffix = Random.Shared.Next(10000, 99999);

            return ("@" + slug + suffix).ToLowerInvariant();
        }

        private static string BuildSlugFromName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var ch in fullName.Trim().ToLowerInvariant())
            {
                if (ch is >= 'a' and <= 'z')
                {
                    sb.Append(ch);
                    continue;
                }

                var mapped = MapPersianChar(ch);
                if (mapped != null)
                    sb.Append(mapped);
            }

            return sb.ToString();
        }

        private static string? MapPersianChar(char ch) => ch switch
        {
            'ا' or 'آ' or 'أ' or 'إ' or 'ع' => "a",
            'ب' => "b",
            'پ' => "p",
            'ت' or 'ط' => "t",
            'ث' or 'س' or 'ص' => "s",
            'ج' => "j",
            'چ' => "ch",
            'ح' or 'ه' or 'ة' => "h",
            'خ' => "kh",
            'د' => "d",
            'ذ' or 'ز' or 'ض' or 'ظ' => "z",
            'ر' => "r",
            'ژ' => "zh",
            'ش' => "sh",
            'غ' => "gh",
            'ف' => "f",
            'ق' => "gh",
            'ک' or 'ك' => "k",
            'گ' => "g",
            'ل' => "l",
            'م' => "m",
            'ن' => "n",
            'و' => "v",
            'ی' or 'ي' or 'ئ' => "i",
            _ => null
        };

        private WalletReferralSettingResponseDto MapSetting(WalletReferralSetting setting)
        {
            return new WalletReferralSettingResponseDto
            {
                Id = setting.Id,
                IsEnabled = setting.IsEnabled,
                DiscountPercent = setting.DiscountPercent,
                BonusPercent = setting.BonusPercent,
                DescriptionTemplate = setting.DescriptionTemplate,
                DescriptionPreview = BuildDescription(setting),
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };
        }

        private static string FormatAmount(decimal amount) => $"{amount:N0} تومان";

        private static string FormatPercent(decimal percent)
        {
            return percent == Math.Truncate(percent)
                ? ((int)percent).ToString()
                : percent.ToString("0.##");
        }

        #endregion
    }
}
