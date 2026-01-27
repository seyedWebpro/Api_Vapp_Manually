using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس ارسال پیامک با استفاده از API سرویس پیامکی
    /// پشتیبانی از تمام endpoint های API: Send, SendBulk, SendArray, Delivery, Inbox, Info
    /// </summary>
    public class SmsService : ISmsService
    {
        private readonly ILogger<SmsService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _senderNumber;

        public SmsService(
            ILogger<SmsService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            
            // خواندن تنظیمات از appsettings.json
            _apiKey = _configuration["Sms:ApiKey"] ?? throw new InvalidOperationException("SMS ApiKey is not configured");
            _baseUrl = _configuration["Sms:BaseUrl"] ?? throw new InvalidOperationException("SMS BaseUrl is not configured");
            _senderNumber = _configuration["Sms:SenderNumber"] ?? throw new InvalidOperationException("SMS SenderNumber is not configured");
        }

        /// <summary>
        /// تولید کد OTP تصادفی
        /// </summary>
        public Task<string> GenerateOtpAsync()
        {
            // استفاده از RandomNumberGenerator برای امنیت بیشتر
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            // تبدیل به عدد بین 1000 تا 9999
            var randomValue = BitConverter.ToUInt32(bytes, 0);
            var otp = (randomValue % 9000) + 1000;
            
            return Task.FromResult(otp.ToString());
        }

        /// <summary>
        /// ارسال OTP به شماره موبایل
        /// </summary>
        public async Task<bool> SendOtpAsync(string phoneNumber, string otpCode, string templateType = "VerifyOtp")
        {
            try
            {
                // نرمال‌سازی شماره موبایل (حذف فاصله و کاراکترهای اضافی)
                var normalizedPhone = NormalizePhoneNumber(phoneNumber);
                
                // ساخت متن پیامک بر اساس نوع Template
                string message = templateType switch
                {
                    "VerifyOtp" => $"کد تایید شما: {otpCode}",
                    "ResetPassword" => $"کد بازیابی رمز عبور: {otpCode}",
                    "Register" => $"کد تایید ثبت نام: {otpCode}",
                    "ForgotPassword" => $"کد بازیابی رمز عبور: {otpCode}", // پشتیبانی از ForgotPassword
                    "Registration" => $"کد تایید ثبت نام: {otpCode}", // پشتیبانی از Registration
                    _ => $"کد تایید شما: {otpCode}"
                };
                
                // لاگ برای دیباگ: بررسی کد OTP قبل از ارسال
                _logger.LogInformation("Sending OTP via SMS - Template: {TemplateType}, OTP Code: {OtpCode}, Phone: {PhoneNumber}", 
                    templateType, otpCode, normalizedPhone);
                
                // اضافه کردن 'لغو11' در انتهای پیامک (الزام API)
                message = $"{message}\nلغو11";

                var result = await SendSmsInternalAsync(normalizedPhone, message);
                
                // لاگ کامل Response برای بررسی کد OTP برگشتی از API
                _logger.LogInformation("SMS API Result - Status: {Status}, Message: {Message}, Sid: {Sid}, Expected OTP: {ExpectedOtp}", 
                    result.Status, result.Message, result.Sid, otpCode);
                
                // بررسی اینکه آیا API کد OTP را در Response برمی‌گرداند
                if (!string.IsNullOrEmpty(result.Message) && result.Message.Contains(otpCode))
                {
                    _logger.LogInformation("OTP code confirmed in API response - Phone: {PhoneNumber}, OTP: {OtpCode}", 
                        normalizedPhone, otpCode);
                }
                else if (!string.IsNullOrEmpty(result.Message))
                {
                    _logger.LogWarning("OTP code mismatch! Expected: {ExpectedOtp}, API Response Message: {ApiMessage}", 
                        otpCode, result.Message);
                }
                
                if (result.Status > 0)
                {
                    _logger.LogInformation("OTP sent successfully - Phone: {PhoneNumber}, OTP: {OtpCode}, Sid: {Sid}", 
                        normalizedPhone, otpCode, result.Sid);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to send OTP - Phone: {PhoneNumber}, Status: {Status}, Message: {Message}", 
                        normalizedPhone, result.Status, result.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP to {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        /// <summary>
        /// نرمال‌سازی شماره موبایل (حذف فاصله و کاراکترهای اضافی)
        /// </summary>
        private string NormalizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            // حذف فاصله‌ها و کاراکترهای اضافی
            var normalized = phoneNumber.Replace(" ", "")
                                       .Replace("-", "")
                                       .Replace("_", "")
                                       .Replace("(", "")
                                       .Replace(")", "")
                                       .Trim();

            return normalized;
        }

        /// <summary>
        /// ارسال پیامک تکی (متد داخلی برای استفاده در SendOtpAsync)
        /// </summary>
        private async Task<SendSmsResponseDto> SendSmsInternalAsync(string mobile, string message, string? senderNumber = null)
        {
            try
            {
                var finalSenderNumber = senderNumber ?? _senderNumber;
                
                var request = new SendSmsRequestDto
                {
                    SenderNumber = finalSenderNumber,
                    Mobile = mobile,
                    Message = message
                };

                // لاگ برای دیباگ - لاگ کامل Request
                var requestJson = JsonSerializer.Serialize(request);
                _logger.LogInformation("Sending SMS Request - SenderNumber: {SenderNumber}, Mobile: {Mobile}, Message: {Message}, Full Request: {RequestJson}", 
                    finalSenderNumber, mobile, message, requestJson);

                var response = await SendRequestAsync<SendSmsRequestDto, SendSmsResponseDto>(
                    "/api/v1/Send", 
                    request);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS to {Mobile}", mobile);
                throw;
            }
        }

        #region Public SMS Methods

        /// <summary>
        /// ارسال پیامک تکی
        /// </summary>
        public async Task<ApiResponse<SendSmsResponseDto>> SendSmsAsync(SendSmsRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Mobile))
                {
                    return ApiResponse<SendSmsResponseDto>.BadRequest("شماره موبایل الزامی است");
                }

                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return ApiResponse<SendSmsResponseDto>.BadRequest("متن پیام الزامی است");
                }

                var normalizedMobile = NormalizePhoneNumber(request.Mobile);
                var finalSenderNumber = string.IsNullOrWhiteSpace(request.SenderNumber) ? _senderNumber : request.SenderNumber;

                var sendRequest = new SendSmsRequestDto
                {
                    SenderNumber = finalSenderNumber,
                    Mobile = normalizedMobile,
                    Message = request.Message
                };

                var response = await SendRequestAsync<SendSmsRequestDto, SendSmsResponseDto>(
                    "/api/v1/Send",
                    sendRequest);

                // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                // Status > 0 هم یعنی موفقیت
                // Status < 0 یعنی خطا
                if (response.Sid > 0 || response.Status > 0)
                {
                    _logger.LogInformation("SMS sent successfully - Mobile: {Mobile}, Sid: {Sid}, Status: {Status}", 
                        normalizedMobile, response.Sid, response.Status);
                    return ApiResponse<SendSmsResponseDto>.CreateSuccess(response, "پیامک با موفقیت ارسال شد");
                }
                else
                {
                    _logger.LogWarning("SMS send failed - Mobile: {Mobile}, Status: {Status}, Message: {Message}",
                        normalizedMobile, response.Status, response.Message);
                    return ApiResponse<SendSmsResponseDto>.BadRequest($"خطا در ارسال پیامک: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS to {Mobile}", request.Mobile);
                return ApiResponse<SendSmsResponseDto>.InternalServerError($"خطا در ارسال پیامک: {ex.Message}");
            }
        }

        /// <summary>
        /// ارسال پیامک گروهی (Bulk)
        /// </summary>
        public async Task<ApiResponse<SendBulkResponseDto>> SendBulkSmsAsync(SendBulkRequestDto request)
        {
            try
            {
                if (request.Mobiles == null || request.Mobiles.Count == 0)
                {
                    return ApiResponse<SendBulkResponseDto>.BadRequest("لیست شماره موبایل‌ها الزامی است");
                }

                if (request.Mobiles.Count > 2000)
                {
                    return ApiResponse<SendBulkResponseDto>.BadRequest("حداکثر 2000 شماره در یک درخواست قابل ارسال است");
                }

                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return ApiResponse<SendBulkResponseDto>.BadRequest("متن پیام الزامی است");
                }

                // نرمال‌سازی شماره‌ها
                var normalizedMobiles = request.Mobiles.Select(NormalizePhoneNumber).ToList();
                var finalSenderNumber = string.IsNullOrWhiteSpace(request.SenderNumber) ? _senderNumber : request.SenderNumber;

                var sendRequest = new SendBulkRequestDto
                {
                    SenderNumber = finalSenderNumber,
                    Mobiles = normalizedMobiles,
                    Message = request.Message
                };

                var response = await SendRequestAsync<SendBulkRequestDto, SendBulkResponseDto>(
                    "/api/v1/SendBulk",
                    sendRequest);

                // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                if (response.Sid > 0 || response.Status > 0)
                {
                    _logger.LogInformation("Bulk SMS sent successfully - Count: {Count}, Sid: {Sid}, Status: {Status}",
                        normalizedMobiles.Count, response.Sid, response.Status);
                    return ApiResponse<SendBulkResponseDto>.CreateSuccess(response, "پیامک‌های گروهی با موفقیت ارسال شدند");
                }
                else
                {
                    _logger.LogWarning("Bulk SMS send failed - Status: {Status}, Message: {Message}",
                        response.Status, response.Messege);
                    return ApiResponse<SendBulkResponseDto>.BadRequest($"خطا در ارسال پیامک‌های گروهی: {response.Messege}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bulk SMS");
                return ApiResponse<SendBulkResponseDto>.InternalServerError($"خطا در ارسال پیامک‌های گروهی: {ex.Message}");
            }
        }

        /// <summary>
        /// ارسال پیامک نظیر به نظیر (هر شماره متن خودش)
        /// </summary>
        public async Task<ApiResponse<SendArrayResponseDto>> SendArraySmsAsync(SendArrayRequestDto request)
        {
            try
            {
                if (request.Mobiles == null || request.Mobiles.Count == 0)
                {
                    return ApiResponse<SendArrayResponseDto>.BadRequest("لیست شماره موبایل‌ها الزامی است");
                }

                if (request.Message == null || request.Message.Count == 0)
                {
                    return ApiResponse<SendArrayResponseDto>.BadRequest("لیست پیام‌ها الزامی است");
                }

                if (request.Mobiles.Count != request.Message.Count)
                {
                    return ApiResponse<SendArrayResponseDto>.BadRequest("طول آرایه موبایل‌ها باید برابر با آرایه پیام‌ها باشد");
                }

                if (request.Mobiles.Count > 2000)
                {
                    return ApiResponse<SendArrayResponseDto>.BadRequest("حداکثر 2000 پیام در یک درخواست قابل ارسال است");
                }

                // نرمال‌سازی شماره‌ها
                var normalizedMobiles = request.Mobiles.Select(NormalizePhoneNumber).ToList();
                var finalSenderNumber = string.IsNullOrWhiteSpace(request.SenderNumber) ? _senderNumber : request.SenderNumber;

                var sendRequest = new SendArrayRequestDto
                {
                    SenderNumber = finalSenderNumber,
                    Mobiles = normalizedMobiles,
                    Message = request.Message
                };

                var response = await SendRequestAsync<SendArrayRequestDto, SendArrayResponseDto>(
                    "/api/v1/SendArray",
                    sendRequest);

                // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                if (response.Sid > 0 || response.Status > 0)
                {
                    _logger.LogInformation("Array SMS sent successfully - Count: {Count}, Sid: {Sid}, Status: {Status}",
                        normalizedMobiles.Count, response.Sid, response.Status);
                    return ApiResponse<SendArrayResponseDto>.CreateSuccess(response, "پیامک‌های نظیر به نظیر با موفقیت ارسال شدند");
                }
                else
                {
                    _logger.LogWarning("Array SMS send failed - Status: {Status}, Message: {Message}",
                        response.Status, response.Message);
                    return ApiResponse<SendArrayResponseDto>.BadRequest($"خطا در ارسال پیامک‌های نظیر به نظیر: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending array SMS");
                return ApiResponse<SendArrayResponseDto>.InternalServerError($"خطا در ارسال پیامک‌های نظیر به نظیر: {ex.Message}");
            }
        }

        /// <summary>
        /// دریافت وضعیت ارسال پیامک (Delivery)
        /// </summary>
        public async Task<ApiResponse<DeliveryResponseDto>> GetDeliveryStatusAsync(long sid)
        {
            try
            {
                if (sid <= 0)
                {
                    return ApiResponse<DeliveryResponseDto>.BadRequest("شناسه پیامک (Sid) معتبر نیست");
                }

                var request = new DeliveryRequestDto { Sid = sid };

                var response = await SendRequestAsync<DeliveryRequestDto, DeliveryResponseDto>(
                    "/api/v1/Delivery",
                    request);

                if (response.Status >= 0)
                {
                    _logger.LogInformation("Delivery status retrieved successfully - Sid: {Sid}, Count: {Count}",
                        sid, response.Deliveries?.Count ?? 0);
                    return ApiResponse<DeliveryResponseDto>.CreateSuccess(response, "وضعیت ارسال با موفقیت دریافت شد");
                }
                else
                {
                    _logger.LogWarning("Delivery status retrieval failed - Sid: {Sid}, Status: {Status}, Message: {Message}",
                        sid, response.Status, response.Messege);
                    return ApiResponse<DeliveryResponseDto>.BadRequest($"خطا در دریافت وضعیت: {response.Messege}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting delivery status for Sid: {Sid}", sid);
                return ApiResponse<DeliveryResponseDto>.InternalServerError($"خطا در دریافت وضعیت ارسال: {ex.Message}");
            }
        }

        /// <summary>
        /// دریافت پیامک‌های ورودی (Inbox) - فقط برای خطوط اختصاصی
        /// </summary>
        public async Task<ApiResponse<InboxResponseDto>> GetInboxAsync(InboxRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.SenderNumber))
                {
                    return ApiResponse<InboxResponseDto>.BadRequest("شماره خط ارسالی الزامی است");
                }

                if (request.Count <= 0 || request.Count > 100)
                {
                    request.Count = 100; // تنظیم به حداکثر مجاز
                }

                var response = await SendRequestAsync<InboxRequestDto, InboxResponseDto>(
                    "/api/v1/Inbox",
                    request);

                if (response.Status >= 0)
                {
                    _logger.LogInformation("Inbox retrieved successfully - SenderNumber: {SenderNumber}, Count: {Count}",
                        request.SenderNumber, response.Inboxs?.Count ?? 0);
                    return ApiResponse<InboxResponseDto>.CreateSuccess(response, "پیامک‌های ورودی با موفقیت دریافت شدند");
                }
                else
                {
                    _logger.LogWarning("Inbox retrieval failed - SenderNumber: {SenderNumber}, Status: {Status}, Message: {Message}",
                        request.SenderNumber, response.Status, response.Message);
                    return ApiResponse<InboxResponseDto>.BadRequest($"خطا در دریافت پیامک‌های ورودی: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inbox for SenderNumber: {SenderNumber}", request.SenderNumber);
                return ApiResponse<InboxResponseDto>.InternalServerError($"خطا در دریافت پیامک‌های ورودی: {ex.Message}");
            }
        }

        /// <summary>
        /// دریافت موجودی کیف پول (Info)
        /// </summary>
        public async Task<ApiResponse<InfoResponseDto>> GetWalletInfoAsync()
        {
            try
            {
                var response = await SendGetRequestAsync<InfoResponseDto>("/api/v1/Info");

                if (response.Status >= 0)
                {
                    _logger.LogInformation("Wallet info retrieved successfully - Balance: {Balance}",
                        response.WalletBalance);
                    return ApiResponse<InfoResponseDto>.CreateSuccess(response, "موجودی کیف پول با موفقیت دریافت شد");
                }
                else
                {
                    _logger.LogWarning("Wallet info retrieval failed - Status: {Status}, Message: {Message}",
                        response.Status, response.Message);
                    return ApiResponse<InfoResponseDto>.BadRequest($"خطا در دریافت موجودی: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallet info");
                return ApiResponse<InfoResponseDto>.InternalServerError($"خطا در دریافت موجودی کیف پول: {ex.Message}");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// متد کمکی برای ارسال درخواست POST
        /// </summary>
        private async Task<TResponse> SendRequestAsync<TRequest, TResponse>(string endpoint, TRequest request)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            var url = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // لاگ کامل Response برای دیباگ
            _logger.LogInformation("SMS API Response - Endpoint: {Endpoint}, StatusCode: {StatusCode}, ResponseContent: {ResponseContent}", 
                endpoint, response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SMS API Error - Endpoint: {Endpoint}, Status: {StatusCode}, Response: {Response}", 
                    endpoint, response.StatusCode, responseContent);
                
                // تلاش برای خواندن خطا از Response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<SendSmsResponseDto>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (errorResponse != null && errorResponse.Status < 0)
                    {
                        throw new InvalidOperationException($"SMS API Error: {errorResponse.Message}");
                    }
                }
                catch
                {
                    // اگر نتوانستیم parse کنیم، خطای عمومی می‌دهیم
                }
                
                response.EnsureSuccessStatusCode();
            }

            var result = JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize response");
            }

            return result;
        }

        /// <summary>
        /// متد کمکی برای ارسال درخواست GET
        /// </summary>
        private async Task<TResponse> SendGetRequestAsync<TResponse>(string endpoint)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            var url = $"{_baseUrl.TrimEnd('/')}{endpoint}";

            var response = await client.GetAsync(url);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // لاگ کامل Response برای دیباگ
            _logger.LogInformation("SMS API GET Response - Endpoint: {Endpoint}, StatusCode: {StatusCode}, ResponseContent: {ResponseContent}", 
                endpoint, response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SMS API GET Error - Endpoint: {Endpoint}, Status: {StatusCode}, Response: {Response}", 
                    endpoint, response.StatusCode, responseContent);
                response.EnsureSuccessStatusCode();
            }

            var result = JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize response");
            }

            return result;
        }

        #endregion
    }
}



