using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت فرم‌های کاربر (فرم‌ساز)
    /// </summary>
    public class UserFormService : IUserFormService
    {
        private readonly IUserFormRepository _userFormRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly FormBuilderOptions _formBuilderOptions;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<UserFormService> _logger;

        public UserFormService(
            IUserFormRepository userFormRepository,
            Api_Vapp.Data.Api_Context context,
            IOptions<FormBuilderOptions> formBuilderOptions,
            IFileUploadService fileUploadService,
            ILogger<UserFormService> logger)
        {
            _userFormRepository = userFormRepository;
            _context = context;
            _formBuilderOptions = formBuilderOptions.Value;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        public async Task<ApiResponse<UserFormResponseDto>> CreateDraftAsync(int userId, CreateUserFormDto createDto)
        {
            try
            {
                var validation = await ValidateCreateRequestAsync(userId, createDto);
                if (validation != null)
                {
                    return validation;
                }

                var form = new UserForm
                {
                    UserId = userId,
                    Title = createDto.Title?.Trim() ?? string.Empty,
                    Description = NormalizeOptionalText(createDto.Description),
                    Slug = UserFormSlugHelper.Normalize(createDto.Slug),
                    TemplateKey = NormalizeOptionalText(createDto.TemplateKey),
                    Status = UserFormStatus.Draft,
                    SaveToPhonebook = createDto.SaveToPhonebook,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var fieldDto in createDto.Fields.OrderBy(f => f.DisplayOrder))
                {
                    form.Fields.Add(MapField(fieldDto));
                }

                foreach (var notebookId in createDto.NotebookIds.Distinct())
                {
                    form.Notebooks.Add(new UserFormNotebook { ContactNotebookId = notebookId });
                }

                await _context.UserForms.AddAsync(form);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User form draft created with ID {FormId} for user {UserId}", form.Id, userId);

                return ApiResponse<UserFormResponseDto>.CreateSuccess(
                    MapToResponseDto(form),
                    "پیش‌نویس فرم با موفقیت ایجاد شد",
                    201);
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<UserFormResponseDto>(dbEx, "creating user form draft", userId: userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user form draft for user {UserId}", userId);
                return ApiResponse<UserFormResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserFormResponseDto>> UpdateInfoAsync(int id, int userId, UpdateUserFormInfoDto? updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (!HasAnyInfoChanges(updateDto))
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var formResult = await GetTrackedFormForUserAsync(id, userId);
                if (formResult.Error != null)
                {
                    return formResult.Error;
                }

                var form = formResult.Form!;

                if (!string.IsNullOrWhiteSpace(updateDto.Slug))
                {
                    var slugValidation = await ValidateSlugAsync(updateDto.Slug, id);
                    if (slugValidation.Error != null)
                    {
                        return slugValidation.Error;
                    }

                    form.Slug = slugValidation.NormalizedSlug;
                }

                if (updateDto.Title != null)
                {
                    if (string.IsNullOrWhiteSpace(updateDto.Title))
                    {
                        return ApiResponse<UserFormResponseDto>.BadRequest(
                            "عنوان فرم نمی‌تواند خالی باشد",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    form.Title = updateDto.Title.Trim();
                }

                if (updateDto.Description != null)
                {
                    form.Description = NormalizeOptionalText(updateDto.Description);
                }

                if (updateDto.SaveToPhonebook.HasValue)
                {
                    form.SaveToPhonebook = updateDto.SaveToPhonebook.Value;

                    if (!updateDto.SaveToPhonebook.Value && updateDto.NotebookIds == null)
                    {
                        await ClearNotebookLinksAsync(form);
                    }
                }

                List<int>? notebookIdsForValidation = null;
                if (updateDto.NotebookIds != null)
                {
                    var distinctNotebookIds = updateDto.NotebookIds.Distinct().ToList();
                    notebookIdsForValidation = distinctNotebookIds;

                    var notebookErrors = await ValidateNotebookIdsAsync(userId, distinctNotebookIds);
                    if (notebookErrors.Count > 0)
                    {
                        return ApiResponse<UserFormResponseDto>.BadRequest(
                            "دفترچه‌های انتخاب‌شده نامعتبر است",
                            notebookErrors,
                            ErrorCodes.ValidationFailed);
                    }

                    await ClearNotebookLinksAsync(form);
                    foreach (var notebookId in distinctNotebookIds)
                    {
                        form.Notebooks.Add(new UserFormNotebook
                        {
                            UserFormId = id,
                            ContactNotebookId = notebookId
                        });
                    }
                }

                return await SaveFormWithPhonebookValidationAsync(
                    form,
                    notebookIdsForValidation,
                    "فرم با موفقیت به‌روزرسانی شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<UserFormResponseDto>(dbEx, "updating user form info", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user form info {FormId} for user {UserId}", id, userId);
                return ApiResponse<UserFormResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserFormResponseDto>> UpdateFieldsAsync(int id, int userId, UpdateUserFormFieldsDto? updateDto)
        {
            try
            {
                if (updateDto == null || updateDto.Fields.Count == 0)
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "حداقل یک فیلد برای به‌روزرسانی الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var formResult = await GetTrackedFormForUserAsync(id, userId);
                if (formResult.Error != null)
                {
                    return formResult.Error;
                }

                var form = formResult.Form!;
                var fieldLookup = BuildFieldLookup(form);
                var fieldErrors = ValidatePartialFieldUpdates(fieldLookup, updateDto.Fields);
                if (fieldErrors.Count > 0)
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "داده‌های فیلدها نامعتبر است",
                        fieldErrors,
                        ErrorCodes.ValidationFailed);
                }

                MergeFormFields(form, updateDto.Fields, fieldLookup);

                var mergedFieldDtos = form.Fields.Select(MapFieldToDto).ToList();
                var mergedFieldErrors = ValidateFields(mergedFieldDtos);
                if (mergedFieldErrors.Count > 0)
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "داده‌های فیلدها نامعتبر است",
                        mergedFieldErrors,
                        ErrorCodes.ValidationFailed);
                }

                return await SaveFormWithPhonebookValidationAsync(
                    form,
                    notebookIdsOverride: null,
                    "فیلدهای فرم با موفقیت به‌روزرسانی شد",
                    mergedFieldDtos);
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<UserFormResponseDto>(dbEx, "updating user form fields", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user form fields {FormId} for user {UserId}", id, userId);
                return ApiResponse<UserFormResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserFormResponseDto>> PublishAsync(int id, int userId, PublishUserFormDto? publishDto = null)
        {
            try
            {
                var form = await _userFormRepository.GetByIdWithDetailsTrackedAsync(id);
                if (form == null)
                {
                    return ApiResponse<UserFormResponseDto>.NotFound("فرم یافت نشد");
                }

                if (form.UserId != userId)
                {
                    return ApiResponse<UserFormResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                if (form.Status == UserFormStatus.Published)
                {
                    return ApiResponse<UserFormResponseDto>.CreateSuccess(
                        MapToResponseDto(form),
                        "فرم قبلاً منتشر شده است");
                }

                var publishError = await ValidateAndApplyPublishAsync(form, id, publishDto);
                if (publishError != null)
                {
                    return publishError;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("User form {FormId} published with slug {Slug}", id, form.Slug);

                return ApiResponse<UserFormResponseDto>.CreateSuccess(
                    MapToResponseDto(form),
                    "فرم با موفقیت منتشر شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<UserFormResponseDto>(dbEx, "publishing user form", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing user form {FormId} for user {UserId}", id, userId);
                return ApiResponse<UserFormResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserFormListResponseDto>> GetFormsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1)
                {
                    return ApiResponse<UserFormListResponseDto>.BadRequest(
                        "شماره صفحه باید بزرگتر از صفر باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (pageSize < 1 || pageSize > 100)
                {
                    return ApiResponse<UserFormListResponseDto>.BadRequest(
                        "تعداد در هر صفحه باید بین 1 تا 100 باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var (items, totalCount) = await _userFormRepository.GetByUserIdPagedAsync(userId, pageNumber, pageSize);

                var summaries = items.Select(form => new UserFormSummaryDto
                {
                    Id = form.Id,
                    Title = form.Title,
                    Slug = form.Slug,
                    Status = form.Status.ToString(),
                    IsActive = GetEffectiveIsActive(form),
                    PublicUrl = BuildPublicUrl(form.Slug),
                    CreatedAt = EnsureUtc(form.CreatedAt),
                    PublishedAt = EnsureUtc(form.PublishedAt)
                }).ToList();

                return ApiResponse<UserFormListResponseDto>.CreateSuccess(new UserFormListResponseDto
                {
                    Forms = PagedResponse<UserFormSummaryDto>.Create(summaries, totalCount, pageNumber, pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing user forms for user {UserId}", userId);
                return ApiResponse<UserFormListResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserFormResponseDto>> GetByIdAsync(int id, int userId)
        {
            try
            {
                var form = await _userFormRepository.GetByIdWithDetailsReadOnlyAsync(id);
                if (form == null)
                {
                    return ApiResponse<UserFormResponseDto>.NotFound("فرم یافت نشد");
                }

                if (form.UserId != userId)
                {
                    return ApiResponse<UserFormResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                return ApiResponse<UserFormResponseDto>.CreateSuccess(MapToResponseDto(form));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user form {FormId} for user {UserId}", id, userId);
                return ApiResponse<UserFormResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id, int userId)
        {
            try
            {
                var form = await _userFormRepository.GetOwnedFormAsync(id, userId, tracked: true);
                if (form == null)
                {
                    return ApiResponse<bool>.NotFound("فرم یافت نشد");
                }

                form.IsDeleted = true;
                form.Slug = null;
                form.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                try
                {
                    var deletedFiles = await _fileUploadService.DeleteAllEntityFilesAsync(
                        FileUploadConstants.EntityType_UserForm,
                        id);

                    if (deletedFiles > 0)
                    {
                        _logger.LogInformation(
                            "Deleted {Count} file(s) for user form {FormId}",
                            deletedFiles,
                            id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting files for user form {FormId}", id);
                }

                _logger.LogInformation("User form {FormId} soft-deleted for user {UserId}", id, userId);

                return ApiResponse<bool>.CreateSuccess(true, "فرم با موفقیت حذف شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<bool>(dbEx, "deleting user form", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user form {FormId} for user {UserId}", id, userId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserFormResponseDto>> SetActiveStatusAsync(int id, int userId, bool isActive)
        {
            try
            {
                var form = await _userFormRepository.GetByIdWithDetailsTrackedAsync(id);
                if (form == null)
                {
                    return ApiResponse<UserFormResponseDto>.NotFound("فرم یافت نشد");
                }

                if (form.UserId != userId)
                {
                    return ApiResponse<UserFormResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                var currentEffective = GetEffectiveIsActive(form);
                if (currentEffective == isActive)
                {
                    return ApiResponse<UserFormResponseDto>.CreateSuccess(
                        MapToResponseDto(form),
                        isActive ? "فرم از قبل فعال است" : "فرم از قبل غیرفعال است");
                }

                if (isActive)
                {
                    if (form.Status == UserFormStatus.Draft)
                    {
                        var publishError = await ValidateAndApplyPublishAsync(form, id, publishDto: null);
                        if (publishError != null)
                        {
                            return publishError;
                        }

                        await _context.SaveChangesAsync();

                        _logger.LogInformation("User form {FormId} activated via publish", id);

                        return ApiResponse<UserFormResponseDto>.CreateSuccess(
                            MapToResponseDto(form),
                            "فرم فعال شد");
                    }

                    form.IsActive = true;
                    form.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return ApiResponse<UserFormResponseDto>.CreateSuccess(
                        MapToResponseDto(form),
                        "فرم فعال شد");
                }

                form.IsActive = false;
                form.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<UserFormResponseDto>.CreateSuccess(
                    MapToResponseDto(form),
                    "فرم غیرفعال شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<UserFormResponseDto>(dbEx, "setting user form active status", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting active status for user form {FormId} for user {UserId}", id, userId);
                return ApiResponse<UserFormResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<UserFormResponseDto>?> ValidateAndApplyPublishAsync(
            UserForm form,
            int formId,
            PublishUserFormDto? publishDto)
        {
            if (string.IsNullOrWhiteSpace(form.Title))
            {
                return ApiResponse<UserFormResponseDto>.BadRequest(
                    "عنوان فرم الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (!form.Fields.Any(f => f.IsActive))
            {
                return ApiResponse<UserFormResponseDto>.BadRequest(
                    "حداقل یک فیلد فعال برای انتشار لازم است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (form.SaveToPhonebook)
            {
                var phonebookErrors = ValidatePhonebookSettings(
                    form.SaveToPhonebook,
                    form.Notebooks.Select(n => n.ContactNotebookId).ToList(),
                    form.Fields.Select(MapFieldToDto).ToList());

                if (phonebookErrors.Count > 0)
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "تنظیمات دفترچه تلفن نامعتبر است",
                        phonebookErrors,
                        ErrorCodes.ValidationFailed);
                }
            }

            string slug;
            if (!string.IsNullOrWhiteSpace(publishDto?.Slug))
            {
                var slugValidation = await ValidateSlugAsync(publishDto.Slug, formId);
                if (slugValidation.Error != null)
                {
                    return slugValidation.Error;
                }

                slug = slugValidation.NormalizedSlug!;
            }
            else if (!string.IsNullOrWhiteSpace(form.Slug))
            {
                slug = form.Slug;
            }
            else
            {
                slug = await GenerateUniqueSlugAsync(form.Title, formId);
            }

            form.Slug = slug;
            form.Status = UserFormStatus.Published;
            form.IsActive = true;
            form.PublishedAt = DateTime.UtcNow;
            form.UpdatedAt = DateTime.UtcNow;

            return null;
        }

        private static bool GetEffectiveIsActive(UserForm form)
        {
            return form.Status == UserFormStatus.Published && form.IsActive;
        }

        private async Task<ApiResponse<UserFormResponseDto>?> ValidateCreateRequestAsync(int userId, CreateUserFormDto createDto)
        {
            var fieldErrors = ValidateFields(createDto.Fields);
            if (fieldErrors.Count > 0)
            {
                return ApiResponse<UserFormResponseDto>.BadRequest(
                    "داده‌های فیلدها نامعتبر است",
                    fieldErrors,
                    ErrorCodes.ValidationFailed);
            }

            if (!string.IsNullOrWhiteSpace(createDto.Slug))
            {
                var slugValidation = await ValidateSlugAsync(createDto.Slug);
                if (slugValidation.Error != null)
                {
                    return slugValidation.Error;
                }
            }

            var notebookErrors = await ValidateNotebookIdsAsync(userId, createDto.NotebookIds);
            if (notebookErrors.Count > 0)
            {
                return ApiResponse<UserFormResponseDto>.BadRequest(
                    "دفترچه‌های انتخاب‌شده نامعتبر است",
                    notebookErrors,
                    ErrorCodes.ValidationFailed);
            }

            if (createDto.SaveToPhonebook)
            {
                var phonebookErrors = ValidatePhonebookSettings(
                    createDto.SaveToPhonebook,
                    createDto.NotebookIds,
                    createDto.Fields);

                if (phonebookErrors.Count > 0)
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "تنظیمات دفترچه تلفن نامعتبر است",
                        phonebookErrors,
                        ErrorCodes.ValidationFailed);
                }
            }

            return null;
        }

        private async Task<(ApiResponse<UserFormResponseDto>? Error, string? NormalizedSlug)> ValidateSlugAsync(
            string slug,
            int? excludeFormId = null)
        {
            var normalizedSlug = UserFormSlugHelper.Normalize(slug);
            if (normalizedSlug == null)
            {
                return (ApiResponse<UserFormResponseDto>.BadRequest(
                    "فرمت slug نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed), null);
            }

            if (await _userFormRepository.SlugExistsAsync(normalizedSlug, excludeFormId))
            {
                return (ApiResponse<UserFormResponseDto>.BadRequest(
                    "این slug قبلاً استفاده شده است",
                    errorCode: ErrorCodes.ValidationFailed), null);
            }

            return (null, normalizedSlug);
        }

        private static bool HasAnyInfoChanges(UpdateUserFormInfoDto updateDto)
        {
            return updateDto.Title != null
                || updateDto.Description != null
                || !string.IsNullOrWhiteSpace(updateDto.Slug)
                || updateDto.SaveToPhonebook.HasValue
                || updateDto.NotebookIds != null;
        }

        private async Task<(UserForm? Form, ApiResponse<UserFormResponseDto>? Error)> GetTrackedFormForUserAsync(int id, int userId)
        {
            var form = await _userFormRepository.GetByIdWithDetailsTrackedForUserAsync(id, userId);
            if (form != null)
            {
                return (form, null);
            }

            var exists = await _userFormRepository.GetByIdAsync(id);
            if (exists == null)
            {
                return (null, ApiResponse<UserFormResponseDto>.NotFound("فرم یافت نشد"));
            }

            return (null, ApiResponse<UserFormResponseDto>.Forbidden(
                ControlledErrorHelper.Unauthorized,
                ErrorCodes.Forbidden));
        }

        private async Task<ApiResponse<UserFormResponseDto>> SaveFormWithPhonebookValidationAsync(
            UserForm form,
            List<int>? notebookIdsOverride,
            string successMessage,
            List<UserFormFieldDto>? fieldsForValidation = null)
        {
            fieldsForValidation ??= form.Fields.Select(MapFieldToDto).ToList();
            var notebookIdsForValidation = notebookIdsOverride
                ?? form.Notebooks.Select(n => n.ContactNotebookId).ToList();

            if (form.SaveToPhonebook)
            {
                var phonebookErrors = ValidatePhonebookSettings(
                    form.SaveToPhonebook,
                    notebookIdsForValidation,
                    fieldsForValidation);

                if (phonebookErrors.Count > 0)
                {
                    return ApiResponse<UserFormResponseDto>.BadRequest(
                        "تنظیمات دفترچه تلفن نامعتبر است",
                        phonebookErrors,
                        ErrorCodes.ValidationFailed);
                }
            }

            form.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return ApiResponse<UserFormResponseDto>.CreateSuccess(
                MapToResponseDto(form),
                successMessage);
        }

        private static Dictionary<string, UserFormField> BuildFieldLookup(UserForm form)
        {
            return form.Fields.ToDictionary(f => f.FieldKey, StringComparer.OrdinalIgnoreCase);
        }

        private static void MergeFormFields(
            UserForm form,
            List<UpdateUserFormFieldDto> incomingFields,
            Dictionary<string, UserFormField> fieldLookup)
        {
            foreach (var fieldDto in incomingFields)
            {
                var fieldKey = fieldDto.FieldKey.Trim();

                if (fieldLookup.TryGetValue(fieldKey, out var existing))
                {
                    ApplyPartialFieldUpdate(existing, fieldDto);
                }
                else
                {
                    var created = MapFieldFromPartial(fieldDto);
                    form.Fields.Add(created);
                    fieldLookup[fieldKey] = created;
                }
            }
        }

        private static void ApplyPartialFieldUpdate(UserFormField existing, UpdateUserFormFieldDto fieldDto)
        {
            if (!string.IsNullOrWhiteSpace(fieldDto.FieldType))
            {
                existing.FieldType = fieldDto.FieldType.Trim().ToLowerInvariant();
            }

            if (fieldDto.Label != null)
            {
                existing.Label = fieldDto.Label.Trim();
            }

            if (fieldDto.Placeholder != null)
            {
                existing.Placeholder = NormalizeOptionalText(fieldDto.Placeholder);
            }

            if (fieldDto.HelpText != null)
            {
                existing.HelpText = NormalizeOptionalText(fieldDto.HelpText);
            }

            if (fieldDto.IsActive.HasValue)
            {
                existing.IsActive = fieldDto.IsActive.Value;
            }

            if (fieldDto.IsRequired.HasValue)
            {
                existing.IsRequired = fieldDto.IsRequired.Value;
            }

            if (fieldDto.DisplayOrder.HasValue)
            {
                existing.DisplayOrder = fieldDto.DisplayOrder.Value;
            }

            if (fieldDto.SourceFieldKey != null)
            {
                existing.SourceFieldKey = NormalizeOptionalText(fieldDto.SourceFieldKey);
            }
        }

        private static UserFormField MapFieldFromPartial(UpdateUserFormFieldDto dto)
        {
            return new UserFormField
            {
                FieldKey = dto.FieldKey.Trim(),
                FieldType = dto.FieldType!.Trim().ToLowerInvariant(),
                Label = dto.Label!.Trim(),
                Placeholder = dto.Placeholder != null ? NormalizeOptionalText(dto.Placeholder) : null,
                HelpText = dto.HelpText != null ? NormalizeOptionalText(dto.HelpText) : null,
                IsActive = dto.IsActive ?? true,
                IsRequired = dto.IsRequired ?? false,
                DisplayOrder = dto.DisplayOrder ?? 0,
                SourceFieldKey = dto.SourceFieldKey != null ? NormalizeOptionalText(dto.SourceFieldKey) : null
            };
        }

        private async Task ClearNotebookLinksAsync(UserForm form)
        {
            foreach (var link in form.Notebooks.ToList())
            {
                _context.Entry(link).State = EntityState.Detached;
            }

            form.Notebooks.Clear();

            await _context.UserFormNotebooks
                .Where(n => n.UserFormId == form.Id)
                .ExecuteDeleteAsync();
        }

        private UserFormResponseDto MapToResponseDto(UserForm form)
        {
            return new UserFormResponseDto
            {
                Id = form.Id,
                Title = form.Title,
                Description = form.Description,
                Slug = form.Slug,
                TemplateKey = form.TemplateKey,
                TemplateId = form.TemplateId,
                Status = form.Status.ToString(),
                SaveToPhonebook = form.SaveToPhonebook,
                IsActive = GetEffectiveIsActive(form),
                PublicUrl = BuildPublicUrl(form.Slug),
                NotebookIds = form.Notebooks.Select(n => n.ContactNotebookId).ToList(),
                Fields = form.Fields
                    .OrderBy(f => f.DisplayOrder)
                    .Select(MapFieldToDto)
                    .ToList(),
                CreatedAt = EnsureUtc(form.CreatedAt),
                UpdatedAt = EnsureUtc(form.UpdatedAt),
                PublishedAt = EnsureUtc(form.PublishedAt)
            };
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static DateTime? EnsureUtc(DateTime? value)
        {
            return value.HasValue ? EnsureUtc(value.Value) : null;
        }

        private static UserFormFieldDto MapFieldToDto(UserFormField field)
        {
            return new UserFormFieldDto
            {
                FieldKey = field.FieldKey,
                FieldType = field.FieldType,
                Label = field.Label,
                Placeholder = field.Placeholder,
                HelpText = field.HelpText,
                IsActive = field.IsActive,
                IsRequired = field.IsRequired,
                DisplayOrder = field.DisplayOrder,
                SourceFieldKey = field.SourceFieldKey
            };
        }

        private static UserFormField MapField(UserFormFieldDto dto)
        {
            return new UserFormField
            {
                FieldKey = dto.FieldKey.Trim(),
                FieldType = dto.FieldType.Trim().ToLowerInvariant(),
                Label = dto.Label.Trim(),
                Placeholder = NormalizeOptionalText(dto.Placeholder),
                HelpText = NormalizeOptionalText(dto.HelpText),
                IsActive = dto.IsActive,
                IsRequired = dto.IsRequired,
                DisplayOrder = dto.DisplayOrder,
                SourceFieldKey = NormalizeOptionalText(dto.SourceFieldKey)
            };
        }

        private string? BuildPublicUrl(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(_formBuilderOptions.PublicBaseUrl))
            {
                return null;
            }

            return $"{_formBuilderOptions.PublicBaseUrl.TrimEnd('/')}/{slug}";
        }

        private static List<string> ValidatePartialFieldUpdates(
            Dictionary<string, UserFormField> fieldLookup,
            List<UpdateUserFormFieldDto> fields)
        {
            var errors = new List<string>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldKey))
                {
                    errors.Add("fieldKey برای همه فیلدها الزامی است");
                    continue;
                }

                var fieldKey = field.FieldKey.Trim();
                if (!keys.Add(fieldKey))
                {
                    errors.Add($"fieldKey تکراری: {fieldKey}");
                    continue;
                }

                fieldLookup.TryGetValue(fieldKey, out var existing);

                if (existing == null)
                {
                    if (string.IsNullOrWhiteSpace(field.FieldType))
                    {
                        errors.Add($"نوع فیلد {fieldKey} الزامی است");
                    }

                    if (string.IsNullOrWhiteSpace(field.Label))
                    {
                        errors.Add($"عنوان فیلد {fieldKey} الزامی است");
                    }
                }
                else if (field.Label != null && string.IsNullOrWhiteSpace(field.Label))
                {
                    errors.Add($"عنوان فیلد {fieldKey} الزامی است");
                }
            }

            return errors;
        }

        private static List<string> ValidateFields(List<UserFormFieldDto> fields)
        {
            var errors = new List<string>();

            if (fields.Count > UserFormConstants.MaxFieldsPerForm)
            {
                errors.Add($"حداکثر {UserFormConstants.MaxFieldsPerForm} فیلد برای هر فرم مجاز است");
                return errors;
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldKey))
                {
                    errors.Add("fieldKey برای همه فیلدها الزامی است");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(field.FieldType))
                {
                    errors.Add($"نوع فیلد {field.FieldKey} الزامی است");
                }

                if (string.IsNullOrWhiteSpace(field.Label))
                {
                    errors.Add($"عنوان فیلد {field.FieldKey} الزامی است");
                }

                if (!keys.Add(field.FieldKey.Trim()))
                {
                    errors.Add($"fieldKey تکراری: {field.FieldKey}");
                }
            }

            return errors;
        }

        private static List<string> ValidatePhonebookSettings(
            bool saveToPhonebook,
            IReadOnlyList<int> notebookIds,
            IReadOnlyList<UserFormFieldDto> fields)
        {
            var errors = new List<string>();

            if (!saveToPhonebook)
            {
                return errors;
            }

            if (notebookIds.Count == 0)
            {
                errors.Add("حداقل یک دفترچه تلفن باید انتخاب شود");
            }

            var hasMobileField = fields.Any(f =>
                f.IsActive &&
                (string.Equals(f.FieldKey, "mobile", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(f.FieldType, "mobile", StringComparison.OrdinalIgnoreCase)));

            if (!hasMobileField)
            {
                errors.Add("برای ذخیره در دفترچه، فیلد موبایل فعال الزامی است");
            }

            return errors;
        }

        private async Task<List<string>> ValidateNotebookIdsAsync(int userId, List<int> notebookIds)
        {
            var errors = new List<string>();
            var distinctIds = notebookIds.Distinct().ToList();

            if (distinctIds.Count == 0)
            {
                return errors;
            }

            var validIds = await _context.ContactNotebooks
                .AsNoTracking()
                .Where(n => distinctIds.Contains(n.Id) && n.UserId == userId && !n.IsDeleted)
                .Select(n => n.Id)
                .ToListAsync();

            var validSet = validIds.ToHashSet();

            foreach (var notebookId in distinctIds)
            {
                if (!validSet.Contains(notebookId))
                {
                    errors.Add($"دفترچه {notebookId} یافت نشد یا متعلق به شما نیست");
                }
            }

            return errors;
        }

        private async Task<string> GenerateUniqueSlugAsync(string title, int excludeFormId)
        {
            var baseSlug = UserFormSlugHelper.SlugifyTitle(title);
            var existingSlugs = (await _userFormRepository.GetExistingSlugsWithPrefixAsync(baseSlug, excludeFormId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var suffix = 0; suffix <= UserFormConstants.SlugGenerationMaxAttempts; suffix++)
            {
                var candidate = UserFormSlugHelper.BuildCandidateSlug(baseSlug, suffix);
                if (!existingSlugs.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseSlug}-{Guid.NewGuid():N}"[..UserFormConstants.MaxSlugLength].Trim('-');
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private ApiResponse<T> MapDbUpdateException<T>(
            DbUpdateException dbEx,
            string operation,
            int? formId = null,
            int? userId = null)
        {
            if (IsUniqueConstraintViolation(dbEx))
            {
                _logger.LogWarning(
                    dbEx,
                    "Unique constraint violation while {Operation} — FormId: {FormId}, UserId: {UserId}",
                    operation,
                    formId,
                    userId);

                return ApiResponse<T>.BadRequest(
                    "اطلاعات ارسالی با داده‌های موجود تداخل دارد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            _logger.LogError(
                dbEx,
                "Database error while {Operation} — FormId: {FormId}, UserId: {UserId}",
                operation,
                formId,
                userId);

            return ApiResponse<T>.InternalServerError(ControlledErrorHelper.Database, ErrorCodes.DatabaseError);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
