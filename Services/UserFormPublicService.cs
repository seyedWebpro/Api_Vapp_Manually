using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Public;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    public class UserFormPublicService : IUserFormPublicService
    {
        private static readonly HashSet<string> ContactFieldKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "full_name", "mobile", "phone"
        };

        private readonly IUserFormRepository _userFormRepository;
        private readonly Api_Context _context;
        private readonly PublicPhonebookService _phonebookService;
        private readonly IPublicParticipantSessionService _sessionService;
        private readonly IPublicParticipantOtpService _otpService;
        private readonly ILogger<UserFormPublicService> _logger;

        public UserFormPublicService(
            IUserFormRepository userFormRepository,
            Api_Context context,
            PublicPhonebookService phonebookService,
            IPublicParticipantSessionService sessionService,
            IPublicParticipantOtpService otpService,
            ILogger<UserFormPublicService> logger)
        {
            _userFormRepository = userFormRepository;
            _context = context;
            _phonebookService = phonebookService;
            _sessionService = sessionService;
            _otpService = otpService;
            _logger = logger;
        }

        public async Task<ApiResponse<FormPublicDto>> GetPublicFormAsync(string slug)
        {
            try
            {
                var normalizedSlug = NormalizeSlug(slug);
                if (normalizedSlug == null)
                {
                    return ApiResponse<FormPublicDto>.BadRequest(
                        "لینک نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var form = await _userFormRepository.GetBySlugReadOnlyAsync(normalizedSlug);
                var availabilityError = EnsurePubliclyAvailable<FormPublicDto>(form);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                return ApiResponse<FormPublicDto>.CreateSuccess(MapToPublicDto(form!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public form for slug {Slug}", slug);
                return ApiResponse<FormPublicDto>.InternalServerError(ControlledErrorHelper.Unexpected);
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

                var form = await _userFormRepository.GetBySlugReadOnlyAsync(normalizedSlug);
                var availabilityError = EnsurePubliclyAvailable<RegisterPublicParticipantResponseDto>(form);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                if (await _userFormRepository.HasSubmissionWithMobileAsync(form!.Id, identity.Mobile))
                {
                    return ApiResponse<RegisterPublicParticipantResponseDto>.BadRequest(
                        "این شماره قبلاً این فرم را پر کرده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var sessionResult = await _sessionService.CreateOrRefreshAsync(
                    PublicParticipantResourceType.UserForm,
                    form.Id,
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
                var otpResult = await _otpService.SendAsync(tokenResult.Session, "public-form-register");
                if (!otpResult.Success)
                {
                    _logger.LogWarning(
                        "Public form register OTP send failed for slug {Slug}, mobile {Mobile}, session {SessionId}: {Message}",
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
                    "Public form participant registered — slug {Slug}, session {SessionId}, mobile {Mobile}, name {FullName}",
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
                _logger.LogError(ex, "Error registering public form participant for slug {Slug}", slug);
                return ApiResponse<RegisterPublicParticipantResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PublicParticipantOtpResponseDto>> VerifyOtpAsync(
            string slug,
            VerifyPublicParticipantOtpDto dto)
        {
            try
            {
                var (form, availabilityError) = await ResolvePubliclyAvailableFormAsync<PublicParticipantOtpResponseDto>(slug);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                var sessionResult = await _sessionService.ValidateActiveAsync(
                    dto.AccessToken,
                    PublicParticipantResourceType.UserForm,
                    form!.Id);

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
                        "Public form OTP verify failed — slug {Slug}, session {SessionId}, mobile {Mobile}, errorCode {ErrorCode}",
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
                    "Public form phone verified — slug {Slug}, session {SessionId}, mobile {Mobile}, name {FullName}",
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
                _logger.LogError(ex, "Error verifying public form OTP for slug {Slug}", slug);
                return ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PublicParticipantOtpResponseDto>> ResendOtpAsync(
            string slug,
            ResendPublicParticipantOtpDto dto)
        {
            try
            {
                var (form, availabilityError) = await ResolvePubliclyAvailableFormAsync<PublicParticipantOtpResponseDto>(slug);
                if (availabilityError != null)
                {
                    return availabilityError;
                }

                var sessionResult = await _sessionService.ValidateActiveAsync(
                    dto.AccessToken,
                    PublicParticipantResourceType.UserForm,
                    form!.Id);

                if (!sessionResult.Success || sessionResult.Data == null)
                {
                    return ApiResponse<PublicParticipantOtpResponseDto>.Error(
                        sessionResult.Message,
                        sessionResult.StatusCode,
                        sessionResult.Errors,
                        sessionResult.ErrorCode);
                }

                var resendResult = await _otpService.ResendAsync(sessionResult.Data, "public-form-resend");
                if (resendResult.Success)
                {
                    _logger.LogInformation(
                        "Public form OTP resent — slug {Slug}, session {SessionId}, mobile {Mobile}",
                        slug,
                        sessionResult.Data.Id,
                        sessionResult.Data.ParticipantMobile);
                }
                else
                {
                    _logger.LogWarning(
                        "Public form OTP resend failed — slug {Slug}, session {SessionId}, mobile {Mobile}, errorCode {ErrorCode}",
                        slug,
                        sessionResult.Data.Id,
                        sessionResult.Data.ParticipantMobile,
                        resendResult.ErrorCode);
                }

                return resendResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending public form OTP for slug {Slug}", slug);
                return ApiResponse<PublicParticipantOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SubmitFormPublicResponseDto>> SubmitFormAsync(string slug, SubmitFormPublicDto dto)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (normalizedSlug == null)
            {
                return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                    "لینک نامعتبر است",
                    errorCode: ErrorCodes.InvalidInput);
            }

            try
            {
                var form = await _context.UserForms
                    .AsSplitQuery()
                    .AsNoTracking()
                    .Include(f => f.Fields.Where(field => field.IsActive).OrderBy(field => field.DisplayOrder))
                    .Include(f => f.Notebooks)
                    .FirstOrDefaultAsync(f =>
                        f.Slug == normalizedSlug &&
                        !f.IsDeleted &&
                        f.Status == UserFormStatus.Published);

                var availabilityError = EnsurePubliclyAvailable<SubmitFormPublicResponseDto>(form);
                if (availabilityError != null || form is null)
                {
                    return availabilityError ?? ApiResponse<SubmitFormPublicResponseDto>.NotFound(FormNotFoundMessage);
                }

                var sessionResult = await _sessionService.ValidateActiveAsync(
                    dto.AccessToken,
                    PublicParticipantResourceType.UserForm,
                    form.Id,
                    requirePhoneVerified: true);

                if (!sessionResult.Success || sessionResult.Data == null)
                {
                    return ApiResponse<SubmitFormPublicResponseDto>.Error(
                        sessionResult.Message,
                        sessionResult.StatusCode,
                        sessionResult.Errors,
                        sessionResult.ErrorCode);
                }

                var session = sessionResult.Data;

                if (await _userFormRepository.HasSubmissionWithMobileAsync(form.Id, session.ParticipantMobile))
                {
                    return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                        "این شماره قبلاً این فرم را پر کرده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var values = dto.Values != null
                    ? new Dictionary<string, string?>(dto.Values, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                InjectContactFieldValues(form.Fields, values, session);

                var fieldErrors = UserFormFieldValueValidator.Validate(form.Fields.ToList(), values);
                if (fieldErrors.Count > 0)
                {
                    return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                        "داده‌های فرم نامعتبر است",
                        fieldErrors,
                        ErrorCodes.ValidationFailed);
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

                var now = DateTime.UtcNow;
                var submission = new UserFormSubmission
                {
                    UserFormId = form.Id,
                    ParticipantFullName = session.ParticipantFullName,
                    ParticipantMobile = session.ParticipantMobile,
                    CreatedAt = now,
                    FieldValues = form.Fields
                        .Where(f => values.ContainsKey(f.FieldKey))
                        .Select(f => new UserFormFieldValue
                        {
                            FieldKey = f.FieldKey,
                            Value = values[f.FieldKey]?.Trim()
                        })
                        .ToList()
                };

                await _context.UserFormSubmissions.AddAsync(submission);
                await _context.SaveChangesAsync();

                if (form.SaveToPhonebook && form.Notebooks.Count > 0)
                {
                    var notebookIds = form.Notebooks.Select(n => n.ContactNotebookId).ToList();
                    var contactId = await _phonebookService.SaveParticipantAsync(
                        notebookIds,
                        session.ParticipantMobile,
                        session.ParticipantFullName);

                    if (contactId.HasValue)
                    {
                        submission.ContactId = contactId;
                        await _context.SaveChangesAsync();
                    }
                }

                var consumed = await _sessionService.TryMarkConsumedAsync(session.Id);
                if (!consumed)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                        "این شماره قبلاً این فرم را پر کرده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Public form submission created — submission {SubmissionId}, form {FormId}, slug {Slug}, session {SessionId}, mobile {Mobile}, name {FullName}",
                    submission.Id,
                    form.Id,
                    normalizedSlug,
                    session.Id,
                    session.ParticipantMobile,
                    session.ParticipantFullName);

                return ApiResponse<SubmitFormPublicResponseDto>.CreateSuccess(
                    new SubmitFormPublicResponseDto { SubmissionId = submission.Id },
                    "فرم با موفقیت ثبت شد",
                    201);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Duplicate or database error submitting public form for slug {Slug}", slug);
                return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                    "این شماره قبلاً این فرم را پر کرده است",
                    errorCode: ErrorCodes.ValidationFailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting public form for slug {Slug}", slug);
                return ApiResponse<SubmitFormPublicResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private const string FormNotFoundMessage = "فرم یافت نشد";
        private const string FormInactiveMessage = "این فرم در حال حاضر فعال نیست و امکان تکمیل آن وجود ندارد";

        /// <summary>
        /// دسترسی عمومی فقط برای فرم Published + Active. غیرفعال = RESOURCE_INACTIVE.
        /// </summary>
        private static ApiResponse<T>? EnsurePubliclyAvailable<T>(UserForm? form)
        {
            if (form == null)
            {
                return ApiResponse<T>.NotFound(FormNotFoundMessage);
            }

            if (!form.IsActive)
            {
                return ApiResponse<T>.Forbidden(FormInactiveMessage, ErrorCodes.ResourceInactive);
            }

            return null;
        }

        private async Task<(UserForm? Form, ApiResponse<T>? Error)> ResolvePubliclyAvailableFormAsync<T>(string slug)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (normalizedSlug == null)
            {
                return (null, ApiResponse<T>.BadRequest("لینک نامعتبر است", errorCode: ErrorCodes.InvalidInput));
            }

            var form = await _userFormRepository.GetBySlugReadOnlyAsync(normalizedSlug);
            return (form, EnsurePubliclyAvailable<T>(form));
        }

        private static void InjectContactFieldValues(
            IEnumerable<UserFormField> fields,
            Dictionary<string, string?> values,
            PublicParticipantSession session)
        {
            foreach (var field in fields.Where(f => f.IsActive && ContactFieldKeys.Contains(f.FieldKey)))
            {
                if (string.Equals(field.FieldKey, "mobile", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field.FieldKey, "phone", StringComparison.OrdinalIgnoreCase))
                {
                    values[field.FieldKey] = session.ParticipantMobile;
                }
                else
                {
                    values[field.FieldKey] = session.ParticipantFullName;
                }
            }
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

        private static FormPublicDto MapToPublicDto(UserForm form) => new()
        {
            Title = form.Title,
            Description = form.Description,
            Slug = form.Slug ?? string.Empty,
            TemplateKey = form.TemplateKey,
            Fields = form.Fields
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .Select(f => new FormPublicFieldDto
                {
                    FieldKey = f.FieldKey,
                    FieldType = f.FieldType,
                    Label = f.Label,
                    Placeholder = f.Placeholder,
                    HelpText = f.HelpText,
                    IsRequired = f.IsRequired
                })
                .ToList()
        };
    }
}
