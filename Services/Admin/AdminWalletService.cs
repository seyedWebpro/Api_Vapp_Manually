using System.Text;
using System.Text.RegularExpressions;
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
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.Admin
{
    /// <summary>
    /// سرویس کیف پول ادمین — مشاهده موجودی، تاریخچه و شارژ دستی با کنترل حسابداری
    /// </summary>
    public class AdminWalletService : IAdminWalletService
    {
        private const decimal MaxManualChargeAmount = 100_000_000m;
        private const decimal MaxWalletBalance = 999_999_999_999m;
        private const string ManualChargeTitle = "شارژ دستی ادمین";
        private const string ManualDeductTitle = "کسر دستی ادمین";
        private const string DuplicateMessage = "تراکنش قبلاً ثبت شده است";

        private static readonly Regex SafeIdempotencyKeyRegex =
            new(@"^[A-Za-z0-9\-_]{8,64}$", RegexOptions.Compiled);

        private readonly Api_Context _context;
        private readonly IWalletService _walletService;
        private readonly IWalletRepository _walletRepository;
        private readonly IAuditService _audit;
        private readonly ILogger<AdminWalletService> _logger;

        public AdminWalletService(
            Api_Context context,
            IWalletService walletService,
            IWalletRepository walletRepository,
            IAuditService audit,
            ILogger<AdminWalletService> logger)
        {
            _context = context;
            _walletService = walletService;
            _walletRepository = walletRepository;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<AdminWalletBalanceDto>> GetBalanceAsync(int userId)
        {
            if (userId <= 0)
            {
                return ApiResponse<AdminWalletBalanceDto>.BadRequest(
                    "شناسه کاربر نامعتبر است",
                    errorCode: ErrorCodes.InvalidUserId);
            }

            // بدون cache — موجودی مالی همیشه live
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.PhoneNumber,
                    u.IsActive,
                    u.WalletBalance,
                    u.UpdatedAt,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return ApiResponse<AdminWalletBalanceDto>.NotFound("کاربر یافت نشد");

            var txCount = await _walletRepository.GetCountByUserIdAsync(userId);

            return ApiResponse<AdminWalletBalanceDto>.CreateSuccess(new AdminWalletBalanceDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                Balance = user.WalletBalance,
                FormattedBalance = FormatAmount(user.WalletBalance),
                TotalTransactionsCount = txCount,
                LastUpdatedAt = user.UpdatedAt ?? user.CreatedAt
            });
        }

        public async Task<ApiResponse<WalletTransactionListDto>> GetTransactionsAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 20)
        {
            if (userId <= 0)
            {
                return ApiResponse<WalletTransactionListDto>.BadRequest(
                    "شناسه کاربر نامعتبر است",
                    errorCode: ErrorCodes.InvalidUserId);
            }

            var exists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId && !u.IsDeleted);

            if (!exists)
                return ApiResponse<WalletTransactionListDto>.NotFound("کاربر یافت نشد");

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            return await _walletService.GetTransactionsAsync(userId, pageNumber, pageSize);
        }

        public async Task<ApiResponse<AdminManualChargeResponseDto>> ManualChargeAsync(
            int adminUserId,
            int userId,
            AdminManualChargeRequestDto dto)
        {
            if (adminUserId <= 0)
            {
                return ApiResponse<AdminManualChargeResponseDto>.Unauthorized(
                    ControlledErrorHelper.Unauthorized,
                    ErrorCodes.Unauthorized);
            }

            if (userId <= 0)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "شناسه کاربر نامعتبر است",
                    errorCode: ErrorCodes.InvalidUserId);
            }

            if (dto == null)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var amount = dto.Amount;
            if (amount <= 0 || amount > MaxManualChargeAmount)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "مبلغ شارژ باید بین ۱ تا ۱۰۰٬۰۰۰٬۰۰۰ تومان باشد",
                    errorCode: ErrorCodes.InvalidInput);
            }

            // فقط عدد صحیح تومان — از خطای اعشاری در حسابداری جلوگیری می‌کند
            if (amount != decimal.Truncate(amount))
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "مبلغ شارژ باید عدد صحیح تومان باشد",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var description = SanitizeDescription(dto.Description);
            if (description.Length < 5)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "دلیل شارژ باید حداقل ۵ کاراکتر باشد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (description.Length > 500)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "دلیل شارژ نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var referenceNumber = BuildReferenceNumber(adminUserId, userId, dto.IdempotencyKey, prefix: "ADM-CHG");
            if (referenceNumber == null)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "کلید یکتایی نامعتبر است. فقط حروف، عدد، خط تیره و زیرخط مجاز است",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var userSnapshot = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .Select(u => new { u.Id, u.WalletBalance, u.PhoneNumber, u.FullName, u.IsActive })
                .FirstOrDefaultAsync();

            if (userSnapshot == null)
                return ApiResponse<AdminManualChargeResponseDto>.NotFound("کاربر یافت نشد");

            if (userSnapshot.WalletBalance + amount > MaxWalletBalance)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "موجودی پس از شارژ از سقف مجاز عبور می‌کند",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var ledgerDescription =
                $"ادمین #{adminUserId} — {description}";

            _logger.LogInformation(
                "Admin manual wallet charge requested — AdminId={AdminId}, UserId={UserId}, Amount={Amount}, Ref={Ref}",
                adminUserId, userId, amount, referenceNumber);

            var creditResult = await _walletService.AddBalanceAsync(
                userId,
                amount,
                WalletTransactionTypes.Deposit,
                ManualChargeTitle,
                ledgerDescription,
                paymentId: null,
                cashbackId: null,
                referenceNumber: referenceNumber,
                sendPushNotification: true,
                actorUserId: adminUserId);

            if (!creditResult.Success || creditResult.Data == null)
            {
                _logger.LogWarning(
                    "Admin manual wallet charge failed — AdminId={AdminId}, UserId={UserId}, Status={Status}, ErrorCode={ErrorCode}",
                    adminUserId, userId, creditResult.StatusCode, creditResult.ErrorCode);

                return ApiResponse<AdminManualChargeResponseDto>.Error(
                    creditResult.Message,
                    creditResult.StatusCode,
                    creditResult.Errors,
                    creditResult.ErrorCode);
            }

            var tx = creditResult.Data;
            var wasAlreadyProcessed = string.Equals(
                creditResult.Message,
                DuplicateMessage,
                StringComparison.Ordinal);

            var response = new AdminManualChargeResponseDto
            {
                UserId = userId,
                Amount = amount,
                FormattedAmount = FormatAmount(amount),
                BalanceBefore = tx.BalanceBefore,
                BalanceAfter = tx.BalanceAfter,
                FormattedBalanceAfter = FormatAmount(tx.BalanceAfter),
                ReferenceNumber = tx.ReferenceNumber ?? referenceNumber,
                WasAlreadyProcessed = wasAlreadyProcessed,
                Transaction = tx
            };

            // فقط برای شارژ جدید audit ادمین بنویس — تکراری‌ها قبلاً ثبت شده‌اند
            if (!wasAlreadyProcessed)
            {
                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Wallet,
                    Action = AuditActions.WalletManualCharged,
                    EntityType = AuditEntityTypes.WalletTransaction,
                    EntityId = tx.Id.ToString(),
                    ActorUserId = adminUserId,
                    TargetUserId = userId,
                    Succeeded = true,
                    After = new
                    {
                        occurredAtUtc = DateTime.UtcNow,
                        eventType = "WalletManualCharged",
                        adminUserId,
                        userId,
                        phoneNumber = userSnapshot.PhoneNumber,
                        fullName = userSnapshot.FullName,
                        amount,
                        amountLabel = FormatAmount(amount),
                        balanceBefore = tx.BalanceBefore,
                        balanceAfter = tx.BalanceAfter,
                        referenceNumber = response.ReferenceNumber,
                        walletTransactionId = tx.Id,
                        description,
                        userWasActive = userSnapshot.IsActive
                    },
                    Metadata = new
                    {
                        source = "AdminPanel",
                        title = ManualChargeTitle
                    }
                });
            }

            _logger.LogInformation(
                "Admin manual wallet charge completed — AdminId={AdminId}, UserId={UserId}, TxId={TxId}, Amount={Amount}, BalanceAfter={BalanceAfter}, Duplicate={Duplicate}",
                adminUserId, userId, tx.Id, amount, tx.BalanceAfter, wasAlreadyProcessed);

            var message = wasAlreadyProcessed
                ? "این شارژ قبلاً ثبت شده است (جلوگیری از ثبت تکراری)"
                : "شارژ دستی با موفقیت انجام شد";

            return ApiResponse<AdminManualChargeResponseDto>.CreateSuccess(response, message);
        }

        public async Task<ApiResponse<AdminManualChargeResponseDto>> ManualDeductAsync(
            int adminUserId,
            int userId,
            AdminManualChargeRequestDto dto)
        {
            if (adminUserId <= 0)
            {
                return ApiResponse<AdminManualChargeResponseDto>.Unauthorized(
                    ControlledErrorHelper.Unauthorized,
                    ErrorCodes.Unauthorized);
            }

            if (userId <= 0)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "شناسه کاربر نامعتبر است",
                    errorCode: ErrorCodes.InvalidUserId);
            }

            if (dto == null)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var amount = dto.Amount;
            if (amount <= 0 || amount > MaxManualChargeAmount)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "مبلغ کسر باید بین ۱ تا ۱۰۰٬۰۰۰٬۰۰۰ تومان باشد",
                    errorCode: ErrorCodes.InvalidInput);
            }

            if (amount != decimal.Truncate(amount))
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "مبلغ کسر باید عدد صحیح تومان باشد",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var description = SanitizeDescription(dto.Description);
            if (description.Length < 5)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "دلیل کسر باید حداقل ۵ کاراکتر باشد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (description.Length > 500)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "دلیل کسر نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var referenceNumber = BuildReferenceNumber(adminUserId, userId, dto.IdempotencyKey, prefix: "ADM-DED");
            if (referenceNumber == null)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    "کلید یکتایی نامعتبر است. فقط حروف، عدد، خط تیره و زیرخط مجاز است",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var userSnapshot = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .Select(u => new { u.Id, u.WalletBalance, u.PhoneNumber, u.FullName, u.IsActive })
                .FirstOrDefaultAsync();

            if (userSnapshot == null)
                return ApiResponse<AdminManualChargeResponseDto>.NotFound("کاربر یافت نشد");

            if (userSnapshot.WalletBalance < amount)
            {
                return ApiResponse<AdminManualChargeResponseDto>.BadRequest(
                    $"موجودی کافی نیست. موجودی فعلی: {FormatAmount(userSnapshot.WalletBalance)}",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var ledgerDescription = $"ادمین #{adminUserId} — {description}";

            _logger.LogInformation(
                "Admin manual wallet deduct requested — AdminId={AdminId}, UserId={UserId}, Amount={Amount}, Ref={Ref}",
                adminUserId, userId, amount, referenceNumber);

            var debitResult = await _walletService.DeductBalanceAsync(
                userId,
                amount,
                ManualDeductTitle,
                ledgerDescription,
                referenceNumber: referenceNumber,
                sendPushNotification: true,
                actorUserId: adminUserId,
                transactionType: WalletTransactionTypes.Withdrawal);

            if (!debitResult.Success || debitResult.Data == null)
            {
                _logger.LogWarning(
                    "Admin manual wallet deduct failed — AdminId={AdminId}, UserId={UserId}, Status={Status}, ErrorCode={ErrorCode}",
                    adminUserId, userId, debitResult.StatusCode, debitResult.ErrorCode);

                return ApiResponse<AdminManualChargeResponseDto>.Error(
                    debitResult.Message,
                    debitResult.StatusCode,
                    debitResult.Errors,
                    debitResult.ErrorCode);
            }

            var tx = debitResult.Data;
            var wasAlreadyProcessed = string.Equals(
                debitResult.Message,
                DuplicateMessage,
                StringComparison.Ordinal);

            var response = new AdminManualChargeResponseDto
            {
                UserId = userId,
                Amount = amount,
                FormattedAmount = FormatAmount(amount),
                BalanceBefore = tx.BalanceBefore,
                BalanceAfter = tx.BalanceAfter,
                FormattedBalanceAfter = FormatAmount(tx.BalanceAfter),
                ReferenceNumber = tx.ReferenceNumber ?? referenceNumber,
                WasAlreadyProcessed = wasAlreadyProcessed,
                Transaction = tx
            };

            if (!wasAlreadyProcessed)
            {
                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Wallet,
                    Action = AuditActions.WalletManualDebited,
                    EntityType = AuditEntityTypes.WalletTransaction,
                    EntityId = tx.Id.ToString(),
                    ActorUserId = adminUserId,
                    TargetUserId = userId,
                    Succeeded = true,
                    After = new
                    {
                        occurredAtUtc = DateTime.UtcNow,
                        eventType = "WalletManualDebited",
                        adminUserId,
                        userId,
                        phoneNumber = userSnapshot.PhoneNumber,
                        fullName = userSnapshot.FullName,
                        amount,
                        amountLabel = FormatAmount(amount),
                        balanceBefore = tx.BalanceBefore,
                        balanceAfter = tx.BalanceAfter,
                        referenceNumber = response.ReferenceNumber,
                        walletTransactionId = tx.Id,
                        description,
                        userWasActive = userSnapshot.IsActive
                    },
                    Metadata = new
                    {
                        source = "AdminPanel",
                        title = ManualDeductTitle
                    }
                });
            }

            _logger.LogInformation(
                "Admin manual wallet deduct completed — AdminId={AdminId}, UserId={UserId}, TxId={TxId}, Amount={Amount}, BalanceAfter={BalanceAfter}, Duplicate={Duplicate}",
                adminUserId, userId, tx.Id, amount, tx.BalanceAfter, wasAlreadyProcessed);

            var message = wasAlreadyProcessed
                ? "این کسر قبلاً ثبت شده است (جلوگیری از ثبت تکراری)"
                : "کسر دستی با موفقیت انجام شد";

            return ApiResponse<AdminManualChargeResponseDto>.CreateSuccess(response, message);
        }

        private static string SanitizeDescription(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var trimmed = raw.Trim();
            var sb = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
            {
                // حذف کاراکترهای کنترلی (به‌جز فاصله/خط‌جدید نرمال‌شده به فاصله)
                if (char.IsControl(ch))
                {
                    if (ch == '\n' || ch == '\r' || ch == '\t')
                        sb.Append(' ');
                    continue;
                }

                sb.Append(ch);
            }

            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>
        /// مرجع یکتا حداکثر ۱۰۰ کاراکتر (محدودیت ستون ReferenceNumber)
        /// </summary>
        private static string? BuildReferenceNumber(
            int adminUserId,
            int userId,
            string? clientKey,
            string prefix)
        {
            if (!string.IsNullOrWhiteSpace(clientKey))
            {
                var key = clientKey.Trim();
                if (!SafeIdempotencyKeyRegex.IsMatch(key))
                    return null;

                var composed = $"{prefix}-{adminUserId}-{userId}-{key}";
                return composed.Length <= 100 ? composed : composed[..100];
            }

            return $"{prefix}-{adminUserId}-{userId}-{Guid.NewGuid():N}";
        }

        private static string FormatAmount(decimal amount) =>
            $"{amount:N0} تومان";
    }
}
