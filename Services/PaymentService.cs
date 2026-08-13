using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Payment;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Exceptions;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Encodings.Web;

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
        private readonly IWalletReferralService _walletReferralService;
        private readonly ISubscriptionActivationService _subscriptionActivationService;
        private readonly ISubscriptionEntitlementService _subscriptionEntitlementService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IZarinPalGatewayClient _zarinPalGatewayClient;
        private readonly IAuditService _audit;
        private readonly ILogger<PaymentService> _logger;
        private readonly IUserPushNotifier _pushNotifier;

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
            IWalletReferralService walletReferralService,
            ISubscriptionActivationService subscriptionActivationService,
            ISubscriptionEntitlementService subscriptionEntitlementService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IZarinPalGatewayClient zarinPalGatewayClient,
            IAuditService audit,
            ILogger<PaymentService> logger,
            IUserPushNotifier pushNotifier)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _userRepository = userRepository;
            _walletService = walletService;
            _walletReferralService = walletReferralService;
            _subscriptionActivationService = subscriptionActivationService;
            _subscriptionEntitlementService = subscriptionEntitlementService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _zarinPalGatewayClient = zarinPalGatewayClient;
            _audit = audit;
            _logger = logger;
            _pushNotifier = pushNotifier;

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
                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Payment,
                        Action = AuditActions.PaymentRequestFailed,
                        EntityType = AuditEntityTypes.Payment,
                        ActorUserId = userId,
                        TargetUserId = userId,
                        Succeeded = false,
                        ErrorMessage = "پرداخت در انتظار قبلی وجود دارد",
                        Metadata = new
                        {
                            occurredAtUtc = DateTime.UtcNow,
                            eventType = "PendingPaymentLock",
                            user = PaymentAuditDetails.UserSnapshot(user),
                            amount = createDto.Amount,
                            amountLabel = $"{createDto.Amount:N0} تومان",
                            paymentType = createDto.PaymentType,
                            gateway = createDto.Gateway,
                            description = createDto.Description
                        }
                    });
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

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Payment,
                    Action = AuditActions.PaymentRequested,
                    EntityType = AuditEntityTypes.Payment,
                    EntityId = payment.Id.ToString(),
                    ActorUserId = userId,
                    TargetUserId = userId,
                    After = PaymentAuditDetails.PaymentSnapshot(payment, user, extra: new
                    {
                        eventType = "PaymentCreated",
                        callbackUrlHost = Uri.TryCreate(payment.CallbackUrl, UriKind.Absolute, out var cu)
                            ? cu.Host
                            : payment.CallbackUrl
                    })
                });

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

                // بررسی وضعیت پرداخت — تأیید مجدد امن (idempotent)
                // اگر قبلاً Verified شده ولی fulfill اشتراک/رفرال جا مانده باشد، دوباره تلاش می‌کنیم
                if (payment.Status == PaymentStatuses.Verified)
                {
                    if (payment.PaymentType == PaymentTypes.Subscription)
                    {
                        try
                        {
                            await _subscriptionActivationService.FulfillVerifiedPaymentAsync(payment);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Idempotent subscription fulfill failed for already-verified payment {PaymentId}",
                                payment.Id);
                        }
                    }
                    else if (payment.PaymentType == PaymentTypes.WalletCharge)
                    {
                        try
                        {
                            await _walletReferralService.FulfillWalletChargeWithReferralAsync(payment);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Idempotent wallet referral fulfill failed for already-verified payment {PaymentId}",
                                payment.Id);
                        }
                    }

                    return ApiResponse<PaymentResultDto>.CreateSuccess(
                        await BuildVerifiedResultAsync(payment),
                        "این پرداخت قبلاً تأیید شده است");
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

                var isZarinPal = string.Equals(payment.Gateway, PaymentGateways.Zarinpal, StringComparison.OrdinalIgnoreCase);

                if (isZarinPal)
                {
                    // Authority فقط از دیتابیس — جلوگیری از reuse کد 101 روی پرداخت دیگر
                    var storedAuthority = payment.RefId?.Trim();
                    if (string.IsNullOrWhiteSpace(storedAuthority))
                    {
                        return ApiResponse<PaymentResultDto>.BadRequest(
                            "کد مرجع پرداخت یافت نشد",
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    if (!string.IsNullOrWhiteSpace(verifyDto.Authority) &&
                        !string.Equals(verifyDto.Authority.Trim(), storedAuthority, StringComparison.Ordinal))
                    {
                        _logger.LogWarning(
                            "ZarinPal authority mismatch for payment {PaymentId}",
                            payment.Id);
                        return ApiResponse<PaymentResultDto>.BadRequest(
                            "کد مرجع پرداخت نامعتبر است",
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    // طبق مستندات: Verify فقط وقتی Status=OK
                    if (!string.Equals(verifyDto.Status, "OK", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(verifyDto.Status))
                        {
                            return ApiResponse<PaymentResultDto>.BadRequest(
                                "وضعیت بازگشت پرداخت نامعتبر است",
                                errorCode: ErrorCodes.InvalidInput);
                        }

                        payment.Status = PaymentStatuses.Failed;
                        payment.ErrorCode = verifyDto.Status;
                        payment.ErrorMessage = "پرداخت توسط کاربر لغو شد یا ناموفق بود";
                        await _context.SaveChangesAsync();

                        var cancelled = new PaymentResultDto
                        {
                            Success = false,
                            Message = payment.ErrorMessage,
                            Payment = MapToPaymentDto(payment)
                        };
                        return ApiResponse<PaymentResultDto>.CreateSuccess(cancelled, cancelled.Message);
                    }
                }

                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    // Reload داخل تراکنش Serializable برای جلوگیری از double-credit
                    await _context.Entry(payment).ReloadAsync();

                    if (payment.Status == PaymentStatuses.Verified)
                    {
                        await transaction.CommitAsync();
                        if (payment.PaymentType == PaymentTypes.Subscription)
                        {
                            try { await _subscriptionActivationService.FulfillVerifiedPaymentAsync(payment); }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Idempotent subscription fulfill failed for payment {PaymentId}", payment.Id);
                            }
                        }
                        else if (payment.PaymentType == PaymentTypes.WalletCharge)
                        {
                            try { await _walletReferralService.FulfillWalletChargeWithReferralAsync(payment); }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Idempotent wallet fulfill failed for payment {PaymentId}", payment.Id);
                            }
                        }

                        return ApiResponse<PaymentResultDto>.CreateSuccess(
                            await BuildVerifiedResultAsync(payment),
                            "این پرداخت قبلاً تأیید شده است");
                    }

                    if (payment.Status is PaymentStatuses.Failed or PaymentStatuses.Cancelled)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<PaymentResultDto>.CreateSuccess(new PaymentResultDto
                        {
                            Success = false,
                            Message = "پرداخت ناموفق بود",
                            Payment = MapToPaymentDto(payment)
                        }, "پرداخت ناموفق بود");
                    }

                    bool isSuccessful = false;
                    string? saleReferenceId = verifyDto.SaleReferenceId;

                    if (isZarinPal)
                    {
                        var zarinAuthority = payment.RefId!.Trim();
                        if (payment.Amount != decimal.Truncate(payment.Amount))
                        {
                            _logger.LogError(
                                "Non-integer payment amount blocked — PaymentId: {PaymentId}, Amount: {Amount}",
                                payment.Id, payment.Amount);
                            await transaction.RollbackAsync();
                            return ApiResponse<PaymentResultDto>.BadRequest(
                                "مبلغ پرداخت نامعتبر است",
                                errorCode: ErrorCodes.InvalidInput);
                        }

                        var amountToman = ToZarinPalAmount(payment.Amount);
                        var zarinVerify = await _zarinPalGatewayClient.VerifyPaymentAsync(amountToman, zarinAuthority);
                        isSuccessful = zarinVerify.Success;
                        if (zarinVerify.Success)
                        {
                            // کد 101 فقط وقتی امن است که همین پرداخت قبلاً Verify نشده باشد
                            // و Authority متعلق به همین ردیف باشد (بالا enforce شد)
                            saleReferenceId = zarinVerify.RefId ?? saleReferenceId;
                            payment.CardNumber = zarinVerify.CardPan ?? payment.CardNumber;
                            payment.TransactionId = zarinVerify.RefId ?? payment.TransactionId;
                        }
                        else
                        {
                            payment.ErrorCode = zarinVerify.Code.ToString();
                            payment.ErrorMessage = ControlledErrorHelper.PaymentFailed;
                        }
                    }
                    // بررسی ResCode (برای به‌پرداخت)
                    else if (!string.IsNullOrEmpty(verifyDto.ResCode))
                    {
                        if (verifyDto.ResCode == "0")
                        {
                            var useSimulation = _configuration.GetValue("Payment:UseSimulation", false);
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
                            else if (useSimulation)
                            {
                                isSuccessful = true;
                            }
                            else
                            {
                                payment.ErrorMessage = ControlledErrorHelper.PaymentFailed;
                            }
                        }
                        else
                        {
                            payment.ErrorCode = verifyDto.ResCode;
                            payment.ErrorMessage = GetBehpardakhtErrorMessage(verifyDto.ResCode);
                        }
                    }
                    else
                    {
                        // بدون ResCode و بدون مسیر زرین‌پال — هرگز با RefId کلاینت اعتبار نده
                        isSuccessful = false;
                        payment.ErrorMessage = ControlledErrorHelper.PaymentFailed;
                    }

                    // به‌روزرسانی اطلاعات پرداخت
                    if (!isZarinPal)
                        payment.RefId = verifyDto.RefId ?? payment.RefId;
                    payment.TransactionId = verifyDto.TransactionId ?? payment.TransactionId;
                    payment.CardNumber = verifyDto.CardNumber ?? payment.CardNumber;
                    payment.ReferenceNumber = saleReferenceId ?? payment.ReferenceNumber;

                    if (isSuccessful)
                    {
                        payment.Status = PaymentStatuses.Verified;
                        payment.PaidAt = DateTime.UtcNow;
                        payment.VerifiedAt = DateTime.UtcNow;

                        await _context.SaveChangesAsync();

                        // اضافه کردن موجودی به کیف پول (+ پاداش رفرال در صورت وجود)
                        if (payment.PaymentType == PaymentTypes.WalletCharge)
                        {
                            try
                            {
                                await _walletReferralService.FulfillWalletChargeWithReferralAsync(payment);
                            }
                            catch (AppException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Wallet charge fulfillment failed for payment {PaymentId}", payment.Id);
                                throw AppException.Internal(ErrorCodes.PaymentFailed, ControlledErrorHelper.PaymentFailed);
                            }
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

                        var successResult = await BuildVerifiedResultAsync(payment);
                        successResult.Message = payment.PaymentType == PaymentTypes.Subscription
                            ? "اشتراک با موفقیت فعال شد"
                            : "پرداخت با موفقیت انجام شد";

                        _logger.LogInformation(
                            "پرداخت تأیید شد. PaymentId={PaymentId} UserId={UserId} Amount={Amount} Type={PaymentType} Gateway={Gateway} Authority={Authority} OrderId={OrderId}",
                            payment.Id, payment.UserId, payment.Amount, payment.PaymentType, payment.Gateway, payment.RefId, payment.OrderId);

                        var verifiedUser = await _userRepository.GetByIdAsync(payment.UserId);
                        await _audit.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Payment,
                            Action = AuditActions.PaymentVerified,
                            EntityType = AuditEntityTypes.Payment,
                            EntityId = payment.Id.ToString(),
                            ActorUserId = payment.UserId,
                            TargetUserId = payment.UserId,
                            After = PaymentAuditDetails.PaymentSnapshot(payment, verifiedUser, extra: new
                            {
                                eventType = "PaymentVerified",
                                outcome = "success",
                                fulfillment = payment.PaymentType == PaymentTypes.Subscription
                                    ? "subscription_activated"
                                    : payment.PaymentType == PaymentTypes.WalletCharge
                                        ? "wallet_credited"
                                        : "none"
                            })
                        });

                        return ApiResponse<PaymentResultDto>.CreateSuccess(successResult, successResult.Message);
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

                        _logger.LogWarning(
                            "پرداخت ناموفق. PaymentId={PaymentId} UserId={UserId} Amount={Amount} ErrorCode={ErrorCode} Message={ErrorMessage}",
                            payment.Id, payment.UserId, payment.Amount, payment.ErrorCode, payment.ErrorMessage);

                        var failedUser = await _userRepository.GetByIdAsync(payment.UserId);
                        await _audit.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Payment,
                            Action = AuditActions.PaymentVerifyFailed,
                            EntityType = AuditEntityTypes.Payment,
                            EntityId = payment.Id.ToString(),
                            ActorUserId = payment.UserId,
                            TargetUserId = payment.UserId,
                            Succeeded = false,
                            ErrorMessage = payment.ErrorMessage,
                            After = PaymentAuditDetails.PaymentSnapshot(payment, failedUser, extra: new
                            {
                                eventType = "PaymentVerifyFailed",
                                outcome = "failed"
                            })
                        });

                        var failPush = PushNotificationCopy.PaymentFailed();
                        await _pushNotifier.NotifyAsync(
                            payment.UserId,
                            NotificationCategory.SystemWarnings,
                            failPush.Title,
                            failPush.Body);

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
            var useSimulation = _configuration.GetValue("Payment:UseSimulation", false);
            var zarinPalEnabled = !string.IsNullOrWhiteSpace(_configuration["ZarinPal:MerchantId"]);

            var gateways = new List<PaymentGatewayInfoDto>
            {
                new PaymentGatewayInfoDto
                {
                    Code = PaymentGateways.Zarinpal,
                    Name = "زرین‌پال",
                    Description = "پرداخت امن از طریق درگاه زرین‌پال",
                    LogoUrl = "/images/gateways/zarinpal.png",
                    IsActive = zarinPalEnabled,
                    ComingSoon = !zarinPalEnabled
                },
                new PaymentGatewayInfoDto
                {
                    Code = PaymentGateways.Behpardakht,
                    Name = "به‌پرداخت",
                    Description = useSimulation
                        ? "درگاه آزمایشی (شبیه‌سازی) برای توسعه"
                        : "پرداخت از طریق درگاه بانکی به‌پرداخت",
                    LogoUrl = "/images/gateways/behpardakht.png",
                    IsActive = useSimulation,
                    ComingSoon = !useSimulation
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

        public async Task<ApiResponse<PaymentResultDto>> SimulateGatewayPaymentAsync(int paymentId)
        {
            try
            {
                var useSimulation = _configuration.GetValue("Payment:UseSimulation", false);
                if (!useSimulation)
                {
                    return ApiResponse<PaymentResultDto>.BadRequest("شبیه‌سازی درگاه غیرفعال است");
                }

                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return ApiResponse<PaymentResultDto>.NotFound("پرداخت یافت نشد");
                }

                if (payment.Status == PaymentStatuses.Verified)
                {
                    // همان مسیر idempotent verify — برای اشتراک، fulfill را دوباره چک می‌کند
                    return await VerifyPaymentAsync(payment.UserId, new VerifyPaymentRequestDto
                    {
                        PaymentId = payment.Id,
                        OrderId = payment.OrderId,
                        RefId = payment.RefId,
                        ResCode = "0",
                        SaleReferenceId = payment.ReferenceNumber,
                        TransactionId = payment.TransactionId,
                        CardNumber = payment.CardNumber
                    });
                }

                if (payment.Status is not (PaymentStatuses.Pending or PaymentStatuses.Processing))
                {
                    return ApiResponse<PaymentResultDto>.BadRequest("وضعیت پرداخت برای شبیه‌سازی معتبر نیست");
                }

                var refId = payment.RefId ?? $"SIMREF{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
                var saleReferenceId = (DateTime.UtcNow.Ticks % 1_000_000_000).ToString();

                return await VerifyPaymentAsync(payment.UserId, new VerifyPaymentRequestDto
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    RefId = refId,
                    ResCode = "0",
                    SaleReferenceId = saleReferenceId,
                    TransactionId = $"SIMTXN{payment.Id}",
                    CardNumber = "6037-****-****-1234"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در شبیه‌سازی پرداخت {PaymentId}", paymentId);
                throw;
            }
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

                var cancelUser = await _userRepository.GetByIdAsync(userId);

                if (payment.Status is not (PaymentStatuses.Pending or PaymentStatuses.Processing))
                {
                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Payment,
                        Action = AuditActions.PaymentCancelDenied,
                        EntityType = AuditEntityTypes.Payment,
                        EntityId = payment.Id.ToString(),
                        ActorUserId = userId,
                        TargetUserId = userId,
                        Succeeded = false,
                        ErrorMessage = "وضعیت پرداخت برای لغو معتبر نیست",
                        After = PaymentAuditDetails.PaymentSnapshot(payment, cancelUser, extra: new
                        {
                            eventType = "CancelDenied",
                            reason = "invalid_status"
                        })
                    });
                    return ApiResponse<bool>.BadRequest("فقط پرداخت‌های در انتظار یا در حال پردازش قابل لغو هستند");
                }

                // بعد از صدور Authority زرین‌پال، لغو سمت ما می‌تواند باعث گیرکردن پول در درگاه شود
                if (string.Equals(payment.Gateway, PaymentGateways.Zarinpal, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(payment.RefId))
                {
                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Payment,
                        Action = AuditActions.PaymentCancelDenied,
                        EntityType = AuditEntityTypes.Payment,
                        EntityId = payment.Id.ToString(),
                        ActorUserId = userId,
                        TargetUserId = userId,
                        Succeeded = false,
                        ErrorMessage = "لغو پس از صدور Authority مجاز نیست",
                        After = PaymentAuditDetails.PaymentSnapshot(payment, cancelUser, extra: new
                        {
                            eventType = "CancelDenied",
                            reason = "authority_already_issued"
                        })
                    });
                    return ApiResponse<bool>.BadRequest(
                        "پرداخت به درگاه ارسال شده و قابل لغو نیست. در صورت انصراف، پرداخت را در درگاه لغو کنید.");
                }

                payment.Status = PaymentStatuses.Cancelled;
                payment.ErrorMessage = "لغو توسط کاربر";
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "پرداخت لغو شد. PaymentId={PaymentId} UserId={UserId} Amount={Amount} Type={PaymentType}",
                    paymentId, userId, payment.Amount, payment.PaymentType);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Payment,
                    Action = AuditActions.PaymentCancelled,
                    EntityType = AuditEntityTypes.Payment,
                    EntityId = payment.Id.ToString(),
                    ActorUserId = userId,
                    TargetUserId = userId,
                    After = PaymentAuditDetails.PaymentSnapshot(payment, cancelUser, extra: new
                    {
                        eventType = "PaymentCancelled",
                        cancelledBy = "user"
                    })
                });

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
                var useSimulation = _configuration.GetValue("Payment:UseSimulation", false);
                if (!useSimulation)
                {
                    _logger.LogWarning("Behpardakht token requested while simulation is disabled — PaymentId: {PaymentId}", paymentId);
                    return (false, null, ControlledErrorHelper.PaymentFailed);
                }

                _logger.LogInformation("درخواست توکن به‌پرداخت (شبیه‌سازی) برای پرداخت {PaymentId}", paymentId);
                var refId = $"SIMREF{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
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
                var useSimulation = _configuration.GetValue("Payment:UseSimulation", false);
                if (!useSimulation)
                {
                    _logger.LogWarning("Behpardakht verify requested while simulation is disabled");
                    return (false, null, ControlledErrorHelper.PaymentFailed);
                }

                _logger.LogInformation("تأیید پرداخت به‌پرداخت (شبیه‌سازی) با RefId: {RefId}", refId);
                return await Task.FromResult((true, saleReferenceId.ToString(), (string?)null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تأیید پرداخت به‌پرداخت");
                return (false, null, ControlledErrorHelper.PaymentFailed);
            }
        }

        public async Task<(bool Success, string? Authority, string? PaymentUrl, string? ErrorMessage)> RequestZarinPalPaymentAsync(
            int paymentId,
            decimal amountToman,
            string description,
            string? mobile = null,
            string? orderId = null)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                var user = payment != null
                    ? await _userRepository.GetByIdAsync(payment.UserId)
                    : null;

                var callbackUrl = ResolveZarinPalServerCallbackUrl();
                if (string.IsNullOrWhiteSpace(callbackUrl))
                {
                    _logger.LogError("ZarinPal CallbackUrl is not configured for payment {PaymentId}", paymentId);
                    await WriteZarinPalAuthorityAuditAsync(
                        payment, user, amountToman, description, mobile, orderId,
                        succeeded: false, authority: null, paymentUrl: null,
                        error: "CallbackUrl missing");
                    return (false, null, null, ControlledErrorHelper.PaymentFailed);
                }

                if (amountToman != decimal.Truncate(amountToman))
                {
                    _logger.LogWarning("ZarinPal request rejected non-integer amount for payment {PaymentId}", paymentId);
                    await WriteZarinPalAuthorityAuditAsync(
                        payment, user, amountToman, description, mobile, orderId,
                        succeeded: false, authority: null, paymentUrl: null,
                        error: "Non-integer amount");
                    return (false, null, null, ControlledErrorHelper.PaymentFailed);
                }

                var amount = ToZarinPalAmount(amountToman);
                var result = await _zarinPalGatewayClient.RequestPaymentAsync(
                    amount,
                    description,
                    callbackUrl,
                    mobile: mobile,
                    orderId: orderId);

                if (!result.Success || string.IsNullOrWhiteSpace(result.Authority) || string.IsNullOrWhiteSpace(result.PaymentUrl))
                {
                    _logger.LogWarning(
                        "ZarinPal request failed for payment {PaymentId} — Code: {Code} UserId={UserId} Amount={Amount}",
                        paymentId,
                        result.Code,
                        payment?.UserId,
                        amountToman);
                    await WriteZarinPalAuthorityAuditAsync(
                        payment, user, amountToman, description, mobile, orderId,
                        succeeded: false, authority: null, paymentUrl: null,
                        error: $"ZarinPal request failed code={result.Code}");
                    return (false, null, null, ControlledErrorHelper.PaymentFailed);
                }

                _logger.LogInformation(
                    "ZarinPal authority issued. PaymentId={PaymentId} UserId={UserId} Phone={Phone} Amount={Amount} Authority={Authority} OrderId={OrderId}",
                    paymentId, payment?.UserId, mobile ?? user?.PhoneNumber, amountToman, result.Authority, orderId ?? payment?.OrderId);

                await WriteZarinPalAuthorityAuditAsync(
                    payment, user, amountToman, description, mobile, orderId,
                    succeeded: true, authority: result.Authority, paymentUrl: result.PaymentUrl,
                    error: null);

                return (true, result.Authority, result.PaymentUrl, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در درخواست پرداخت زرین‌پال برای {PaymentId}", paymentId);
                return (false, null, null, ControlledErrorHelper.PaymentFailed);
            }
        }

        public async Task<(bool Success, string ReturnHtml, int? PaymentId)> HandleZarinPalCallbackAsync(
            string? authority,
            string? status)
        {
            authority = authority?.Trim();
            status = status?.Trim();

            if (string.IsNullOrWhiteSpace(authority))
            {
                _logger.LogWarning("ZarinPal callback without Authority");
                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Payment,
                    Action = AuditActions.PaymentCallback,
                    EntityType = AuditEntityTypes.Payment,
                    Succeeded = false,
                    ErrorMessage = "Authority missing",
                    Metadata = PaymentAuditDetails.Callback(null, null, null, status, false, "Authority missing")
                });
                return (false, BuildAppReturnHtml(null, null, false, "کد مرجع پرداخت نامعتبر است"), null);
            }

            var payment = await _paymentRepository.GetByRefIdAsync(authority);
            if (payment == null)
            {
                _logger.LogWarning("ZarinPal callback — payment not found for authority {Authority}", authority);
                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Payment,
                    Action = AuditActions.PaymentCallback,
                    EntityType = AuditEntityTypes.Payment,
                    Succeeded = false,
                    ErrorMessage = "Payment not found for authority",
                    Metadata = PaymentAuditDetails.Callback(null, null, authority, status, false, "Payment not found")
                });
                return (false, BuildAppReturnHtml(null, null, false, ControlledErrorHelper.NotFound), null);
            }

            var callbackUser = await _userRepository.GetByIdAsync(payment.UserId);

            // Status != OK → پرداخت لغو/ناموفق — Verify صدا زده نمی‌شود (طبق مستندات زرین‌پال)
            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                if (payment.Status is PaymentStatuses.Pending or PaymentStatuses.Processing)
                {
                    payment.Status = PaymentStatuses.Failed;
                    payment.ErrorCode = status ?? "NOK";
                    payment.ErrorMessage = "پرداخت توسط کاربر لغو شد یا ناموفق بود";
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation(
                    "ZarinPal callback NOK. PaymentId={PaymentId} UserId={UserId} Phone={Phone} Amount={Amount} Status={Status} Authority={Authority}",
                    payment.Id, payment.UserId, callbackUser?.PhoneNumber, payment.Amount, status, authority);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Payment,
                    Action = AuditActions.PaymentCallback,
                    EntityType = AuditEntityTypes.Payment,
                    EntityId = payment.Id.ToString(),
                    ActorUserId = payment.UserId,
                    TargetUserId = payment.UserId,
                    Succeeded = false,
                    ErrorMessage = payment.ErrorMessage,
                    After = PaymentAuditDetails.Callback(
                        payment, callbackUser, authority, status, false, payment.ErrorMessage)
                });

                return (false, BuildAppReturnHtml(payment.Id, payment.PaymentType, false, payment.ErrorMessage), payment.Id);
            }

            var verifyResult = await VerifyPaymentAsync(payment.UserId, new VerifyPaymentRequestDto
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Authority = authority,
                Status = "OK",
                RefId = authority
            });

            var success = verifyResult.Success && verifyResult.Data?.Success == true;
            var message = success
                ? (verifyResult.Data?.Message ?? "پرداخت با موفقیت انجام شد")
                : (verifyResult.Data?.Message ?? ControlledErrorHelper.PaymentFailed);

            // reload برای اسنپ‌شات نهایی
            payment = await _paymentRepository.GetByIdAsync(payment.Id) ?? payment;
            _logger.LogInformation(
                "ZarinPal callback OK processed. PaymentId={PaymentId} UserId={UserId} Phone={Phone} Amount={Amount} Success={Success} Authority={Authority}",
                payment.Id, payment.UserId, callbackUser?.PhoneNumber, payment.Amount, success, authority);

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Payment,
                Action = AuditActions.PaymentCallback,
                EntityType = AuditEntityTypes.Payment,
                EntityId = payment.Id.ToString(),
                ActorUserId = payment.UserId,
                TargetUserId = payment.UserId,
                Succeeded = success,
                ErrorMessage = success ? null : message,
                After = PaymentAuditDetails.Callback(payment, callbackUser, authority, status, success, message)
            });

            return (success, BuildAppReturnHtml(payment.Id, payment.PaymentType, success, message), payment.Id);
        }

        private async Task WriteZarinPalAuthorityAuditAsync(
            Payment? payment,
            User? user,
            decimal amountToman,
            string description,
            string? mobile,
            string? orderId,
            bool succeeded,
            string? authority,
            string? paymentUrl,
            string? error)
        {
            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Payment,
                Action = succeeded
                    ? AuditActions.PaymentGatewayAuthorityIssued
                    : AuditActions.PaymentGatewayAuthorityFailed,
                EntityType = AuditEntityTypes.Payment,
                EntityId = payment?.Id.ToString(),
                ActorUserId = payment?.UserId,
                TargetUserId = payment?.UserId,
                Succeeded = succeeded,
                ErrorMessage = error,
                After = new
                {
                    occurredAtUtc = DateTime.UtcNow,
                    eventType = succeeded ? "ZarinPalAuthorityIssued" : "ZarinPalAuthorityFailed",
                    user = PaymentAuditDetails.UserSnapshot(user),
                    paymentId = payment?.Id,
                    userId = payment?.UserId,
                    phoneNumber = mobile ?? user?.PhoneNumber,
                    amount = amountToman,
                    amountLabel = $"{amountToman:N0} تومان",
                    paymentType = payment?.PaymentType,
                    gateway = payment?.Gateway ?? PaymentGateways.Zarinpal,
                    orderId = orderId ?? payment?.OrderId,
                    authority,
                    gatewayHost = Uri.TryCreate(paymentUrl, UriKind.Absolute, out var u) ? u.Host : null,
                    description,
                    status = payment?.Status
                }
            });
        }

        #region Private Methods

        private string ResolveZarinPalServerCallbackUrl()
        {
            var configured = _configuration["ZarinPal:CallbackUrl"]?.Trim();
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var apiBase = _configuration["Payment:ApiBaseUrl"]?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(apiBase))
                return $"{apiBase}/api/Payment/callback/zarinpal";

            return string.Empty;
        }

        private static int ToZarinPalAmount(decimal amountToman)
        {
            // مبالغ سیستم به تومان است؛ currency=IRT به زرین‌پال همان تومان را می‌فرستد
            return (int)decimal.Round(amountToman, 0, MidpointRounding.AwayFromZero);
        }

        private string BuildAppReturnHtml(int? paymentId, string? paymentType, bool success, string? message)
        {
            var appReturn = _configuration["ZarinPal:AppReturnUrl"]?.Trim() ?? "vapp://payment/result";
            var query = new List<string>
            {
                $"success={(success ? "1" : "0")}"
            };
            if (paymentId.HasValue)
                query.Add($"paymentId={paymentId.Value}");
            if (!string.IsNullOrWhiteSpace(paymentType))
                query.Add($"paymentType={Uri.EscapeDataString(paymentType)}");
            if (!string.IsNullOrWhiteSpace(message))
                query.Add($"message={Uri.EscapeDataString(message)}");

            var deepLink = $"{appReturn}?{string.Join("&", query)}";
            var safeDeepLink = HtmlEncoder.Default.Encode(deepLink);
            var safeMessage = HtmlEncoder.Default.Encode(message ?? (success ? "پرداخت موفق" : "پرداخت ناموفق"));
            var title = success ? "پرداخت موفق" : "پرداخت ناموفق";

            var html = $@"<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{HtmlEncoder.Default.Encode(title)}</title>
  <style>
    body {{ font-family: Tahoma, sans-serif; background:#f7f7f8; color:#111; display:flex; align-items:center; justify-content:center; min-height:100vh; margin:0; }}
    .box {{ background:#fff; padding:28px 22px; border-radius:16px; max-width:420px; width:90%; text-align:center; box-shadow:0 8px 30px rgba(0,0,0,.06); }}
    a.btn {{ display:inline-block; margin-top:18px; padding:12px 18px; background:#0f766e; color:#fff; text-decoration:none; border-radius:10px; }}
    p {{ line-height:1.8; }}
  </style>
</head>
<body>
  <div class=""box"">
    <h2>{HtmlEncoder.Default.Encode(title)}</h2>
    <p>{safeMessage}</p>
    <p>در حال بازگشت به اپلیکیشن Vapp…</p>
    <a class=""btn"" href=""{safeDeepLink}"">بازگشت به اپلیکیشن</a>
  </div>
  <script>
    setTimeout(function () {{ window.location.replace(""{safeDeepLink}""); }}, 400);
  </script>
</body>
</html>";

            return html;
        }

        private async Task<PaymentResultDto> BuildVerifiedResultAsync(Payment payment)
        {
            var user = await _userRepository.GetByIdAsync(payment.UserId);
            var result = new PaymentResultDto
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
                var snapshot = await _subscriptionEntitlementService.GetEntitlementSnapshotAsync(payment.UserId);
                var active = snapshot.ActiveSubscription;
                if (active?.Plan != null)
                {
                    var remainingDays = Math.Max(0, (int)Math.Ceiling((active.ExpiresAt - DateTime.UtcNow).TotalDays));
                    result.ActivatedSubscription = new DTOs.Subscription.CurrentSubscriptionDto
                    {
                        UserSubscriptionId = active.Id,
                        PlanId = active.Plan.Id,
                        PlanName = active.Plan.Name,
                        TierCode = active.Plan.TierCode,
                        StartDate = active.StartDate,
                        ExpiresAt = active.ExpiresAt,
                        RemainingDays = remainingDays,
                        IsActive = true,
                        IsFreePlan = false,
                        FeatureCodes = snapshot.FeatureCodes.ToList()
                    };
                }
            }

            return result;
        }

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
            return $"VP{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        }

        /// <summary>هرگز شماره کامل کارت را در audit ثبت نکن — فقط ۴ رقم آخر نگه‌داشته می‌شود.</summary>
        private static string? MaskCardNumber(string? cardNumber)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
                return cardNumber;

            var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 4)
                return "****";

            return $"******{digitsOnly[^4..]}";
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




