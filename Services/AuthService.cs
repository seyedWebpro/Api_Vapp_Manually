using Api_Vapp.Data;
using Api_Vapp.DTOs.Auth;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching;

namespace Api_Vapp.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IJwtService _jwtService;
        private readonly ISmsService _smsService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly ITokenBlacklistService _tokenBlacklistService;
        private readonly IMemoryCache _cache;
        private readonly Api_Context _context;
        private readonly ILogger<AuthService> _logger;
        
        private const int OtpExpirationMinutes = 5;
        private const int MaxOtpAttempts = 5; // حداکثر تلاش برای OTP
        private const int OtpLockoutMinutes = 15; // زمان قفل شدن پس از تلاش‌های ناموفق
        private const int OtpRateLimitMinutes = 2; // حداقل فاصله بین ارسال OTP
        private const string UserNotFoundLoginMessage = "کاربری با این شماره تلفن یافت نشد. لطفاً ابتدا ثبت‌نام کنید";

        public AuthService(
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IJwtService jwtService,
            ISmsService smsService,
            IRefreshTokenService refreshTokenService,
            ITokenBlacklistService tokenBlacklistService,
            IMemoryCache cache,
            Api_Context context,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _jwtService = jwtService;
            _smsService = smsService;
            _refreshTokenService = refreshTokenService;
            _tokenBlacklistService = tokenBlacklistService;
            _cache = cache;
            _context = context;
            _logger = logger;
        }

        private async Task<string> GenerateAccessTokenWithRolesAsync(User user)
        {
            var userRoles = await _userRoleRepository.GetActiveUserRolesAsync(user.Id);
            var roleNames = userRoles
                .Where(ur => ur.Role != null && ur.Role.IsActive && !ur.Role.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToList();

            return _jwtService.GenerateAccessToken(user, roleNames);
        }

        /// <summary>
        /// بررسی Rate Limit و محاسبه زمان باقی‌مانده
        /// </summary>
        private (bool isRateLimited, int? retryAfterSeconds) CheckRateLimit(string phoneNumber)
        {
            var rateLimitKey = $"OtpRateLimit_{phoneNumber}";
            
            if (_cache.TryGetValue(rateLimitKey, out RateLimitInfoDto? rateLimitInfo) && rateLimitInfo != null)
            {
                // بررسی زمان انقضا (استفاده از >= برای اطمینان از حذف cache منقضی شده)
                if (rateLimitInfo.ExpiresAt > DateTime.UtcNow)
                {
                    var remainingSeconds = (int)Math.Ceiling((rateLimitInfo.ExpiresAt - DateTime.UtcNow).TotalSeconds);
                    // اطمینان از اینکه مقدار منفی نباشد
                    if (remainingSeconds > 0)
                    {
                        return (true, remainingSeconds);
                    }
                }
                
                // Cache منقضی شده یا زمان باقی‌مانده صفر است، حذف می‌کنیم
                _cache.Remove(rateLimitKey);
            }
            
            return (false, null);
        }

        /// <summary>
        /// تنظیم Rate Limit با زمان انقضا
        /// </summary>
        private void SetRateLimit(string phoneNumber, int minutes)
        {
            var rateLimitKey = $"OtpRateLimit_{phoneNumber}";
            var rateLimitInfo = new RateLimitInfoDto
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(minutes),
                IsActive = true
            };
            
            // استفاده از MemoryCacheEntryOptions برای اطمینان از overwrite شدن داده‌های قدیمی
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes),
                Priority = CacheItemPriority.Normal,
                Size = 1 // الزامی است وقتی SizeLimit تنظیم شده باشد
            };
            
            _cache.Set(rateLimitKey, rateLimitInfo, cacheOptions);
        }

        /// <summary>
        /// تنظیم Cache با MemoryCacheEntryOptions برای اطمینان از overwrite شدن داده‌های قدیمی
        /// </summary>
        private void SetCacheData<T>(string key, T data, int expirationMinutes)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expirationMinutes),
                Priority = CacheItemPriority.Normal,
                SlidingExpiration = null, // استفاده از Absolute Expiration برای جلوگیری از تمدید خودکار
                Size = 1 // الزامی است وقتی SizeLimit تنظیم شده باشد
            };
            
            _cache.Set(key, data, cacheOptions);
        }

        // DEV ONLY — TODO(production): قبل از release برای کاربران نهایی این متد را حذف کنید (جستجو: LogDevOtpForDevelopment)
        private void LogDevOtpForDevelopment(string phoneNumber, string otpCode, string purpose)
        {
            DevOtpLogger.Write(_logger, phoneNumber, otpCode, purpose);
        }

        // DEV ONLY — TODO(production): قبل از release این متد و فیلد OtpCode در SendOtpResponseDto را حذف کنید (جستجو: CreateSuccessOtpResponse)
        private static SendOtpResponseDto CreateSuccessOtpResponse(string message, string otpCode, int expiresInSeconds)
        {
            return new SendOtpResponseDto
            {
                StatusCode = 200,
                Success = true,
                Message = message,
                ExpiresInSeconds = expiresInSeconds,
                OtpCode = otpCode
            };
        }

        private static SendOtpResponseDto CreateSmsFailedOtpResponse()
        {
            return new SendOtpResponseDto
            {
                StatusCode = 503,
                Success = false,
                Message = ControlledErrorHelper.SmsFailed,
                ExpiresInSeconds = 0
            };
        }

        private void ClearOtpRateLimit(string phoneNumber)
        {
            _cache.Remove($"OtpRateLimit_{phoneNumber}");
        }

        private void RollbackFailedOtpSend(string phoneNumber, string otpCacheKey)
        {
            _cache.Remove(otpCacheKey);
            ClearOtpRateLimit(phoneNumber);
        }

        private async Task<(bool Sent, SendOtpResponseDto? FailureResponse)> SendOtpOrFailAsync(
            string phoneNumber,
            string otpCode,
            string templateType,
            string otpCacheKey,
            string purpose,
            string? ipAddress = null)
        {
            var sent = await _smsService.SendOtpAsync(phoneNumber, otpCode, templateType);
            if (sent)
            {
                return (true, null);
            }

            _logger.LogWarning(
                "OTP SMS delivery failed for {Purpose} - Phone: {PhoneNumber} from IP {IpAddress}",
                purpose, phoneNumber, ipAddress);

            RollbackFailedOtpSend(phoneNumber, otpCacheKey);
            return (false, CreateSmsFailedOtpResponse());
        }

        private async Task<(User? User, SendOtpResponseDto? BlockedResponse)> ResolveLoginUserForOtpAsync(string phoneNumber)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber);

            if (user == null || user.IsDeleted)
            {
                return (null, new SendOtpResponseDto
                {
                    StatusCode = 404,
                    Success = false,
                    Message = UserNotFoundLoginMessage,
                    ExpiresInSeconds = 0
                });
            }

            if (!user.IsActive)
            {
                return (null, new SendOtpResponseDto
                {
                    StatusCode = 403,
                    Success = false,
                    Message = ControlledErrorHelper.InactiveUserAccount,
                    ExpiresInSeconds = 0
                });
            }

            return (user, null);
        }

        private async Task<(User? User, AuthResponseDto? BlockedResponse)> ResolveLoginUserForVerifyAsync(string phoneNumber)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber);

            if (user == null || user.IsDeleted)
            {
                return (null, new AuthResponseDto
                {
                    StatusCode = 400,
                    Success = false,
                    Message = "کد تایید یا شماره تلفن صحیح نیست"
                });
            }

            if (!user.IsActive)
            {
                return (null, new AuthResponseDto
                {
                    StatusCode = 403,
                    Success = false,
                    Message = ControlledErrorHelper.InactiveUserAccount
                });
            }

            return (user, null);
        }

        private async Task<bool> UserHasAdminRoleAsync(int userId)
        {
            var userRoles = await _userRoleRepository.GetActiveUserRolesAsync(userId);
            return userRoles.Any(ur =>
                ur.Role != null
                && ur.Role.IsActive
                && !ur.Role.IsDeleted
                && string.Equals(ur.Role.Name, "Admin", StringComparison.OrdinalIgnoreCase));
        }

        private SendOtpResponseDto CreateAdminPanelAccessDeniedOtpResponse()
        {
            return new SendOtpResponseDto
            {
                StatusCode = 403,
                Success = false,
                Message = ControlledErrorHelper.AdminPanelAccessDenied,
                ExpiresInSeconds = 0
            };
        }

        private AuthResponseDto CreateAdminPanelAccessDeniedAuthResponse()
        {
            return new AuthResponseDto
            {
                StatusCode = 403,
                Success = false,
                Message = ControlledErrorHelper.AdminPanelAccessDenied
            };
        }

        private async Task<SendOtpResponseDto?> BlockAdminPanelAccessForOtpAsync(User user)
        {
            if (await UserHasAdminRoleAsync(user.Id))
            {
                return null;
            }

            _logger.LogWarning("Admin panel OTP blocked for non-admin user {UserId}", user.Id);
            return CreateAdminPanelAccessDeniedOtpResponse();
        }

        public async Task<SendOtpResponseDto> RegisterAsync(RegisterDto registerDto, string? ipAddress = null)
        {
            try
            {
                // Rate Limiting: بررسی فاصله زمانی بین درخواست‌ها
                var (isRateLimited, retryAfterSeconds) = CheckRateLimit(registerDto.PhoneNumber);
                if (isRateLimited)
                {
                    _logger.LogWarning("محدودیت نرخ برای شماره تلفن {PhoneNumber} از IP {IpAddress} exceeded", 
                        registerDto.PhoneNumber, ipAddress);
                    return new SendOtpResponseDto
                    {
                        StatusCode = 429, // Too Many Requests
                        Success = false,
                        Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                        ExpiresInSeconds = 0,
                        RetryAfterSeconds = retryAfterSeconds
                    };
                }

                // بررسی وجود کاربر با شماره تلفن
                var existsByPhone = await _userRepository.ExistsByPhoneNumberAsync(registerDto.PhoneNumber);

                if (existsByPhone)
                {
                    return new SendOtpResponseDto
                    {
                        StatusCode = 409, // Conflict
                        Success = false,
                        Message = "شماره تلفن وارد شده قبلاً در سیستم ثبت شده است. لطفاً از بخش ورود استفاده کنید",
                        ExpiresInSeconds = 0
                    };
                }

                // بررسی تکراری بودن کد ملی
                var existsByNationalId = await _userRepository.ExistsByNationalIdAsync(registerDto.NationalId);

                if (existsByNationalId)
                {
                    return new SendOtpResponseDto
                    {
                        StatusCode = 409, // Conflict
                        Success = false,
                        Message = "کد ملی وارد شده قبلاً در سیستم ثبت شده است",
                        ExpiresInSeconds = 0
                    };
                }

                // تولید و ارسال OTP
                var otpCode = await _smsService.GenerateOtpAsync();
                var cacheKey = $"RegisterOtp_{registerDto.PhoneNumber}";
                var cacheData = new RegisterOtpCacheDto
                {
                    OtpCode = otpCode,
                    FullName = registerDto.FullName,
                    NationalId = registerDto.NationalId
                };

                // استفاده از متد helper برای اطمینان از overwrite شدن داده‌های قدیمی
                SetCacheData(cacheKey, cacheData, OtpExpirationMinutes);
                
                // Rate Limiting: ثبت درخواست برای جلوگیری از ارسال مکرر
                SetRateLimit(registerDto.PhoneNumber, OtpRateLimitMinutes);
                
                // Reset attempt counter
                var attemptKey = $"OtpAttempt_{registerDto.PhoneNumber}_register";
                _cache.Remove(attemptKey);

                var (sent, failureResponse) = await SendOtpOrFailAsync(
                    registerDto.PhoneNumber,
                    otpCode,
                    "Register",
                    cacheKey,
                    "Register",
                    ipAddress);
                if (!sent)
                {
                    return failureResponse!;
                }
                
                _logger.LogInformation("Registration OTP generated for {PhoneNumber} from IP {IpAddress}",
                    registerDto.PhoneNumber, ipAddress);

                LogDevOtpForDevelopment(registerDto.PhoneNumber, otpCode, "Register");

                return CreateSuccessOtpResponse(
                    "کد تایید ارسال شد",
                    otpCode,
                    OtpExpirationMinutes * 60);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RegisterAsync for phone {PhoneNumber}", registerDto.PhoneNumber);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<AuthResponseDto> VerifyRegistrationOtpAsync(VerifyOtpDto verifyOtpDto, string? ipAddress = null)
        {
            try
            {
                // بررسی محدودیت تلاش
                var attemptKey = $"OtpAttempt_{verifyOtpDto.PhoneNumber}_register";
                var attemptData = _cache.Get<OtpAttemptCacheDto>(attemptKey);
                
                if (attemptData?.LockedUntil != null && attemptData.LockedUntil > DateTime.UtcNow)
                {
                    var remainingMinutes = (int)(attemptData.LockedUntil.Value - DateTime.UtcNow).TotalMinutes;
                    _logger.LogWarning("OTP attempts locked for {PhoneNumber} from IP {IpAddress}", 
                        verifyOtpDto.PhoneNumber, ipAddress);
                    return new AuthResponseDto
                    {
                        StatusCode = 423, // Locked
                        Success = false,
                        Message = $"حساب شما به دلیل تلاش‌های ناموفق به مدت {remainingMinutes} دقیقه قفل شده است"
                    };
                }

                var cacheKey = $"RegisterOtp_{verifyOtpDto.PhoneNumber}";
                
                // بررسی وجود OTP در cache
                if (!_cache.TryGetValue(cacheKey, out RegisterOtpCacheDto? cachedData) || cachedData == null)
                {
                    // اگر OTP در cache وجود نداشت، ممکن است شماره تلفن اشتباه باشد یا OTP منقضی شده باشد
                    // برای امنیت، پیام کلی می‌دهیم
                    return new AuthResponseDto
                    {
                        StatusCode = 400, // Bad Request
                        Success = false,
                        Message = ControlledErrorHelper.OtpExpired
                    };
                }

                // بررسی صحت کد تایید
                var cachedOtpCode = cachedData.OtpCode?.Trim() ?? string.Empty;
                var userOtpCode = verifyOtpDto.OtpCode?.Trim() ?? string.Empty;
                
                // DEV ONLY — TODO(production): قبل از release برای کاربران نهایی این لاگ را حذف کنید (جستجو: DEV-OTP-VERIFY)
                _logger.LogInformation("[DEV-OTP-VERIFY] Cached OTP: {CachedOtp}, User Input: {UserOtp}, Phone: {PhoneNumber}",
                    cachedOtpCode, userOtpCode, verifyOtpDto.PhoneNumber);
                
                if (cachedOtpCode != userOtpCode)
                {
                    // افزایش تعداد تلاش‌های ناموفق
                    if (attemptData == null)
                    {
                        attemptData = new OtpAttemptCacheDto
                        {
                            AttemptCount = 1,
                            FirstAttemptTime = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        attemptData.AttemptCount++;
                    }

                    // قفل کردن پس از تلاش‌های ناموفق
                    if (attemptData.AttemptCount >= MaxOtpAttempts)
                    {
                        attemptData.LockedUntil = DateTime.UtcNow.AddMinutes(OtpLockoutMinutes);
                        _logger.LogWarning("OTP attempts exceeded for {PhoneNumber} from IP {IpAddress}. Account locked.", 
                            verifyOtpDto.PhoneNumber, ipAddress);
                    }

                    SetCacheData(attemptKey, attemptData, OtpLockoutMinutes + 5);

                    return new AuthResponseDto
                    {
                        StatusCode = 400, // Bad Request
                        Success = false,
                        Message = ControlledErrorHelper.OtpIncorrect
                    };
                }

                // بررسی تکراری بودن کد ملی (قبل از ایجاد کاربر)
                var existsByNationalId = await _userRepository.ExistsByNationalIdAsync(cachedData.NationalId);
                if (existsByNationalId)
                {
                    return new AuthResponseDto
                    {
                        StatusCode = 409, // Conflict
                        Success = false,
                        Message = "کد ملی وارد شده قبلاً در سیستم ثبت شده است"
                    };
                }

                // استفاده از Transaction برای اطمینان از یکپارچگی داده‌ها
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // ایجاد کاربر جدید
                    var user = new User
                    {
                        PhoneNumber = verifyOtpDto.PhoneNumber,
                        FullName = cachedData.FullName,
                        NationalId = cachedData.NationalId,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                        IsPhoneVerified = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _userRepository.AddAsync(user);

                    // تولید توکن‌ها
                    var accessToken = await GenerateAccessTokenWithRolesAsync(user);
                    var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id);

                    await transaction.CommitAsync();

                    // حذف OTP و attempt counter از کش
                    _cache.Remove(cacheKey);
                    _cache.Remove(attemptKey);

                    _logger.LogInformation("User registered successfully: {PhoneNumber} from IP {IpAddress}", 
                        verifyOtpDto.PhoneNumber, ipAddress);

                    return new AuthResponseDto
                    {
                        StatusCode = 201, // Created
                        Success = true,
                        Message = "ثبت‌نام با موفقیت انجام شد",
                        Tokens = new TokenResponseDto
                        {
                            AccessToken = accessToken,
                            RefreshToken = refreshToken.Token,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtService.GetAccessTokenExpirationMinutes()),
                            RefreshTokenExpiresAt = refreshToken.ExpiresAt
                        },
                        User = new UserInfoDto
                        {
                            Id = user.Id,
                            FullName = user.FullName ?? string.Empty,
                            PhoneNumber = user.PhoneNumber,
                            IsPhoneVerified = user.IsPhoneVerified
                        }
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VerifyRegistrationOtpAsync for phone {PhoneNumber}", verifyOtpDto.PhoneNumber);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<SendOtpResponseDto> ResendRegistrationOtpAsync(LoginDto loginDto, string? ipAddress = null)
        {
            try
            {
                // Rate Limiting: بررسی فاصله زمانی بین درخواست‌ها
                var (isRateLimited, retryAfterSeconds) = CheckRateLimit(loginDto.PhoneNumber);
                if (isRateLimited)
                {
                    _logger.LogWarning("Rate limit exceeded for resend registration OTP {PhoneNumber} from IP {IpAddress}", 
                        loginDto.PhoneNumber, ipAddress);
                    return new SendOtpResponseDto
                    {
                        StatusCode = 429, // Too Many Requests
                        Success = false,
                        Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                        ExpiresInSeconds = 0,
                        RetryAfterSeconds = retryAfterSeconds
                    };
                }

                // بررسی وجود OTP قبلی در cache
                var cacheKey = $"RegisterOtp_{loginDto.PhoneNumber}";
                if (!_cache.TryGetValue(cacheKey, out RegisterOtpCacheDto? cachedData) || cachedData == null)
                {
                    return new SendOtpResponseDto
                    {
                        StatusCode = 404, // Not Found
                        Success = false,
                        Message = "کد تایید قبلی یافت نشد. لطفاً ابتدا ثبت‌نام کنید",
                        ExpiresInSeconds = 0
                    };
                }

                // بررسی وجود کاربر با شماره تلفن (نباید کاربری با این شماره وجود داشته باشد)
                var existsByPhone = await _userRepository.ExistsByPhoneNumberAsync(loginDto.PhoneNumber);
                if (existsByPhone)
                {
                    return new SendOtpResponseDto
                    {
                        StatusCode = 409, // Conflict
                        Success = false,
                        Message = "شماره تلفن وارد شده قبلاً در سیستم ثبت شده است. لطفاً از بخش ورود استفاده کنید",
                        ExpiresInSeconds = 0
                    };
                }

                // تولید و ارسال OTP جدید
                var otpCode = await _smsService.GenerateOtpAsync();
                var newCacheData = new RegisterOtpCacheDto
                {
                    OtpCode = otpCode,
                    FullName = cachedData.FullName,
                    NationalId = cachedData.NationalId
                };

                // استفاده از متد helper برای اطمینان از overwrite شدن داده‌های قدیمی
                SetCacheData(cacheKey, newCacheData, OtpExpirationMinutes);
                
                // Rate Limiting: ثبت درخواست برای جلوگیری از ارسال مکرر
                SetRateLimit(loginDto.PhoneNumber, OtpRateLimitMinutes);
                
                // Reset attempt counter
                var attemptKey = $"OtpAttempt_{loginDto.PhoneNumber}_register";
                _cache.Remove(attemptKey);

                var sent = await _smsService.SendOtpAsync(loginDto.PhoneNumber, otpCode, "Register");
                if (!sent)
                {
                    _logger.LogWarning(
                        "OTP SMS delivery failed for Register resend - Phone: {PhoneNumber} from IP {IpAddress}",
                        loginDto.PhoneNumber, ipAddress);
                    SetCacheData(cacheKey, cachedData, OtpExpirationMinutes);
                    ClearOtpRateLimit(loginDto.PhoneNumber);
                    return CreateSmsFailedOtpResponse();
                }
                
                _logger.LogInformation("Registration OTP resent for {PhoneNumber} from IP {IpAddress}",
                    loginDto.PhoneNumber, ipAddress);

                LogDevOtpForDevelopment(loginDto.PhoneNumber, otpCode, "Register");

                return CreateSuccessOtpResponse(
                    "کد تایید مجدداً ارسال شد",
                    otpCode,
                    OtpExpirationMinutes * 60);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResendRegistrationOtpAsync for phone {PhoneNumber}", loginDto.PhoneNumber);
                throw;
            }
        }

        public async Task<SendOtpResponseDto> LoginAsync(LoginDto loginDto, string? ipAddress = null, bool requireAdminPanelAccess = false)
        {
            try
            {
                // Rate Limiting
                var (isRateLimited, retryAfterSeconds) = CheckRateLimit(loginDto.PhoneNumber);
                if (isRateLimited)
                {
                    _logger.LogWarning("Rate limit exceeded for login {PhoneNumber} from IP {IpAddress}", 
                        loginDto.PhoneNumber, ipAddress);
                    return new SendOtpResponseDto
                    {
                        StatusCode = 429, // Too Many Requests
                        Success = false,
                        Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                        ExpiresInSeconds = 0,
                        RetryAfterSeconds = retryAfterSeconds
                    };
                }

                var (user, blockedResponse) = await ResolveLoginUserForOtpAsync(loginDto.PhoneNumber);
                if (blockedResponse != null)
                {
                    return blockedResponse;
                }

                if (requireAdminPanelAccess && user != null)
                {
                    var adminBlock = await BlockAdminPanelAccessForOtpAsync(user);
                    if (adminBlock != null)
                    {
                        return adminBlock;
                    }
                }

                // تولید و ارسال OTP
                var otpCode = await _smsService.GenerateOtpAsync();
                var cacheKey = $"LoginOtp_{loginDto.PhoneNumber}";
                SetCacheData(cacheKey, otpCode, OtpExpirationMinutes);
                
                // Rate Limiting
                SetRateLimit(loginDto.PhoneNumber, OtpRateLimitMinutes);
                
                // Reset attempt counter
                var attemptKey = $"OtpAttempt_{loginDto.PhoneNumber}_login";
                _cache.Remove(attemptKey);

                var (sent, failureResponse) = await SendOtpOrFailAsync(
                    loginDto.PhoneNumber,
                    otpCode,
                    "VerifyOtp",
                    cacheKey,
                    "Login",
                    ipAddress);
                if (!sent)
                {
                    return failureResponse!;
                }
                
                _logger.LogInformation("Login OTP generated for {PhoneNumber} from IP {IpAddress}", 
                    loginDto.PhoneNumber, ipAddress);

                LogDevOtpForDevelopment(loginDto.PhoneNumber, otpCode, "Login");

                return CreateSuccessOtpResponse(
                    "کد تایید ارسال شد",
                    otpCode,
                    OtpExpirationMinutes * 60);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LoginAsync for phone {PhoneNumber}", loginDto.PhoneNumber);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<AuthResponseDto> VerifyLoginOtpAsync(VerifyOtpDto verifyOtpDto, string? ipAddress = null, bool requireAdminPanelAccess = false)
        {
            try
            {
                // بررسی محدودیت تلاش
                var attemptKey = $"OtpAttempt_{verifyOtpDto.PhoneNumber}_login";
                var attemptData = _cache.Get<OtpAttemptCacheDto>(attemptKey);
                
                if (attemptData?.LockedUntil != null && attemptData.LockedUntil > DateTime.UtcNow)
                {
                    var remainingMinutes = (int)(attemptData.LockedUntil.Value - DateTime.UtcNow).TotalMinutes;
                    _logger.LogWarning("OTP attempts locked for login {PhoneNumber} from IP {IpAddress}", 
                        verifyOtpDto.PhoneNumber, ipAddress);
                    return new AuthResponseDto
                    {
                        StatusCode = 423, // Locked
                        Success = false,
                        Message = $"حساب شما به دلیل تلاش‌های ناموفق به مدت {remainingMinutes} دقیقه قفل شده است"
                    };
                }

                var cacheKey = $"LoginOtp_{verifyOtpDto.PhoneNumber}";
                
                // بررسی وجود OTP در cache
                if (!_cache.TryGetValue(cacheKey, out var cachedOtp) || cachedOtp == null)
                {
                    // اگر OTP در cache وجود نداشت، ممکن است شماره تلفن اشتباه باشد یا OTP منقضی شده باشد
                    // برای امنیت، پیام کلی می‌دهیم
                    return new AuthResponseDto
                    {
                        StatusCode = 400, // Bad Request
                        Success = false,
                        Message = ControlledErrorHelper.OtpExpired
                    };
                }

                // بررسی صحت کد تایید
                var cachedOtpCode = cachedOtp?.ToString()?.Trim() ?? string.Empty;
                var userOtpCode = verifyOtpDto.OtpCode?.Trim() ?? string.Empty;
                
                // DEV ONLY — TODO(production): قبل از release برای کاربران نهایی این لاگ را حذف کنید (جستجو: DEV-OTP-VERIFY)
                _logger.LogInformation("[DEV-OTP-VERIFY] Cached OTP: {CachedOtp}, User Input: {UserOtp}, Phone: {PhoneNumber}",
                    cachedOtpCode, userOtpCode, verifyOtpDto.PhoneNumber);
                
                if (cachedOtpCode != userOtpCode)
                {
                    // افزایش تعداد تلاش‌های ناموفق
                    if (attemptData == null)
                    {
                        attemptData = new OtpAttemptCacheDto
                        {
                            AttemptCount = 1,
                            FirstAttemptTime = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        attemptData.AttemptCount++;
                    }

                    if (attemptData.AttemptCount >= MaxOtpAttempts)
                    {
                        attemptData.LockedUntil = DateTime.UtcNow.AddMinutes(OtpLockoutMinutes);
                        _logger.LogWarning("OTP attempts exceeded for login {PhoneNumber} from IP {IpAddress}. Account locked.", 
                            verifyOtpDto.PhoneNumber, ipAddress);
                    }

                    SetCacheData(attemptKey, attemptData, OtpLockoutMinutes + 5);

                    return new AuthResponseDto
                    {
                        StatusCode = 400, // Bad Request
                        Success = false,
                        Message = ControlledErrorHelper.OtpIncorrect
                    };
                }

                // بررسی وجود کاربر (بعد از تأیید OTP)
                var (user, blockedResponse) = await ResolveLoginUserForVerifyAsync(verifyOtpDto.PhoneNumber);
                if (blockedResponse != null)
                {
                    return blockedResponse;
                }

                if (user == null)
                {
                    return new AuthResponseDto
                    {
                        StatusCode = 400,
                        Success = false,
                        Message = "کد تایید یا شماره تلفن صحیح نیست"
                    };
                }

                if (requireAdminPanelAccess && !await UserHasAdminRoleAsync(user.Id))
                {
                    _cache.Remove(cacheKey);
                    _cache.Remove(attemptKey);
                    _logger.LogWarning("Admin panel login blocked for non-admin user {UserId}", user.Id);
                    return CreateAdminPanelAccessDeniedAuthResponse();
                }

                // به‌روزرسانی آخرین ورود
                await _userRepository.UpdateLastLoginAsync(user.Id);

                // حذف OTP و attempt counter از کش
                _cache.Remove(cacheKey);
                _cache.Remove(attemptKey);

                // تولید توکن‌ها
                var accessToken = await GenerateAccessTokenWithRolesAsync(user);
                var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id);

                _logger.LogInformation("User logged in successfully: {PhoneNumber} from IP {IpAddress}", 
                    verifyOtpDto.PhoneNumber, ipAddress);

                return new AuthResponseDto
                {
                    StatusCode = 200, // OK
                    Success = true,
                    Message = "ورود با موفقیت انجام شد",
                    Tokens = new TokenResponseDto
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken.Token,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtService.GetAccessTokenExpirationMinutes()),
                        RefreshTokenExpiresAt = refreshToken.ExpiresAt
                    },
                        User = new UserInfoDto
                        {
                            Id = user.Id,
                            FullName = user.FullName ?? string.Empty,
                            PhoneNumber = user.PhoneNumber,
                            IsPhoneVerified = user.IsPhoneVerified
                        }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VerifyLoginOtpAsync for phone {PhoneNumber}", verifyOtpDto.PhoneNumber);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<SendOtpResponseDto> ResendLoginOtpAsync(LoginDto loginDto, string? ipAddress = null, bool requireAdminPanelAccess = false)
        {
            try
            {
                // Rate Limiting
                var (isRateLimited, retryAfterSeconds) = CheckRateLimit(loginDto.PhoneNumber);
                if (isRateLimited)
                {
                    _logger.LogWarning("Rate limit exceeded for resend login OTP {PhoneNumber} from IP {IpAddress}", 
                        loginDto.PhoneNumber, ipAddress);
                    return new SendOtpResponseDto
                    {
                        StatusCode = 429, // Too Many Requests
                        Success = false,
                        Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                        ExpiresInSeconds = 0,
                        RetryAfterSeconds = retryAfterSeconds
                    };
                }

                var (user, blockedResponse) = await ResolveLoginUserForOtpAsync(loginDto.PhoneNumber);
                if (blockedResponse != null)
                {
                    return blockedResponse;
                }

                if (requireAdminPanelAccess && user != null)
                {
                    var adminBlock = await BlockAdminPanelAccessForOtpAsync(user);
                    if (adminBlock != null)
                    {
                        return adminBlock;
                    }
                }

                // تولید و ارسال OTP جدید
                var otpCode = await _smsService.GenerateOtpAsync();
                var cacheKey = $"LoginOtp_{loginDto.PhoneNumber}";
                SetCacheData(cacheKey, otpCode, OtpExpirationMinutes);
                
                // Rate Limiting
                SetRateLimit(loginDto.PhoneNumber, OtpRateLimitMinutes);
                
                // Reset attempt counter
                var attemptKey = $"OtpAttempt_{loginDto.PhoneNumber}_login";
                _cache.Remove(attemptKey);

                var (sent, failureResponse) = await SendOtpOrFailAsync(
                    loginDto.PhoneNumber,
                    otpCode,
                    "VerifyOtp",
                    cacheKey,
                    "Login resend",
                    ipAddress);
                if (!sent)
                {
                    return failureResponse!;
                }
                
                _logger.LogInformation("Login OTP resent for {PhoneNumber} from IP {IpAddress}", 
                    loginDto.PhoneNumber, ipAddress);

                LogDevOtpForDevelopment(loginDto.PhoneNumber, otpCode, "Login");

                return CreateSuccessOtpResponse(
                    "کد تایید مجدداً ارسال شد",
                    otpCode,
                    OtpExpirationMinutes * 60);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResendLoginOtpAsync for phone {PhoneNumber}", loginDto.PhoneNumber);
                throw;
            }
        }

        public async Task<SendOtpResponseDto> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto, string? ipAddress = null)
        {
            try
            {
                // Rate Limiting
                var (isRateLimited, retryAfterSeconds) = CheckRateLimit(forgotPasswordDto.PhoneNumber);
                if (isRateLimited)
                {
                    _logger.LogWarning("Rate limit exceeded for forgot password {PhoneNumber} from IP {IpAddress}", 
                        forgotPasswordDto.PhoneNumber, ipAddress);
                    return new SendOtpResponseDto
                    {
                        StatusCode = 429, // Too Many Requests
                        Success = false,
                        Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                        ExpiresInSeconds = 0,
                        RetryAfterSeconds = retryAfterSeconds
                    };
                }

                var user = await _userRepository.GetByPhoneNumberAsync(forgotPasswordDto.PhoneNumber);

                if (user == null)
                {
                    return new SendOtpResponseDto
                    {
                        StatusCode = 404, // Not Found
                        Success = false,
                        Message = "کاربری با این شماره تلفن یافت نشد",
                        ExpiresInSeconds = 0
                    };
                }

                // تولید و ارسال OTP
                var otpCode = await _smsService.GenerateOtpAsync();
                var cacheKey = $"ForgotPasswordOtp_{forgotPasswordDto.PhoneNumber}";
                SetCacheData(cacheKey, otpCode, OtpExpirationMinutes);
                
                // Rate Limiting
                SetRateLimit(forgotPasswordDto.PhoneNumber, OtpRateLimitMinutes);
                
                // Reset attempt counter
                var attemptKey = $"OtpAttempt_{forgotPasswordDto.PhoneNumber}_forgot";
                _cache.Remove(attemptKey);

                var (sent, failureResponse) = await SendOtpOrFailAsync(
                    forgotPasswordDto.PhoneNumber,
                    otpCode,
                    "ResetPassword",
                    cacheKey,
                    "ResetPassword",
                    ipAddress);
                if (!sent)
                {
                    return failureResponse!;
                }
                
                _logger.LogInformation("Forgot password OTP generated for {PhoneNumber} from IP {IpAddress}",
                    forgotPasswordDto.PhoneNumber, ipAddress);

                LogDevOtpForDevelopment(forgotPasswordDto.PhoneNumber, otpCode, "ResetPassword");

                return CreateSuccessOtpResponse(
                    "کد تایید ارسال شد",
                    otpCode,
                    OtpExpirationMinutes * 60);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForgotPasswordAsync for phone {PhoneNumber}", forgotPasswordDto.PhoneNumber);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<SendOtpResponseDto> ResendForgotPasswordOtpAsync(LoginDto loginDto, string? ipAddress = null)
        {
            try
            {
                // Rate Limiting
                var (isRateLimited, retryAfterSeconds) = CheckRateLimit(loginDto.PhoneNumber);
                if (isRateLimited)
                {
                    _logger.LogWarning("Rate limit exceeded for resend forgot password OTP {PhoneNumber} from IP {IpAddress}", 
                        loginDto.PhoneNumber, ipAddress);
                    return new SendOtpResponseDto
                    {
                        StatusCode = 429, // Too Many Requests
                        Success = false,
                        Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                        ExpiresInSeconds = 0,
                        RetryAfterSeconds = retryAfterSeconds
                    };
                }

                var user = await _userRepository.GetByPhoneNumberAsync(loginDto.PhoneNumber);

                if (user == null)
                {
                    return new SendOtpResponseDto
                    {
                        StatusCode = 404, // Not Found
                        Success = false,
                        Message = "کاربری با این شماره تلفن یافت نشد",
                        ExpiresInSeconds = 0
                    };
                }

                // تولید و ارسال OTP جدید
                var otpCode = await _smsService.GenerateOtpAsync();
                var cacheKey = $"ForgotPasswordOtp_{loginDto.PhoneNumber}";
                SetCacheData(cacheKey, otpCode, OtpExpirationMinutes);
                
                // Rate Limiting
                SetRateLimit(loginDto.PhoneNumber, OtpRateLimitMinutes);
                
                // Reset attempt counter
                var attemptKey = $"OtpAttempt_{loginDto.PhoneNumber}_forgot";
                _cache.Remove(attemptKey);

                var (sent, failureResponse) = await SendOtpOrFailAsync(
                    loginDto.PhoneNumber,
                    otpCode,
                    "ResetPassword",
                    cacheKey,
                    "ResetPassword resend",
                    ipAddress);
                if (!sent)
                {
                    return failureResponse!;
                }
                
                _logger.LogInformation("Forgot password OTP resent for {PhoneNumber} from IP {IpAddress}",
                    loginDto.PhoneNumber, ipAddress);

                LogDevOtpForDevelopment(loginDto.PhoneNumber, otpCode, "ResetPassword");

                return CreateSuccessOtpResponse(
                    "کد تایید مجدداً ارسال شد",
                    otpCode,
                    OtpExpirationMinutes * 60);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResendForgotPasswordOtpAsync for phone {PhoneNumber}", loginDto.PhoneNumber);
                throw;
            }
        }

        public async Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto, string? ipAddress = null)
        {
            try
            {
                // بررسی محدودیت تلاش
                var attemptKey = $"OtpAttempt_{resetPasswordDto.PhoneNumber}_forgot";
                var attemptData = _cache.Get<OtpAttemptCacheDto>(attemptKey);
                
                if (attemptData?.LockedUntil != null && attemptData.LockedUntil > DateTime.UtcNow)
                {
                    var remainingMinutes = (int)(attemptData.LockedUntil.Value - DateTime.UtcNow).TotalMinutes;
                    _logger.LogWarning("OTP attempts locked for reset password {PhoneNumber} from IP {IpAddress}", 
                        resetPasswordDto.PhoneNumber, ipAddress);
                    return new AuthResponseDto
                    {
                        StatusCode = 423, // Locked
                        Success = false,
                        Message = $"حساب شما به دلیل تلاش‌های ناموفق به مدت {remainingMinutes} دقیقه قفل شده است"
                    };
                }

                var cacheKey = $"ForgotPasswordOtp_{resetPasswordDto.PhoneNumber}";
                
                // بررسی وجود OTP در cache
                if (!_cache.TryGetValue(cacheKey, out var cachedOtp) || cachedOtp == null)
                {
                    // اگر OTP در cache وجود نداشت، ممکن است شماره تلفن اشتباه باشد یا OTP منقضی شده باشد
                    // برای امنیت، پیام کلی می‌دهیم
                    return new AuthResponseDto
                    {
                        StatusCode = 400, // Bad Request
                        Success = false,
                        Message = ControlledErrorHelper.OtpExpired
                    };
                }

                // بررسی صحت کد تایید
                if (cachedOtp.ToString() != resetPasswordDto.OtpCode)
                {
                    // افزایش تعداد تلاش‌های ناموفق
                    if (attemptData == null)
                    {
                        attemptData = new OtpAttemptCacheDto
                        {
                            AttemptCount = 1,
                            FirstAttemptTime = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        attemptData.AttemptCount++;
                    }

                    if (attemptData.AttemptCount >= MaxOtpAttempts)
                    {
                        attemptData.LockedUntil = DateTime.UtcNow.AddMinutes(OtpLockoutMinutes);
                        _logger.LogWarning("OTP attempts exceeded for reset password {PhoneNumber} from IP {IpAddress}. Account locked.", 
                            resetPasswordDto.PhoneNumber, ipAddress);
                    }

                    SetCacheData(attemptKey, attemptData, OtpLockoutMinutes + 5);

                    return new AuthResponseDto
                    {
                        StatusCode = 400, // Bad Request
                        Success = false,
                        Message = ControlledErrorHelper.OtpIncorrect
                    };
                }

                // بررسی وجود کاربر (بعد از تأیید OTP)
                var user = await _userRepository.GetByPhoneNumberAsync(resetPasswordDto.PhoneNumber);

                if (user == null)
                {
                    // اگر OTP درست بود ولی کاربر وجود نداشت، ممکن است کاربر حذف شده باشد
                    // برای امنیت، پیام کلی می‌دهیم
                    return new AuthResponseDto
                    {
                        StatusCode = 400, // Bad Request
                        Success = false,
                        Message = "کد تایید یا شماره تلفن صحیح نیست"
                    };
                }

                // تغییر رمز عبور
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                // حذف OTP و attempt counter از کش
                _cache.Remove(cacheKey);
                _cache.Remove(attemptKey);

                // تولید توکن‌ها
                var accessToken = await GenerateAccessTokenWithRolesAsync(user);
                var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id);

                _logger.LogInformation("Password reset successfully for {PhoneNumber} from IP {IpAddress}", 
                    resetPasswordDto.PhoneNumber, ipAddress);

                return new AuthResponseDto
                {
                    StatusCode = 200, // OK
                    Success = true,
                    Message = "رمز عبور با موفقیت تغییر کرد",
                    Tokens = new TokenResponseDto
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken.Token,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtService.GetAccessTokenExpirationMinutes()),
                        RefreshTokenExpiresAt = refreshToken.ExpiresAt
                    },
                        User = new UserInfoDto
                        {
                            Id = user.Id,
                            FullName = user.FullName ?? string.Empty,
                            PhoneNumber = user.PhoneNumber,
                            IsPhoneVerified = user.IsPhoneVerified
                        }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPasswordAsync for phone {PhoneNumber}", resetPasswordDto.PhoneNumber);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto, string? ipAddress = null)
        {
            try
            {
                var isValid = await _refreshTokenService.IsRefreshTokenValidAsync(refreshTokenDto.RefreshToken);
                
                if (!isValid)
                {
                    _logger.LogWarning("Invalid refresh token attempt from IP {IpAddress}", ipAddress);
                    return new AuthResponseDto
                    {
                        StatusCode = 401, // Unauthorized
                        Success = false,
                        Message = "Refresh Token نامعتبر یا منقضی شده است"
                    };
                }

                var refreshToken = await _refreshTokenService.GetRefreshTokenAsync(refreshTokenDto.RefreshToken);
                
                if (refreshToken == null || refreshToken.User == null)
                {
                    return new AuthResponseDto
                    {
                        StatusCode = 404, // Not Found
                        Success = false,
                        Message = "Refresh Token یافت نشد"
                    };
                }

                var user = refreshToken.User;

                // بررسی وضعیت کاربر
                if (!user.IsActive || user.IsDeleted)
                {
                    _logger.LogWarning("Refresh token attempt for inactive/deleted user {UserId} from IP {IpAddress}", 
                        user.Id, ipAddress);
                    return new AuthResponseDto
                    {
                        StatusCode = 403, // Forbidden
                        Success = false,
                        Message = ControlledErrorHelper.InactiveUserAccount
                    };
                }

                // ذخیره Expiration Time اولیه قبل از لغو
                var originalExpiresAt = refreshToken.ExpiresAt;

                // لغو توکن قدیمی
                await _refreshTokenService.RevokeRefreshTokenAsync(refreshTokenDto.RefreshToken);

                // تولید توکن‌های جدید
                // Refresh Token جدید با همان Expiration Time اولیه ایجاد می‌شود (برای 24 ساعت لاگین بودن)
                var accessToken = await GenerateAccessTokenWithRolesAsync(user);
                var newRefreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id, originalExpiresAt);

                _logger.LogInformation("Token refreshed successfully for user {UserId} from IP {IpAddress}", 
                    user.Id, ipAddress);

                return new AuthResponseDto
                {
                    StatusCode = 200, // OK
                    Success = true,
                    Message = "توکن‌ها با موفقیت به‌روزرسانی شدند",
                    Tokens = new TokenResponseDto
                    {
                        AccessToken = accessToken,
                        RefreshToken = newRefreshToken.Token,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtService.GetAccessTokenExpirationMinutes()),
                        RefreshTokenExpiresAt = newRefreshToken.ExpiresAt
                    },
                        User = new UserInfoDto
                        {
                            Id = user.Id,
                            FullName = user.FullName ?? string.Empty,
                            PhoneNumber = user.PhoneNumber,
                            IsPhoneVerified = user.IsPhoneVerified
                        }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RefreshTokenAsync");
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<LogoutResponseDto> LogoutAsync(int userId, string? jti, string? ipAddress = null)
        {
            try
            {
                // بررسی وجود کاربر
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("Logout attempt for non-existent or deleted user {UserId} from IP {IpAddress}", 
                        userId, ipAddress);
                    return new LogoutResponseDto
                    {
                        StatusCode = 404, // Not Found
                        Success = false,
                        Message = "کاربر یافت نشد"
                    };
                }

                // لغو تمام Refresh Token های کاربر
                await _refreshTokenService.RevokeAllUserTokensAsync(userId);

                // اضافه کردن JTI (JWT ID) به blacklist برای غیرفعال کردن Access Token
                if (!string.IsNullOrWhiteSpace(jti))
                {
                    var accessTokenExpirationMinutes = _jwtService.GetAccessTokenExpirationMinutes();
                    await _tokenBlacklistService.AddToBlacklistAsync(jti, accessTokenExpirationMinutes);
                    _logger.LogInformation("JTI {Jti} added to blacklist for user {UserId}", jti, userId);
                }
                else
                {
                    _logger.LogWarning("JTI not found in token for user {UserId} during logout", userId);
                }

                _logger.LogInformation("User {UserId} ({PhoneNumber}) logged out successfully from IP {IpAddress}", 
                    userId, user.PhoneNumber, ipAddress);

                return new LogoutResponseDto
                {
                    StatusCode = 200, // OK
                    Success = true,
                    Message = "خروج از سیستم با موفقیت انجام شد"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LogoutAsync for user {UserId}", userId);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }
    }
}
