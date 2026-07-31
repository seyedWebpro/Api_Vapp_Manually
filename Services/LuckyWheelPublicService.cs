using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.DTOs.Public;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Api_Vapp.Services
{
    public class LuckyWheelPublicService : ILuckyWheelPublicService
    {
        private readonly ILuckyWheelRepository _luckyWheelRepository;
        private readonly Api_Context _context;
        private readonly PublicPhonebookService _phonebookService;
        private readonly IPublicParticipantSessionService _sessionService;
        private readonly IPublicParticipantOtpService _otpService;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<LuckyWheelPublicService> _logger;

        public LuckyWheelPublicService(
            ILuckyWheelRepository luckyWheelRepository,
            Api_Context context,
            PublicPhonebookService phonebookService,
            IPublicParticipantSessionService sessionService,
            IPublicParticipantOtpService otpService,
            IHostEnvironment environment,
            ILogger<LuckyWheelPublicService> logger)
        {
            _luckyWheelRepository = luckyWheelRepository;
            _context = context;
            _phonebookService = phonebookService;
            _sessionService = sessionService;
            _otpService = otpService;
            _environment = environment;
            _logger = logger;
        }

        public async Task<ApiResponse<LuckyWheelPublicDto>> GetPublicWheelAsync(string slug)
        {
            try
            {
                var normalizedSlug = NormalizeSlug(slug);
                if (normalizedSlug == null)
                {
                    return ApiResponse<LuckyWheelPublicDto>.BadRequest(
                        "لینک نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var wheel = await _luckyWheelRepository.GetBySlugReadOnlyAsync(normalizedSlug);
                var availabilityError = EnsurePubliclyAvailable<LuckyWheelPublicDto>(wheel);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                return ApiResponse<LuckyWheelPublicDto>.CreateSuccess(MapToPublicDto(wheel!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public lucky wheel for slug {Slug}", slug);
                return ApiResponse<LuckyWheelPublicDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<RegisterPublicParticipantResponseDto>> RegisterAsync(
            string slug,
            RegisterPublicParticipantDto dto)
        {
            try
            {
                var normalizedSlug = NormalizeSlug(slug);
                if (normalizedSlug == null)
                {
                    return ApiResponse<RegisterPublicParticipantResponseDto>.BadRequest(
                        "لینک نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var identity = ValidateIdentity(dto);
                if (identity.Error != null)
                {
                    return identity.Error;
                }

                var wheel = await _luckyWheelRepository.GetBySlugReadOnlyAsync(normalizedSlug);
                var availabilityError = EnsurePubliclyAvailable<RegisterPublicParticipantResponseDto>(wheel);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                if (AllowTestReplay(normalizedSlug))
                {
                    await ResetParticipationForTestAsync(wheel!.Id, identity.Mobile);
                }
                else if (await _luckyWheelRepository.HasParticipantWithMobileAsync(wheel!.Id, identity.Mobile))
                {
                    return ApiResponse<RegisterPublicParticipantResponseDto>.BadRequest(
                        "این شماره قبلاً در این گردونه شرکت کرده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var sessionResult = await _sessionService.CreateOrRefreshAsync(
                    PublicParticipantResourceType.LuckyWheel,
                    wheel.Id,
                    identity.FullName,
                    identity.Mobile);

                if (!sessionResult.Success || sessionResult.Data == null)
                {
                    return ApiResponse<RegisterPublicParticipantResponseDto>.Error(
                        sessionResult.Message,
                        sessionResult.StatusCode,
                        sessionResult.Errors,
                        sessionResult.ErrorCode);
                }

                var tokenResult = sessionResult.Data;
                var otpResult = await _otpService.SendAsync(tokenResult.Session, "public-wheel-register");
                if (!otpResult.Success)
                {
                    _logger.LogWarning(
                        "Public wheel register OTP send failed for slug {Slug}, mobile {Mobile}, session {SessionId}: {Message}",
                        normalizedSlug,
                        identity.Mobile,
                        tokenResult.Session.Id,
                        otpResult.Message);

                    return ApiResponse<RegisterPublicParticipantResponseDto>.Error(
                        otpResult.Message,
                        otpResult.StatusCode,
                        otpResult.Errors,
                        otpResult.ErrorCode);
                }

                _logger.LogInformation(
                    "Public wheel participant registered — slug {Slug}, session {SessionId}, mobile {Mobile}, name {FullName}",
                    normalizedSlug,
                    tokenResult.Session.Id,
                    identity.Mobile,
                    identity.FullName);

                return ApiResponse<RegisterPublicParticipantResponseDto>.CreateSuccess(
                    new RegisterPublicParticipantResponseDto
                    {
                        AccessToken = tokenResult.AccessToken,
                        ExpiresAt = tokenResult.Session.ExpiresAt,
                        ParticipantFullName = tokenResult.Session.ParticipantFullName,
                        ParticipantMobile = tokenResult.Session.ParticipantMobile,
                        IsPhoneVerified = false,
                        OtpExpiresInSeconds = otpResult.Data?.ExpiresInSeconds,
                        RetryAfterSeconds = otpResult.Data?.RetryAfterSeconds,
                        OtpCode = otpResult.Data?.OtpCode
                    },
                    "کد تایید به شماره موبایل ارسال شد",
                    sessionResult.StatusCode == 201 ? 201 : 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering public wheel participant for slug {Slug}", slug);
                return ApiResponse<RegisterPublicParticipantResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PublicParticipantOtpResponseDto>> VerifyOtpAsync(
            string slug,
            VerifyPublicParticipantOtpDto dto)
        {
            try
            {
                var (wheel, availabilityError) = await ResolvePubliclyAvailableWheelAsync<PublicParticipantOtpResponseDto>(slug);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                var sessionResult = await _sessionService.ValidateActiveAsync(
                    dto.AccessToken,
                    PublicParticipantResourceType.LuckyWheel,
                    wheel!.Id);

                if (!sessionResult.Success || sessionResult.Data == null)
                {
                    return ApiResponse<PublicParticipantOtpResponseDto>.Error(
                        sessionResult.Message,
                        sessionResult.StatusCode,
                        sessionResult.Errors,
                        sessionResult.ErrorCode);
                }

                var verifyResult = await _otpService.VerifyAsync(sessionResult.Data, dto.OtpCode);
                if (!verifyResult.Success)
                {
                    _logger.LogWarning(
                        "Public wheel OTP verify failed — slug {Slug}, session {SessionId}, mobile {Mobile}, errorCode {ErrorCode}",
                        slug,
                        sessionResult.Data.Id,
                        sessionResult.Data.ParticipantMobile,
                        verifyResult.ErrorCode);

                    return verifyResult;
                }

                if (!sessionResult.Data.PhoneVerifiedAt.HasValue)
                {
                    await _sessionService.MarkPhoneVerifiedAsync(sessionResult.Data);
                }

                _logger.LogInformation(
                    "Public wheel phone verified — slug {Slug}, session {SessionId}, mobile {Mobile}, name {FullName}",
                    slug,
                    sessionResult.Data.Id,
                    sessionResult.Data.ParticipantMobile,
                    sessionResult.Data.ParticipantFullName);

                return ApiResponse<PublicParticipantOtpResponseDto>.CreateSuccess(
                    new PublicParticipantOtpResponseDto
                    {
                        IsPhoneVerified = true,
                        ExpiresInSeconds = 0,
                        SessionExpiresAt = sessionResult.Data.ExpiresAt
                    },
                    "شماره موبایل با موفقیت تأیید شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying public wheel OTP for slug {Slug}", slug);
                return ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PublicParticipantOtpResponseDto>> ResendOtpAsync(
            string slug,
            ResendPublicParticipantOtpDto dto)
        {
            try
            {
                var (wheel, availabilityError) = await ResolvePubliclyAvailableWheelAsync<PublicParticipantOtpResponseDto>(slug);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                var sessionResult = await _sessionService.ValidateActiveAsync(
                    dto.AccessToken,
                    PublicParticipantResourceType.LuckyWheel,
                    wheel!.Id);

                if (!sessionResult.Success || sessionResult.Data == null)
                {
                    return ApiResponse<PublicParticipantOtpResponseDto>.Error(
                        sessionResult.Message,
                        sessionResult.StatusCode,
                        sessionResult.Errors,
                        sessionResult.ErrorCode);
                }

                var resendResult = await _otpService.ResendAsync(sessionResult.Data, "public-wheel-resend");
                if (resendResult.Success)
                {
                    _logger.LogInformation(
                        "Public wheel OTP resent — slug {Slug}, session {SessionId}, mobile {Mobile}",
                        slug,
                        sessionResult.Data.Id,
                        sessionResult.Data.ParticipantMobile);
                }
                else
                {
                    _logger.LogWarning(
                        "Public wheel OTP resend failed — slug {Slug}, session {SessionId}, mobile {Mobile}, errorCode {ErrorCode}",
                        slug,
                        sessionResult.Data.Id,
                        sessionResult.Data.ParticipantMobile,
                        resendResult.ErrorCode);
                }

                return resendResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending public wheel OTP for slug {Slug}", slug);
                return ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SpinLuckyWheelPublicResponseDto>> SpinAsync(string slug, SpinLuckyWheelPublicDto dto)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (normalizedSlug == null)
            {
                return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                    "لینک نامعتبر است",
                    errorCode: ErrorCodes.InvalidInput);
            }

            try
            {
                var wheel = await _context.LuckyWheels
                    .AsSplitQuery()
                    .Include(w => w.Items.OrderBy(item => item.DisplayOrder))
                    .Include(w => w.Notebooks)
                    .FirstOrDefaultAsync(w =>
                        w.Slug == normalizedSlug &&
                        !w.IsDeleted &&
                        w.Status == LuckyWheelStatus.Published);

                var availabilityError = EnsurePubliclyAvailable<SpinLuckyWheelPublicResponseDto>(wheel);
                if (availabilityError != null || wheel is null)
                {
                    return availabilityError ?? ApiResponse<SpinLuckyWheelPublicResponseDto>.NotFound(WheelNotFoundMessage);
                }

                if (wheel.Items.Count == 0)
                {
                    return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                        "گردونه آماده چرخش نیست",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var sessionResult = await _sessionService.ValidateActiveAsync(
                    dto.AccessToken,
                    PublicParticipantResourceType.LuckyWheel,
                    wheel.Id,
                    requirePhoneVerified: true);

                if (!sessionResult.Success || sessionResult.Data == null)
                {
                    return ApiResponse<SpinLuckyWheelPublicResponseDto>.Error(
                        sessionResult.Message,
                        sessionResult.StatusCode,
                        sessionResult.Errors,
                        sessionResult.ErrorCode);
                }

                var session = sessionResult.Data;

                if (await _luckyWheelRepository.HasParticipantWithMobileAsync(wheel.Id, session.ParticipantMobile))
                {
                    return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                        "این شماره قبلاً در این گردونه شرکت کرده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

                var wonItem = LuckyWheelSpinHelper.PickWeightedItem(wheel.Items.ToList());
                var now = DateTime.UtcNow;
                var prizeCode = await GenerateUniquePrizeCodeAsync(wheel.Id);

                var participant = new LuckyWheelParticipant
                {
                    LuckyWheelId = wheel.Id,
                    ParticipantFullName = session.ParticipantFullName,
                    ParticipantMobile = session.ParticipantMobile,
                    WonLuckyWheelItemId = wonItem.Id,
                    PrizeCode = prizeCode,
                    CreatedAt = now
                };

                await _context.LuckyWheelParticipants.AddAsync(participant);
                await _context.SaveChangesAsync();

                if (wheel.SaveToPhonebook && wheel.Notebooks.Count > 0)
                {
                    var notebookIds = wheel.Notebooks.Select(n => n.ContactNotebookId).ToList();
                    var contactId = await _phonebookService.SaveParticipantAsync(
                        notebookIds,
                        session.ParticipantMobile,
                        session.ParticipantFullName);

                    if (contactId.HasValue)
                    {
                        participant.ContactId = contactId;
                        await _context.SaveChangesAsync();
                    }
                }

                var consumed = await _sessionService.TryMarkConsumedAsync(session.Id);
                if (!consumed)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                        "این شماره قبلاً در این گردونه شرکت کرده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Public wheel spin completed — participant {ParticipantId}, wheel {WheelId}, slug {Slug}, session {SessionId}, mobile {Mobile}, name {FullName}, wonItem {ItemId} {ItemName}, prizeCode {PrizeCode}",
                    participant.Id,
                    wheel.Id,
                    normalizedSlug,
                    session.Id,
                    session.ParticipantMobile,
                    session.ParticipantFullName,
                    wonItem.Id,
                    wonItem.Name,
                    prizeCode);

                return ApiResponse<SpinLuckyWheelPublicResponseDto>.CreateSuccess(
                    new SpinLuckyWheelPublicResponseDto
                    {
                        ParticipantId = participant.Id,
                        WonItemId = wonItem.Id,
                        WonItemName = wonItem.Name,
                        PrizeCode = prizeCode
                    },
                    "چرخش با موفقیت ثبت شد",
                    201);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Duplicate or database error spinning public lucky wheel for slug {Slug}", slug);
                return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                    "این شماره قبلاً در این گردونه شرکت کرده است",
                    errorCode: ErrorCodes.ValidationFailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error spinning public lucky wheel for slug {Slug}", slug);
                return ApiResponse<SpinLuckyWheelPublicResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        /// <summary>
        /// فقط در Development: اجازه تست مجدد با همان شماره (مثلاً test-wheel)
        /// </summary>
        private bool AllowTestReplay(string _) => _environment.IsDevelopment();

        private async Task ResetParticipationForTestAsync(int wheelId, string mobile)
        {
            var participants = await _context.LuckyWheelParticipants
                .Where(p => p.LuckyWheelId == wheelId && p.ParticipantMobile == mobile)
                .ToListAsync();

            if (participants.Count > 0)
            {
                _context.LuckyWheelParticipants.RemoveRange(participants);
            }

            var now = DateTime.UtcNow;
            var sessions = await _context.PublicParticipantSessions
                .Where(s =>
                    !s.IsDeleted &&
                    s.ResourceType == PublicParticipantResourceType.LuckyWheel &&
                    s.ResourceId == wheelId &&
                    s.ParticipantMobile == mobile)
                .ToListAsync();

            foreach (var session in sessions)
            {
                session.IsDeleted = true;
                session.UpdatedAt = now;
            }

            if (participants.Count > 0 || sessions.Count > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Test replay reset — wheel {WheelId}, mobile {Mobile}, removedParticipants {ParticipantCount}, softDeletedSessions {SessionCount}",
                    wheelId,
                    mobile,
                    participants.Count,
                    sessions.Count);
            }
        }

        private const string WheelNotFoundMessage = "گردونه یافت نشد";
        private const string WheelInactiveMessage = "این گردونه در حال حاضر فعال نیست و امکان شرکت در آن وجود ندارد";

        private static ApiResponse<T>? EnsurePubliclyAvailable<T>(LuckyWheel? wheel)
        {
            if (wheel == null)
            {
                return ApiResponse<T>.NotFound(WheelNotFoundMessage);
            }

            if (!wheel.IsActive)
            {
                return ApiResponse<T>.Forbidden(WheelInactiveMessage, ErrorCodes.ResourceInactive);
            }

            return null;
        }

        private async Task<(LuckyWheel? Wheel, ApiResponse<T>? Error)> ResolvePubliclyAvailableWheelAsync<T>(string slug)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (normalizedSlug == null)
            {
                return (null, ApiResponse<T>.BadRequest("لینک نامعتبر است", errorCode: ErrorCodes.InvalidInput));
            }

            var wheel = await _luckyWheelRepository.GetBySlugReadOnlyAsync(normalizedSlug);
            return (wheel, EnsurePubliclyAvailable<T>(wheel));
        }

        private async Task<string> GenerateUniquePrizeCodeAsync(int wheelId)
        {
            for (var attempt = 0; attempt < LuckyWheelConstants.PrizeCodeGenerationMaxAttempts; attempt++)
            {
                var candidate = LuckyWheelSpinHelper.CreatePrizeCodeCandidate();
                if (!await _luckyWheelRepository.PrizeCodeExistsAsync(wheelId, candidate))
                {
                    return candidate;
                }
            }

            return $"LW-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }

        private static (string FullName, string Mobile, ApiResponse<RegisterPublicParticipantResponseDto>? Error) ValidateIdentity(
            RegisterPublicParticipantDto dto)
        {
            var firstName = dto.FirstName?.Trim() ?? string.Empty;
            var lastName = dto.LastName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(firstName))
            {
                return (string.Empty, string.Empty, ApiResponse<RegisterPublicParticipantResponseDto>.BadRequest(
                    "نام الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                return (string.Empty, string.Empty, ApiResponse<RegisterPublicParticipantResponseDto>.BadRequest(
                    "نام خانوادگی الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var mobile = BookingMobileHelper.Normalize(dto.ParticipantMobile);
            if (!BookingMobileHelper.IsValidIranianMobile(mobile))
            {
                return (string.Empty, string.Empty, ApiResponse<RegisterPublicParticipantResponseDto>.BadRequest(
                    "شماره موبایل نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var fullName = $"{firstName} {lastName}".Trim();
            if (fullName.Length > 200)
            {
                return (string.Empty, string.Empty, ApiResponse<RegisterPublicParticipantResponseDto>.BadRequest(
                    "نام نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            return (fullName, mobile, null);
        }

        private static string? NormalizeSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return UserFormSlugHelper.Normalize(slug.Trim());
        }

        private static LuckyWheelPublicDto MapToPublicDto(LuckyWheel wheel) => new()
        {
            Title = wheel.Title,
            Description = wheel.Description,
            Slug = wheel.Slug ?? string.Empty,
            Items = wheel.Items
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new LuckyWheelPublicItemDto
                {
                    Id = i.Id,
                    Name = i.Name
                })
                .ToList()
        };
    }
}
