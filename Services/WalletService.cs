using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.DTOs.Cashback;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس کیف پول
    /// </summary>
    public class WalletService : IWalletService
    {
        private readonly Api_Context _context;
        private readonly IWalletRepository _walletRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICashbackRepository _cashbackRepository;
        private readonly IWalletReferralService _walletReferralService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly IAuditService _audit;
        private readonly IUserPushNotifier _pushNotifier;
        private readonly ILogger<WalletService> _logger;

        public WalletService(
            Api_Context context,
            IWalletRepository walletRepository,
            IUserRepository userRepository,
            ICashbackRepository cashbackRepository,
            IWalletReferralService walletReferralService,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IAuditService audit,
            IUserPushNotifier pushNotifier,
            ILogger<WalletService> logger)
        {
            _context = context;
            _walletRepository = walletRepository;
            _userRepository = userRepository;
            _cashbackRepository = cashbackRepository;
            _walletReferralService = walletReferralService;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _audit = audit;
            _pushNotifier = pushNotifier;
            _logger = logger;
        }

        public async Task<ApiResponse<WalletInfoDto>> GetWalletInfoAsync(int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<WalletInfoDto>.NotFound("کاربر یافت نشد");
                }

                var transactionsCount = await _walletRepository.GetCountByUserIdAsync(userId);
                var activeCashbacksCount = await _cashbackRepository.GetCountByUserIdAsync(userId, true);

                var walletInfo = new WalletInfoDto
                {
                    Balance = user.WalletBalance,
                    FormattedBalance = FormatAmount(user.WalletBalance),
                    ActiveCashbacksCount = activeCashbacksCount,
                    TotalTransactionsCount = transactionsCount,
                    LastUpdatedAt = user.UpdatedAt ?? user.CreatedAt
                };

                return ApiResponse<WalletInfoDto>.CreateSuccess(walletInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت اطلاعات کیف پول کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<WalletTransactionListDto>> GetTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var transactions = await _walletRepository.GetByUserIdAsync(userId, pageNumber, pageSize);
                var totalCount = await _walletRepository.GetCountByUserIdAsync(userId);
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var transactionDtos = transactions.Select(MapToWalletTransactionDto).ToList();

                var result = new WalletTransactionListDto
                {
                    Transactions = transactionDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<WalletTransactionListDto>.CreateSuccess(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت تراکنش‌های کیف پول کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<List<WalletTransactionDto>>> GetRecentTransactionsAsync(int userId, int count = 5)
        {
            try
            {
                var transactions = await _walletRepository.GetRecentTransactionsAsync(userId, count);
                var transactionDtos = transactions.Select(MapToWalletTransactionDto).ToList();
                return ApiResponse<List<WalletTransactionDto>>.CreateSuccess(transactionDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت آخرین تراکنش‌های کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<ChargeWalletResponseDto>> ChargeWalletAsync(int userId, ChargeWalletRequestDto request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<ChargeWalletResponseDto>.NotFound("کاربر یافت نشد");
                }

                if (!string.Equals(request.Gateway, PaymentGateways.Behpardakht, StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<ChargeWalletResponseDto>.BadRequest("درگاه پرداخت پشتیبانی نمی‌شود");
                }

                var requestedAmount = request.Amount;
                var referralResolve = await _walletReferralService.ResolveReferralForChargeAsync(
                    userId, requestedAmount, request.ReferralCode);

                if (!referralResolve.Success)
                {
                    return ApiResponse<ChargeWalletResponseDto>.BadRequest(
                        referralResolve.Message,
                        referralResolve.Errors,
                        referralResolve.ErrorCode);
                }

                var referralMeta = referralResolve.Data;
                var payableAmount = referralMeta?.PayableAmount ?? requestedAmount;
                var discountAmount = referralMeta?.DiscountAmount ?? 0m;
                var discountPercent = referralMeta?.DiscountPercent ?? 0m;

                var useSimulation = _configuration.GetValue("Payment:UseSimulation", true);
                var orderId = GenerateOrderId();
                var callbackUrl = request.CallbackUrl
                    ?? _configuration["Payment:Behpardakht:FrontendCallbackUrl"]
                    ?? "/payment/result";

                // RefId شبیه‌سازی‌شده تا آماده‌شدن درگاه واقعی
                var refId = useSimulation
                    ? $"SIMREF{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}"
                    : null;

                var description = referralMeta != null
                    ? $"شارژ کیف پول با کد معرفی {referralMeta.ReferralCode}"
                    : "شارژ کیف پول";

                var payment = new Payment
                {
                    UserId = userId,
                    Amount = payableAmount,
                    PaymentType = PaymentTypes.WalletCharge,
                    Gateway = PaymentGateways.Behpardakht,
                    OrderId = orderId,
                    RefId = refId,
                    Status = useSimulation ? PaymentStatuses.Processing : PaymentStatuses.Pending,
                    CallbackUrl = callbackUrl,
                    Description = description,
                    MetaData = WalletReferralService.SerializeChargeMeta(referralMeta),
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Payments.AddAsync(payment);
                await _context.SaveChangesAsync();

                var response = new ChargeWalletResponseDto
                {
                    PaymentId = payment.Id,
                    OrderId = orderId,
                    Amount = payableAmount,
                    RequestedAmount = requestedAmount,
                    DiscountAmount = discountAmount,
                    DiscountPercent = discountPercent,
                    ReferralApplied = referralMeta != null,
                    ReferralCode = referralMeta?.ReferralCode,
                    GatewayUrl = BuildGatewayUrl(payment.Id),
                    Gateway = payment.Gateway,
                    PaymentType = PaymentTypes.WalletCharge,
                    PaymentTypeTitle = "شارژ کیف پول",
                    RefId = payment.RefId,
                    IsSimulation = useSimulation
                };

                _logger.LogInformation(
                    "درخواست شارژ کیف پول ایجاد شد. کاربر: {UserId}, مبلغ درخواستی: {RequestedAmount}, قابل پرداخت: {PayableAmount}, رفرال: {ReferralCode}, سفارش: {OrderId}, Simulation: {IsSimulation}",
                    userId, requestedAmount, payableAmount, referralMeta?.ReferralCode, orderId, useSimulation);

                return ApiResponse<ChargeWalletResponseDto>.CreateSuccess(response, "درخواست پرداخت با موفقیت ایجاد شد", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد درخواست شارژ کیف پول برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<WalletTransactionDto>> AddBalanceAsync(
            int userId, 
            decimal amount, 
            string transactionType,
            string title, 
            string? description = null, 
            int? paymentId = null, 
            int? cashbackId = null,
            string? referenceNumber = null)
        {
            try
            {
                if (amount <= 0)
                {
                    return ApiResponse<WalletTransactionDto>.BadRequest("مبلغ باید بزرگتر از صفر باشد");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<WalletTransactionDto>.NotFound("کاربر یافت نشد");
                }

                var ownsTransaction = _context.Database.CurrentTransaction == null;
                IDbContextTransaction? transaction = null;
                if (ownsTransaction)
                    transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var balanceBefore = user.WalletBalance;
                    var balanceAfter = balanceBefore + amount;

                    var walletTransaction = new WalletTransaction
                    {
                        UserId = userId,
                        TransactionType = transactionType,
                        Amount = amount,
                        BalanceBefore = balanceBefore,
                        BalanceAfter = balanceAfter,
                        Title = title,
                        Description = description,
                        PaymentId = paymentId,
                        CashbackId = cashbackId,
                        ReferenceNumber = referenceNumber,
                        Status = TransactionStatuses.Completed,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    };

                    await _context.WalletTransactions.AddAsync(walletTransaction);

                    user.WalletBalance = balanceAfter;
                    user.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    if (ownsTransaction && transaction != null)
                        await transaction.CommitAsync();

                    _logger.LogInformation("موجودی کیف پول کاربر {UserId} به مبلغ {Amount} تومان افزایش یافت. موجودی جدید: {NewBalance}", 
                        userId, amount, balanceAfter);

                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Wallet,
                        Action = AuditActions.WalletCredited,
                        EntityType = AuditEntityTypes.WalletTransaction,
                        EntityId = walletTransaction.Id.ToString(),
                        TargetUserId = userId,
                        After = new
                        {
                            walletTransactionId = walletTransaction.Id,
                            userId,
                            amount,
                            balanceBefore,
                            balanceAfter,
                            transactionType,
                            title,
                            paymentId,
                            cashbackId
                        }
                    });

                    var creditCopy = PushNotificationCopy.WalletCredited(amount, balanceAfter, title);
                    // بعد از commit ارسال می‌شود؛ کمی تأخیر تا UI موبایل از درگاه برگردد و شانس نمایش سیستم‌ترِی بیشتر شود
                    var pushUserId = userId;
                    var pushTitle = creditCopy.Title;
                    var pushBody = creditCopy.Body;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(1200);
                            await _pushNotifier.NotifyAsync(
                                pushUserId,
                                NotificationCategory.WalletTransaction,
                                pushTitle,
                                pushBody);
                        }
                        catch (Exception pushEx)
                        {
                            _logger.LogError(pushEx,
                                "خطا در Push تأخیری شارژ کیف پول — UserId={UserId}", pushUserId);
                        }
                    });

                    return ApiResponse<WalletTransactionDto>.CreateSuccess(
                        MapToWalletTransactionDto(walletTransaction), 
                        "موجودی با موفقیت افزایش یافت");
                }
                catch
                {
                    if (ownsTransaction && transaction != null)
                        await transaction.RollbackAsync();
                    throw;
                }
                finally
                {
                    if (ownsTransaction)
                        transaction?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در افزایش موجودی کیف پول کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<WalletTransactionDto>> DeductBalanceAsync(
            int userId, 
            decimal amount, 
            string title, 
            string? description = null)
        {
            try
            {
                if (amount <= 0)
                {
                    return ApiResponse<WalletTransactionDto>.BadRequest("مبلغ باید بزرگتر از صفر باشد");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<WalletTransactionDto>.NotFound("کاربر یافت نشد");
                }

                if (user.WalletBalance < amount)
                {
                    var warn = PushNotificationCopy.InsufficientWallet(amount, user.WalletBalance);
                    await _pushNotifier.NotifyAsync(
                        userId,
                        NotificationCategory.SystemWarnings,
                        warn.Title,
                        warn.Body);
                    return ApiResponse<WalletTransactionDto>.BadRequest("موجودی کیف پول کافی نیست");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var balanceBefore = user.WalletBalance;
                    var balanceAfter = balanceBefore - amount;

                    // ایجاد تراکنش کیف پول
                    var walletTransaction = new WalletTransaction
                    {
                        UserId = userId,
                        TransactionType = WalletTransactionTypes.Purchase,
                        Amount = -amount, // منفی برای کسر از موجودی
                        BalanceBefore = balanceBefore,
                        BalanceAfter = balanceAfter,
                        Title = title,
                        Description = description,
                        Status = TransactionStatuses.Completed,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    };

                    await _context.WalletTransactions.AddAsync(walletTransaction);

                    // به‌روزرسانی موجودی کاربر
                    user.WalletBalance = balanceAfter;
                    user.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("موجودی کیف پول کاربر {UserId} به مبلغ {Amount} تومان کاهش یافت. موجودی جدید: {NewBalance}", 
                        userId, amount, balanceAfter);

                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Wallet,
                        Action = AuditActions.WalletDebited,
                        EntityType = AuditEntityTypes.WalletTransaction,
                        EntityId = walletTransaction.Id.ToString(),
                        TargetUserId = userId,
                        After = new
                        {
                            walletTransactionId = walletTransaction.Id,
                            userId,
                            amount,
                            balanceBefore,
                            balanceAfter,
                            transactionType = WalletTransactionTypes.Purchase,
                            title
                        }
                    });

                    var debitCopy = PushNotificationCopy.WalletDebited(amount, balanceAfter, title);
                    await _pushNotifier.NotifyAsync(
                        userId,
                        NotificationCategory.WalletTransaction,
                        debitCopy.Title,
                        debitCopy.Body);

                    return ApiResponse<WalletTransactionDto>.CreateSuccess(
                        MapToWalletTransactionDto(walletTransaction), 
                        "موجودی با موفقیت کسر شد");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در کسر موجودی کیف پول کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> HasSufficientBalanceAsync(int userId, decimal amount)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user != null && user.WalletBalance >= amount;
        }

        public async Task<decimal> GetBalanceAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.WalletBalance ?? 0;
        }

        public async Task<ApiResponse<WalletPageDto>> GetWalletPageAsync(int userId, int recentTransactionsCount = 10)
        {
            try
            {
                if (recentTransactionsCount < 1) recentTransactionsCount = 1;
                if (recentTransactionsCount > 50) recentTransactionsCount = 50;

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<WalletPageDto>.NotFound("کاربر یافت نشد");
                }

                // دریافت موجودی کیف پول
                var balance = user.WalletBalance;

                // دریافت کش‌بک‌های فعال با استفاده از lazy loading برای جلوگیری از circular dependency
                List<CashbackDto> activeCashbacks = new List<CashbackDto>();
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var cashbackService = scope.ServiceProvider.GetService<ICashbackService>();
                    if (cashbackService != null)
                    {
                        var activeCashbacksResponse = await cashbackService.GetActiveCashbacksAsync(userId);
                        activeCashbacks = activeCashbacksResponse.Success ? activeCashbacksResponse.Data ?? new List<CashbackDto>() : new List<CashbackDto>();
                    }
                    else
                    {
                        _logger.LogWarning("ICashbackService در دسترس نیست، لیست کش‌بک‌های فعال خالی برمی‌گردد");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خطا در دریافت کش‌بک‌های فعال کاربر {UserId}، لیست خالی برمی‌گردد", userId);
                    // در صورت خطا، لیست خالی برمی‌گردد
                }

                // دریافت آخرین تراکنش‌ها
                var recentTransactionsResponse = await GetRecentTransactionsAsync(userId, recentTransactionsCount);
                var recentTransactions = recentTransactionsResponse.Success ? recentTransactionsResponse.Data ?? new List<WalletTransactionDto>() : new List<WalletTransactionDto>();

                // دریافت تعداد کل تراکنش‌ها
                var totalTransactionsCount = await _walletRepository.GetCountByUserIdAsync(userId);

                var walletPage = new WalletPageDto
                {
                    Balance = balance,
                    FormattedBalance = FormatAmount(balance),
                    ActiveCashbacks = activeCashbacks,
                    RecentTransactions = recentTransactions,
                    TotalTransactionsCount = totalTransactionsCount
                };

                return ApiResponse<WalletPageDto>.CreateSuccess(walletPage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت اطلاعات صفحه کیف پول کاربر {UserId}", userId);
                throw;
            }
        }

        #region Private Methods

        private WalletTransactionDto MapToWalletTransactionDto(WalletTransaction transaction)
        {
            return new WalletTransactionDto
            {
                Id = transaction.Id,
                TransactionType = transaction.TransactionType,
                Title = transaction.Title,
                Description = transaction.Description,
                Amount = transaction.Amount,
                FormattedAmount = FormatAmountWithSign(transaction.Amount),
                BalanceBefore = transaction.BalanceBefore,
                BalanceAfter = transaction.BalanceAfter,
                ReferenceNumber = transaction.ReferenceNumber,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt,
                PersianCreatedAt = ToPersianDate(transaction.CreatedAt),
                CompletedAt = transaction.CompletedAt
            };
        }

        private string GenerateOrderId()
        {
            // فرمت: VW + تاریخ + شماره رندوم
            return $"VW{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        }

        private string BuildGatewayUrl(int paymentId)
        {
            var apiBaseUrl = _configuration["Payment:ApiBaseUrl"]?.TrimEnd('/') ?? string.Empty;
            var redirectPath = $"/api/Payment/redirect/{paymentId}";
            return string.IsNullOrEmpty(apiBaseUrl) ? redirectPath : $"{apiBaseUrl}{redirectPath}";
        }

        private static string FormatAmount(decimal amount)
        {
            return $"{amount:N0} تومان";
        }

        private static string FormatAmountWithSign(decimal amount)
        {
            var sign = amount >= 0 ? "+" : "";
            return $"{sign}{amount:N0}";
        }

        private static string ToPersianDate(DateTime date)
        {
            try
            {
                var pc = new PersianCalendar();
                var persianDate = $"{pc.GetDayOfMonth(date)} {GetPersianMonthName(pc.GetMonth(date))}";
                var time = date.ToString("HH:mm");
                return $"{time} - {persianDate}";
            }
            catch
            {
                return date.ToString("yyyy-MM-dd HH:mm");
            }
        }

        private static string GetPersianMonthName(int month)
        {
            var months = new[] { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", 
                                 "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
            return month >= 1 && month <= 12 ? months[month] : "";
        }

        #endregion
    }
}




