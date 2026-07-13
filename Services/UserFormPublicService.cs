using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class UserFormPublicService : IUserFormPublicService
    {
        private readonly IUserFormRepository _userFormRepository;
        private readonly Api_Context _context;
        private readonly PublicPhonebookService _phonebookService;
        private readonly ILogger<UserFormPublicService> _logger;

        public UserFormPublicService(
            IUserFormRepository userFormRepository,
            Api_Context context,
            PublicPhonebookService phonebookService,
            ILogger<UserFormPublicService> logger)
        {
            _userFormRepository = userFormRepository;
            _context = context;
            _phonebookService = phonebookService;
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
                if (form == null)
                {
                    return ApiResponse<FormPublicDto>.NotFound("فرم یافت نشد یا غیرفعال است");
                }

                return ApiResponse<FormPublicDto>.CreateSuccess(MapToPublicDto(form));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public form for slug {Slug}", slug);
                return ApiResponse<FormPublicDto>.InternalServerError(ControlledErrorHelper.Unexpected);
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

            if (string.IsNullOrWhiteSpace(dto.ParticipantFullName))
            {
                return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                    "نام الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var mobile = BookingMobileHelper.Normalize(dto.ParticipantMobile);
            if (!BookingMobileHelper.IsValidIranianMobile(mobile))
            {
                return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                    "شماره موبایل نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var form = await _context.UserForms
                    .AsSplitQuery()
                    .Include(f => f.Fields.Where(field => field.IsActive).OrderBy(field => field.DisplayOrder))
                    .Include(f => f.Notebooks)
                    .FirstOrDefaultAsync(f =>
                        f.Slug == normalizedSlug &&
                        !f.IsDeleted &&
                        f.Status == UserFormStatus.Published &&
                        f.IsActive);

                if (form == null)
                {
                    return ApiResponse<SubmitFormPublicResponseDto>.NotFound("فرم یافت نشد یا غیرفعال است");
                }

                var values = dto.Values ?? new Dictionary<string, string?>();
                var fieldErrors = UserFormFieldValueValidator.Validate(form.Fields.ToList(), values);
                if (fieldErrors.Count > 0)
                {
                    return ApiResponse<SubmitFormPublicResponseDto>.BadRequest(
                        "داده‌های فرم نامعتبر است",
                        fieldErrors,
                        ErrorCodes.ValidationFailed);
                }

                var now = DateTime.UtcNow;
                var submission = new UserFormSubmission
                {
                    UserFormId = form.Id,
                    ParticipantFullName = dto.ParticipantFullName.Trim(),
                    ParticipantMobile = mobile,
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
                        mobile,
                        submission.ParticipantFullName);

                    if (contactId.HasValue)
                    {
                        submission.ContactId = contactId;
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Public form submission {SubmissionId} created for form {FormId}",
                    submission.Id,
                    form.Id);

                return ApiResponse<SubmitFormPublicResponseDto>.CreateSuccess(
                    new SubmitFormPublicResponseDto { SubmissionId = submission.Id },
                    "فرم با موفقیت ثبت شد",
                    201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error submitting public form for slug {Slug}", slug);
                return ApiResponse<SubmitFormPublicResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
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
