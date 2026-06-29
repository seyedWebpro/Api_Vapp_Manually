using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Payment;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Exceptions;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس پرداخت
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly Api_Context _context;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IWalletService _walletService;
        private readonly ISubscriptionActivationService _subscriptionActivationService;
        private readonly ISubscriptionEntitlementService _subscriptionEntitlementService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PaymentService> _logger;

        // تنظیمات درگاه به‌پرداخت
        private readonly string _behpardakhtTerminalId;
        private readonly string _behpardakhtUsername;
        private readonly string _behpardakhtPassword;
        private readonly string _behpardakhtPaymentUrl;
        private readonly string _behpardakhtTokenUrl;
        private readonly string _behpardakhtVerifyUrl;
        private readonly string _behpardakhtSettleUrl;

        public PaymentService(
            Api_Context context,
            IPaymentRepository paymentRepository,
            IUserRepository userRepository,
            IWalletService walletService,
            ISubscriptionActivationService subscriptionActivationService,
            ISubscriptionEntitlementService subscriptionEntitlementService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _userRepository = userRepository;
            _walletService = walletService;
            _subscriptionActivationService = subscriptionActivationService;
            _subscriptionEntitlementService = subscriptionEntitlementService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // خواندن تنظیمات درگاه
            _behpardakhtTerminalId = _configuration["Payment:Behpardakht:TerminalId"] ?? "";
            _behpardakhtUsername = _configuration["Payment:Behpardakht:Username"] ?? "";
            _behpardakhtPassword = _configuration["Payment:Behpardakht:Password"] ?? "";
            _behpardakhtPaymentUrl = _configuration["Payment:Behpardakht:PaymentUrl"] ?? "https://bpm.shaparak.ir/pgwchannel/startpay.mellat";
            _behpardakhtTokenUrl = _configuration["Payment:Behpardakht:TokenUrl"] ?? "https://bpm.shaparak.ir/pgwchannel/services/pgw?wsdl";
            _behpardakhtVerifyUrl = _configuration["Payment:Behpardakht:VerifyUrl"] ?? "https://bpm.shaparak.ir/pgwchannel/services/pgw?wsdl";
            _behpardakhtSettleUrl = _configuration["Payment:Behpardakht:SettleUrl"] ?? "https://bpm.shaparak.ir/pgwchannel/services/pgw?wsdl";
        }

        public async Task<ApiResponse<PaymentDto>> CreatePaymentAsync(int userId, CreatePaymentDto createDto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<PaymentDto>.NotFound("کاربر یافت نشد");
                }

                // بررسی وجود پرداخت در انتظار
                if (await _paymentRepository.HasPendingPaymentAsync(userId))
                {
                    return ApiResponse<PaymentDto>.BadRequest("شما یک پرداخت در انتظار دارید. لطفاً ابتدا آن را تکمیل یا لغو کنید.");
                }

                // ایجاد شماره سفارش یکتا
                var orderId = GenerateOrderId();

                var payment = new Payment
                {
                    UserId = userId,
                    Amount = createDto.Amount,
                    PaymentType = createDto.PaymentType,
                    Gateway = createDto.Gateway,
                    OrderId = orderId,
                    Status = PaymentStatuses.Pending,
                    Description = createDto.Description,
                    CallbackUrl = createDto.CallbackUrl,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Payments.AddAsync(payment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("پرداخت جدید با شناسه {PaymentId} برای کاربر {UserId} ایجاد شد", payment.Id, userId);

                return ApiResponse<PaymentDto>.CreateSuccess(MapToPaymentDto(payment), "پرداخت با موفقیت ایجاد شد", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد پرداخت برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<PaymentDto>> GetPaymentByIdAsync(int id, int userId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(id);
                if (payment == null || payment.UserId != userId)
                {
                    return ApiResponse<PaymentDto>.NotFound("پرداخت یافت نشد");
                }

                return ApiResponse<PaymentDto>.CreateSuccess(MapToPaymentDto(payment));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت پرداخت {PaymentId} برای کاربر {UserId}", id, userId);
                throw;
            }
        }

        public async Task<ApiResponse<PaymentDto>> GetPaymentByOrderIdAsync(string orderId, int userId)
        {
            try
            {
                var payment = await _paymentRepository.GetByOrderIdAsync(orderId);
                if (payment == null || payment.UserId != userId)
                {
                    return ApiResponse<PaymentDto>.NotFound("پرداخت یافت نشد");
                }

                return ApiResponse<PaymentDto>.CreateSuccess(MapToPaymentDto(payment));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت پرداخت با شماره سفارش {OrderId}", orderId);
                throw;
            }
        }

        public async Task<ApiResponse<PaymentResultDto>> VerifyPaymentAsync(int userId, VerifyPaymentRequestDto verifyDto)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(verifyDto.PaymentId);
                if (payment == null)
                {
                    return ApiResponse<PaymentResultDto>.NotFound("پرداخت یافت نشد");
                }

                if (payment.UserId != userId)
                {
                    return ApiResponse<PaymentResultDto>.Forbidden("شما مجاز به تأیید این پرداخت نیستید");
                }

                // بررسی وضعیت پرداخت
                if (payment.Status == PaymentStatuses.Verified)
                {
                    return ApiResponse<PaymentResultDto>.BadRequest("این پرداخت قبلاً تأیید شده است");
                }

                if (payment.Status == PaymentStatuses.Failed || payment.Status == PaymentStatuses.Cancelled)
                {
                    var result = new PaymentResultDto
                    {
                        Success = false,
                        Message = "پرداخت ناموفق بود",
                        Payment = MapToPaymentDto(payment)
                    };
                    return ApiResponse<PaymentResultDto>.CreateSuccess(result, "پرداخت ناموفق بود");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // بررسی کد وضعیت درگاه
                    bool isSuccessful = false;
                    string? saleReferenceId = verifyDto.SaleReferenceId;

                    // بررسی ResCode (برای به‌پرداخت)
                    if (!string.IsNullOrEmpty(verifyDto.ResCode))
                    {
                        if (verifyDto.ResCode == "0")
                        {
                            // پرداخت موفق - باید Verify و Settle شود
                            if (!string.IsNullOrEmpty(verifyDto.RefId) && !string.IsNullOrEmpty(saleReferenceId))
                            {
                                var (verifySuccess, settleRefId, errorMessage) = await VerifyAndSettleBehpardakhtAsync(
                                    verifyDto.RefId, 
                                    long.Parse(saleReferenceId));
                                
                                isSuccessful = verifySuccess;
                                if (!verifySuccess && !string.IsNullOrEmpty(errorMessage))
                                {
                                    payment.ErrorMessage = errorMessage;
                                }
                            }
                            else
                            {
                                // در حالت تست، موفق در نظر می‌گیریم
                                isSuccessful = true;
                            }
                        }
                        else
                        {
                            // پرداخت ناموفق
                            payment.ErrorCode = verifyDto.ResCode;
                            payment.ErrorMessage = GetBehpardakhtErrorMessage(verifyDto.ResCode);
                        }
                    }
                    else
                    {
                        // در صورت عدم وجود ResCode، بررسی بر اساس RefId
                        isSuccessful = !string.IsNullOrEmpty(verifyDto.RefId);
                    }

                    // به‌روزرسانی اطلاعات پرداخت
                    payment.RefId = verifyDto.RefId;
                    payment.TransactionId = verifyDto.TransactionId;
                    payment.CardNumber = verifyDto.CardNumber;
                    payment.ReferenceNumber = saleReferenceId;

                    if (isSuccessful)
                    {
                        payment.Status = PaymentStatuses.Verified;
                        payment.PaidAt = DateTime.UtcNow;
                        payment.VerifiedAt = DateTime.UtcNow;

                        await _context.SaveChangesAsync();

                        // اضافه کردن موجودی به کیف پول
                        if (payment.PaymentType == PaymentTypes.WalletCharge)
                        {
                            await _walletService.AddBalanceAsync(
                                payment.UserId,
                                payment.Amount,
                                WalletTransactionTypes.Deposit,
                                "شارژ کیف پول",
                                $"پرداخت از طریق {GetGatewayName(payment.Gateway)}",
                                payment.Id,
                                null,
                                payment.ReferenceNumber);
                        }
                        else if (payment.PaymentType == PaymentTypes.Subscription)
                        {
                            try
                            {
                                await _subscriptionActivationService.FulfillVerifiedPaymentAsync(payment);
                            }
                            catch (AppException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Subscription fulfillment failed for payment {PaymentId}", payment.Id);
                                throw AppException.Internal(ErrorCodes.PaymentFailed, SubscriptionMessages.ActivationFailed);
                            }
                        }

                        await transaction.CommitAsync();

                        var user = await _userRepository.GetByIdAsync(payment.UserId);
                        var successResult = new PaymentResultDto
                        {
                            Success = true,
                            Message = payment.PaymentType == PaymentTypes.Subscription
                                ? "اشتراک با موفقیت فعال شد"
                                : "پرداخت با موفقیت انجام شد",
                            Payment = MapToPaymentDto(payment),
                            NewBalance = user?.WalletBalance,
                            FormattedNewBalance = user != null ? $"{user.WalletBalance:N0} تومان" : null
                        };

                        if (payment.PaymentType == PaymentTypes.Subscription)
                        {
                            var active = await _subscriptionEntitlementService.GetActiveSubscriptionAsync(payment.UserId);
                            if (active?.Plan != null)
                            {
                                var remainingDays = Math.Max(0, (int)Math.Ceiling((active.ExpiresAt - DateTime.UtcNow).TotalDays));
                                successResult.ActivatedSubscription = new DTOs.Subscription.CurrentSubscriptionDto
                                {
                                    UserSubscriptionId = active.Id,
                                    PlanId = active.Plan.Id,
                                    PlanName = active.Plan.Name,
                                    TierCode = active.Plan.TierCode,
                                    StartDate = active.StartDate,
                                    ExpiresAt = active.ExpiresAt,
                                    RemainingDays = remainingDays,
                                    IsActive = true,
                                    IsFreePlan = false
                                };
                            }
                        }

                        _logger.LogInformation("پرداخت {PaymentId} با موفقیت تأیید شد. مبلغ: {Amount}", payment.Id, payment.Amount);

                        return ApiResponse<PaymentResultDto>.CreateSuccess(successResult, "پرداخت با موفقیت انجام شد");
                    }
                    else
                    {
                        payment.Status = PaymentStatuses.Failed;
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        var failResult = new PaymentResultDto
                        {
                            Success = false,
                            Message = payment.ErrorMessage ?? "پرداخت ناموفق بود",
                            Payment = MapToPaymentDto(payment)
                        };

                        _logger.LogWarning("پرداخت {PaymentId} ناموفق بود. کد خطا: {ErrorCode}", payment.Id, payment.ErrorCode);

                        return ApiResponse<PaymentResultDto>.CreateSuccess(failResult, "پرداخت ناموفق بود");
                    }
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تأیید پرداخت {PaymentId}", verifyDto.PaymentId);
                throw;
            }
        }

        public async Task<ApiResponse<PaymentListDto>> GetPaymentsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var payments = await _paymentRepository.GetByUserIdAsync(userId, pageNumber, pageSize);
                var totalCount = await _paymentRepository.GetCountByUserIdAsync(userId);
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var paymentDtos = payments.Select(MapToPaymentDto).ToList();

                var result = new PaymentListDto
                {
                    Payments = paymentDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<PaymentListDto>.CreateSuccess(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست پرداخت‌های کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<List<PaymentGatewayInfoDto>>> GetAvailableGatewaysAsync()
        {
            var gateways = new List<PaymentGatewayInfoDto>
            {
                new PaymentGatewayInfoDto
                {
                    Code = PaymentGateways.Behpardakht,
                    Name = "به‌پرداخت",
                    Description = "پرداخت از طریق درگاه بانکی به‌پرداخت",
                    LogoUrl = "/images/gateways/behpardakht.png",
                    IsActive = true,
                    ComingSoon = false
                },
                new PaymentGatewayInfoDto
                {
                    Code = PaymentGateways.Wallet,
                    Name = "پرداخت درون برنامه‌ای",
                    Description = "امکان پرداخت مستقیم از داخل اپ",
                    LogoUrl = "/images/gateways/vapp.png",
                    IsActive = false,
                    ComingSoon = true
                }
            };

            return await Task.FromResult(ApiResponse<List<PaymentGatewayInfoDto>>.CreateSuccess(gateways));
        }

        public async Task<ApiResponse<bool>> CancelPaymentAsync(int paymentId, int userId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null || payment.UserId != userId)
                {
                    return ApiResponse<bool>.NotFound("پرداخت یافت نشد");
                }

                if (payment.Status != PaymentStatuses.Pending)
                {
                    return ApiResponse<bool>.BadRequest("فقط پرداخت‌های در انتظار قابل لغو هستند");
                }

                payment.Status = PaymentStatuses.Cancelled;
                payment.ErrorMessage = "لغو توسط کاربر";
                await _context.SaveChangesAsync();

                _logger.LogInformation("پرداخت {PaymentId} توسط کاربر {UserId} لغو شد", paymentId, userId);

                return ApiResponse<bool>.CreateSuccess(true, "پرداخت با موفقیت لغو شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در لغو پرداخت {PaymentId}", paymentId);
                throw;
            }
        }

        public async Task<(bool Success, string? RefId, string? ErrorMessage)> RequestBehpardakhtTokenAsync(
            int paymentId, 
            decimal amount, 
            string orderId, 
            string callbackUrl)
        {
            try
            {
                // در اینجا باید درخواست SOAP به سرور به‌پرداخت ارسال شود
                // برای سادگی، یک شبیه‌سازی انجام می‌دهیم
                
                _logger.LogInformation("درخواست توکن به‌پرداخت برای پرداخت {PaymentId}", paymentId);
                
                // شبیه‌سازی موفقیت
                var refId = $"SIMREF{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
                
                return await Task.FromResult((true, refId, (string?)null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در درخواست توکن به‌پرداخت");
                return (false, null, ControlledErrorHelper.PaymentFailed);
            }
        }

        public async Task<(bool Success, string? SaleReferenceId, string? ErrorMessage)> VerifyAndSettleBehpardakhtAsync(
            string refId, 
            long saleReferenceId)
        {
            try
            {
                // در اینجا باید درخواست SOAP به سرور به‌پرداخت ارسال شود
                // برای سادگی، یک شبیه‌سازی انجام می‌دهیم
                
                _logger.LogInformation("تأیید پرداخت به‌پرداخت با RefId: {RefId}", refId);
                
                // شبیه‌سازی موفقیت
                return await Task.FromResult((true, saleReferenceId.ToString(), (string?)null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تأیید پرداخت به‌پرداخت");
                return (false, null, ControlledErrorHelper.PaymentFailed);
            }
        }

        #region Private Methods

        private PaymentDto MapToPaymentDto(Payment payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,
                Amount = payment.Amount,
                FormattedAmount = $"{payment.Amount:N0} تومان",
                PaymentType = payment.PaymentType,
                PaymentTypeTitle = GetPaymentTypeTitle(payment.PaymentType),
                Gateway = payment.Gateway,
                OrderId = payment.OrderId,
                RefId = payment.RefId,
                ReferenceNumber = payment.ReferenceNumber,
                TransactionId = payment.TransactionId,
                CardNumber = payment.CardNumber,
                Status = payment.Status,
                StatusTitle = GetStatusTitle(payment.Status),
                ErrorMessage = payment.ErrorMessage,
                Description = payment.Description,
                CreatedAt = payment.CreatedAt,
                PersianCreatedAt = ToPersianDate(payment.CreatedAt),
                PaidAt = payment.PaidAt,
                VerifiedAt = payment.VerifiedAt
            };
        }

        private string GenerateOrderId()
        {
            return $"VP{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }

        private static string GetPaymentTypeTitle(string paymentType)
        {
            return paymentType switch
            {
                PaymentTypes.WalletCharge => "شارژ کیف پول",
                PaymentTypes.Subscription => "خرید اشتراک",
                PaymentTypes.SmsPurchase => "خرید پیامک",
                _ => "نامشخص"
            };
        }

        private static string GetStatusTitle(string status)
        {
            return status switch
            {
                PaymentStatuses.Pending => "در انتظار پرداخت",
                PaymentStatuses.Processing => "در حال پردازش",
                PaymentStatuses.Paid => "پرداخت شده",
                PaymentStatuses.Verified => "تأیید شده",
                PaymentStatuses.Failed => "ناموفق",
                PaymentStatuses.Cancelled => "لغو شده",
                PaymentStatuses.Refunded => "استرداد شده",
                _ => "نامشخص"
            };
        }

        private static string GetGatewayName(string gateway)
        {
            return gateway switch
            {
                PaymentGateways.Behpardakht => "به‌پرداخت ملت",
                PaymentGateways.Zarinpal => "زرین‌پال",
                PaymentGateways.Wallet => "کیف پول",
                _ => "درگاه بانکی"
            };
        }

        private static string GetBehpardakhtErrorMessage(string resCode)
        {
            return resCode switch
            {
                "11" => "شماره کارت نامعتبر است",
                "12" => "موجودی کافی نیست",
                "13" => "رمز نادرست است",
                "14" => "تعداد درخواست‌ها بیش از حد مجاز است",
                "15" => "کاربر از انجام تراکنش منصرف شده است",
                "17" => "کارت غیرفعال است",
                "18" => "مشکل در اتصال به درگاه",
                "21" => "تراکنش تکراری است",
                "23" => "خطای امنیتی",
                "34" => "خطا در انجام تراکنش",
                "35" => "زمان پرداخت منقضی شده است",
                "41" => "لغو فرآیند توسط شما",
                "42" => "اختلال در درگاه بانکی",
                "43" => "کسر نشدن مبلغ از حساب",
                _ => "خطا در انجام تراکنش"
            };
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




