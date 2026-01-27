using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.DTOs.Cashback;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WalletService> _logger;

        public WalletService(
            Api_Context context,
            IWalletRepository walletRepository,
            IUserRepository userRepository,
            ICashbackRepository cashbackRepository,
            IServiceProvider serviceProvider,
            ILogger<WalletService> logger)
        {
            _context = context;
            _walletRepository = walletRepository;
            _userRepository = userRepository;
            _cashbackRepository = cashbackRepository;
            _serviceProvider = serviceProvider;
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

                // ایجاد شماره سفارش یکتا
                var orderId = GenerateOrderId();

                // ایجاد رکورد پرداخت
                var payment = new Payment
                {
                    UserId = userId,
                    Amount = request.Amount,
                    PaymentType = PaymentTypes.WalletCharge,
                    Gateway = request.Gateway,
                    OrderId = orderId,
                    Status = PaymentStatuses.Pending,
                    CallbackUrl = request.CallbackUrl,
                    Description = "شارژ کیف پول",
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Payments.AddAsync(payment);
                await _context.SaveChangesAsync();

                // ایجاد URL درگاه پرداخت
                var gatewayUrl = GenerateGatewayUrl(payment, request.Gateway);

                var response = new ChargeWalletResponseDto
                {
                    PaymentId = payment.Id,
                    OrderId = orderId,
                    Amount = request.Amount,
                    GatewayUrl = gatewayUrl,
                    Gateway = request.Gateway
                };

                _logger.LogInformation("درخواست شارژ کیف پول با موفقیت ایجاد شد. کاربر: {UserId}, مبلغ: {Amount}, سفارش: {OrderId}", 
                    userId, request.Amount, orderId);

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

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var balanceBefore = user.WalletBalance;
                    var balanceAfter = balanceBefore + amount;

                    // ایجاد تراکنش کیف پول
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

                    // به‌روزرسانی موجودی کاربر
                    user.WalletBalance = balanceAfter;
                    user.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("موجودی کیف پول کاربر {UserId} به مبلغ {Amount} تومان افزایش یافت. موجودی جدید: {NewBalance}", 
                        userId, amount, balanceAfter);

                    return ApiResponse<WalletTransactionDto>.CreateSuccess(
                        MapToWalletTransactionDto(walletTransaction), 
                        "موجودی با موفقیت افزایش یافت");
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
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
            return $"VW{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }

        private string GenerateGatewayUrl(Payment payment, string gateway)
        {
            // در اینجا باید URL واقعی درگاه ساخته شود
            // برای حالت توسعه، URL شبیه‌سازی شده برمی‌گردانیم
            return $"/api/Payment/redirect/{payment.Id}";
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




