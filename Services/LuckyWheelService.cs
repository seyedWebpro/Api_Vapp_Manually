using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت گردونه شانس
    /// </summary>
    public class LuckyWheelService : ILuckyWheelService
    {
        private readonly ILuckyWheelRepository _luckyWheelRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly LuckyWheelOptions _luckyWheelOptions;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<LuckyWheelService> _logger;

        public LuckyWheelService(
            ILuckyWheelRepository luckyWheelRepository,
            Api_Vapp.Data.Api_Context context,
            IOptions<LuckyWheelOptions> luckyWheelOptions,
            IFileUploadService fileUploadService,
            ILogger<LuckyWheelService> logger)
        {
            _luckyWheelRepository = luckyWheelRepository;
            _context = context;
            _luckyWheelOptions = luckyWheelOptions.Value;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        public async Task<ApiResponse<LuckyWheelResponseDto>> CreateDraftAsync(int userId, CreateLuckyWheelDto createDto)
        {
            _logger.LogInformation("Creating lucky wheel draft for user {UserId}", userId);

            try
            {
                var validation = await ValidateCreateRequestAsync(userId, createDto);
                if (validation != null)
                {
                    return validation;
                }

                var wheel = new LuckyWheel
                {
                    UserId = userId,
                    Title = createDto.Title?.Trim() ?? string.Empty,
                    Description = NormalizeOptionalText(createDto.Description),
                    Slug = UserFormSlugHelper.Normalize(createDto.Slug),
                    Status = LuckyWheelStatus.Draft,
                    SaveToPhonebook = createDto.SaveToPhonebook,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var notebookId in createDto.NotebookIds.Distinct())
                {
                    wheel.Notebooks.Add(new LuckyWheelNotebook { ContactNotebookId = notebookId });
                }

                await _context.LuckyWheels.AddAsync(wheel);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Lucky wheel draft created with ID {WheelId} for user {UserId}", wheel.Id, userId);

                return ApiResponse<LuckyWheelResponseDto>.CreateSuccess(
                    MapToResponseDto(wheel),
                    "پیش‌نویس گردونه با موفقیت ایجاد شد",
                    201);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating lucky wheel draft for user {UserId}", userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating lucky wheel draft for user {UserId}", userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<LuckyWheelResponseDto>> UpdateAsync(int id, int userId, UpdateLuckyWheelDto updateDto)
        {
            _logger.LogInformation("Updating lucky wheel {WheelId} for user {UserId}", id, userId);

            try
            {
                var wheel = await _luckyWheelRepository.GetByIdWithDetailsTrackedAsync(id);
                if (wheel == null)
                {
                    return ApiResponse<LuckyWheelResponseDto>.NotFound("گردونه یافت نشد");
                }

                if (wheel.UserId != userId)
                {
                    return ApiResponse<LuckyWheelResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                if (!HasAnyChanges(updateDto))
                {
                    return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                        "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (updateDto.Items != null)
                {
                    var itemErrors = wheel.Status == LuckyWheelStatus.Published
                        ? ValidateItemsForPublish(updateDto.Items)
                        : ValidateItemsForEditing(updateDto.Items);
                    if (itemErrors.Count > 0)
                    {
                        return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                            "داده‌های جوایز نامعتبر است",
                            itemErrors,
                            ErrorCodes.ValidationFailed);
                    }
                }

                if (!string.IsNullOrWhiteSpace(updateDto.Slug))
                {
                    var slugValidation = await ValidateSlugAsync(updateDto.Slug, id);
                    if (slugValidation.Error != null)
                    {
                        return slugValidation.Error;
                    }

                    wheel.Slug = slugValidation.NormalizedSlug;
                }

                if (updateDto.Title != null)
                {
                    if (string.IsNullOrWhiteSpace(updateDto.Title))
                    {
                        return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                            "عنوان گردونه نمی‌تواند خالی باشد",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    wheel.Title = updateDto.Title.Trim();
                }

                if (updateDto.Description != null)
                {
                    wheel.Description = NormalizeOptionalText(updateDto.Description);
                }

                if (updateDto.SaveToPhonebook.HasValue)
                {
                    wheel.SaveToPhonebook = updateDto.SaveToPhonebook.Value;
                }

                if (updateDto.NotebookIds != null)
                {
                    var notebookErrors = await ValidateNotebookIdsAsync(userId, updateDto.NotebookIds);
                    if (notebookErrors.Count > 0)
                    {
                        return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                            "دفترچه‌های انتخاب‌شده نامعتبر است",
                            notebookErrors,
                            ErrorCodes.ValidationFailed);
                    }

                    await ClearNotebookLinksAsync(wheel);
                    foreach (var notebookId in updateDto.NotebookIds.Distinct())
                    {
                        wheel.Notebooks.Add(new LuckyWheelNotebook
                        {
                            LuckyWheelId = id,
                            ContactNotebookId = notebookId
                        });
                    }
                }

                if (updateDto.Items != null)
                {
                    await ReplaceItemsAsync(wheel, updateDto.Items);
                }

                var notebookIdsForValidation = updateDto.NotebookIds?.Distinct().ToList()
                    ?? wheel.Notebooks.Select(n => n.ContactNotebookId).ToList();

                if (wheel.SaveToPhonebook)
                {
                    var phonebookErrors = ValidatePhonebookSettings(wheel.SaveToPhonebook, notebookIdsForValidation);
                    if (phonebookErrors.Count > 0)
                    {
                        return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                            "تنظیمات دفترچه تلفن نامعتبر است",
                            phonebookErrors,
                            ErrorCodes.ValidationFailed);
                    }
                }

                wheel.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<LuckyWheelResponseDto>.CreateSuccess(
                    MapToResponseDto(wheel),
                    "گردونه با موفقیت به‌روزرسانی شد");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<LuckyWheelResponseDto>> AddItemsAsync(int id, int userId, AddLuckyWheelItemsDto addDto)
        {
            _logger.LogInformation(
                "Adding {Count} item(s) to lucky wheel {WheelId} for user {UserId}",
                addDto.Items.Count,
                id,
                userId);

            try
            {
                var wheel = await _luckyWheelRepository.GetByIdWithDetailsTrackedAsync(id);
                if (wheel == null)
                {
                    return ApiResponse<LuckyWheelResponseDto>.NotFound("گردونه یافت نشد");
                }

                if (wheel.UserId != userId)
                {
                    return ApiResponse<LuckyWheelResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                var mergedItems = wheel.Items
                    .Select(MapItemToDto)
                    .ToList();
                mergedItems.AddRange(addDto.Items);

                var itemErrors = wheel.Status == LuckyWheelStatus.Published
                    ? ValidateItemsForPublish(mergedItems)
                    : ValidateItemsForEditing(mergedItems);
                if (itemErrors.Count > 0)
                {
                    return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                        "داده‌های جوایز نامعتبر است",
                        itemErrors,
                        ErrorCodes.ValidationFailed);
                }

                foreach (var itemDto in addDto.Items)
                {
                    wheel.Items.Add(MapItem(itemDto));
                }

                wheel.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var message = addDto.Items.Count == 1
                    ? "آیتم جایزه با موفقیت اضافه شد"
                    : "آیتم‌های جایزه با موفقیت اضافه شدند";

                return ApiResponse<LuckyWheelResponseDto>.CreateSuccess(
                    MapToResponseDto(wheel),
                    message);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error adding items to lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding items to lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<LuckyWheelResponseDto>> PublishAsync(int id, int userId, PublishLuckyWheelDto? publishDto = null)
        {
            _logger.LogInformation("Publishing lucky wheel {WheelId} for user {UserId}", id, userId);

            try
            {
                var wheel = await _luckyWheelRepository.GetByIdWithDetailsTrackedAsync(id);
                if (wheel == null)
                {
                    return ApiResponse<LuckyWheelResponseDto>.NotFound("گردونه یافت نشد");
                }

                if (wheel.UserId != userId)
                {
                    return ApiResponse<LuckyWheelResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                if (string.IsNullOrWhiteSpace(wheel.Title))
                {
                    return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                        "عنوان گردونه الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var itemErrors = ValidateItemsForPublish(wheel.Items.Select(MapItemToDto).ToList());
                if (itemErrors.Count > 0)
                {
                    return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                        "جوایز گردونه نامعتبر است",
                        itemErrors,
                        ErrorCodes.ValidationFailed);
                }

                if (wheel.SaveToPhonebook)
                {
                    var phonebookErrors = ValidatePhonebookSettings(
                        wheel.SaveToPhonebook,
                        wheel.Notebooks.Select(n => n.ContactNotebookId).ToList());

                    if (phonebookErrors.Count > 0)
                    {
                        return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                            "تنظیمات دفترچه تلفن نامعتبر است",
                            phonebookErrors,
                            ErrorCodes.ValidationFailed);
                    }
                }

                string slug;
                if (!string.IsNullOrWhiteSpace(publishDto?.Slug))
                {
                    var slugValidation = await ValidateSlugAsync(publishDto.Slug, id);
                    if (slugValidation.Error != null)
                    {
                        return slugValidation.Error;
                    }

                    slug = slugValidation.NormalizedSlug!;
                }
                else if (!string.IsNullOrWhiteSpace(wheel.Slug))
                {
                    var existingSlugValidation = await ValidateSlugAsync(wheel.Slug, id);
                    if (existingSlugValidation.Error != null)
                    {
                        return existingSlugValidation.Error;
                    }

                    slug = existingSlugValidation.NormalizedSlug!;
                }
                else
                {
                    slug = await GenerateUniqueSlugAsync(wheel.Title, id);
                }

                wheel.Slug = slug;
                wheel.Status = LuckyWheelStatus.Published;
                wheel.IsActive = true;
                wheel.PublishedAt = DateTime.UtcNow;
                wheel.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Lucky wheel {WheelId} published with slug {Slug}", id, slug);

                return ApiResponse<LuckyWheelResponseDto>.CreateSuccess(
                    MapToResponseDto(wheel),
                    "گردونه با موفقیت منتشر شد");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error publishing lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<LuckyWheelListResponseDto>> GetWheelsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1)
                {
                    return ApiResponse<LuckyWheelListResponseDto>.BadRequest(
                        "شماره صفحه باید بزرگتر از صفر باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (pageSize < 1 || pageSize > 100)
                {
                    return ApiResponse<LuckyWheelListResponseDto>.BadRequest(
                        "تعداد در هر صفحه باید بین 1 تا 100 باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var (items, totalCount) = await _luckyWheelRepository.GetByUserIdPagedAsync(userId, pageNumber, pageSize);

                var summaries = new List<LuckyWheelSummaryDto>();
                foreach (var wheel in items)
                {
                    summaries.Add(new LuckyWheelSummaryDto
                    {
                        Id = wheel.Id,
                        Title = wheel.Title,
                        Slug = wheel.Slug,
                        Status = wheel.Status.ToString(),
                        IsActive = wheel.IsActive,
                        PublicUrl = BuildPublicUrl(wheel.Slug),
                        ParticipantCount = await _luckyWheelRepository.GetParticipantCountAsync(wheel.Id),
                        CreatedAt = EnsureUtc(wheel.CreatedAt),
                        PublishedAt = EnsureUtc(wheel.PublishedAt)
                    });
                }

                return ApiResponse<LuckyWheelListResponseDto>.CreateSuccess(new LuckyWheelListResponseDto
                {
                    Wheels = PagedResponse<LuckyWheelSummaryDto>.Create(summaries, totalCount, pageNumber, pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing lucky wheels for user {UserId}", userId);
                return ApiResponse<LuckyWheelListResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<LuckyWheelResponseDto>> GetByIdAsync(int id, int userId)
        {
            try
            {
                var wheel = await _luckyWheelRepository.GetByIdWithDetailsReadOnlyAsync(id);
                if (wheel == null)
                {
                    return ApiResponse<LuckyWheelResponseDto>.NotFound("گردونه یافت نشد");
                }

                if (wheel.UserId != userId)
                {
                    return ApiResponse<LuckyWheelResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                return ApiResponse<LuckyWheelResponseDto>.CreateSuccess(MapToResponseDto(wheel));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id, int userId)
        {
            _logger.LogInformation("Deleting lucky wheel {WheelId} for user {UserId}", id, userId);

            try
            {
                var wheel = await _luckyWheelRepository.GetOwnedWheelAsync(id, userId, tracked: true);
                if (wheel == null)
                {
                    return ApiResponse<bool>.NotFound("گردونه یافت نشد");
                }

                wheel.IsDeleted = true;
                wheel.Slug = null;
                wheel.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                try
                {
                    var deletedFiles = await _fileUploadService.DeleteAllEntityFilesAsync(
                        FileUploadConstants.EntityType_LuckyWheel,
                        id);

                    if (deletedFiles > 0)
                    {
                        _logger.LogInformation(
                            "Deleted {Count} file(s) for lucky wheel {WheelId}",
                            deletedFiles,
                            id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting files for lucky wheel {WheelId}", id);
                }

                _logger.LogInformation("Lucky wheel {WheelId} soft-deleted for user {UserId}", id, userId);

                return ApiResponse<bool>.CreateSuccess(true, "گردونه با موفقیت حذف شد");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Database, ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<LuckyWheelResponseDto>> SetActiveStatusAsync(int id, int userId, bool isActive)
        {
            _logger.LogInformation(
                "Setting lucky wheel {WheelId} active status to {IsActive} for user {UserId}",
                id,
                isActive,
                userId);

            try
            {
                var wheel = await _luckyWheelRepository.GetOwnedWheelAsync(id, userId, tracked: true);
                if (wheel == null)
                {
                    return ApiResponse<LuckyWheelResponseDto>.NotFound("گردونه یافت نشد");
                }

                if (wheel.Status != LuckyWheelStatus.Published)
                {
                    return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                        "فقط گردونه‌های منتشرشده قابل فعال/غیرفعال کردن هستند",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (wheel.IsActive == isActive)
                {
                    var current = await _luckyWheelRepository.GetByIdWithDetailsReadOnlyAsync(id);
                    return ApiResponse<LuckyWheelResponseDto>.CreateSuccess(
                        MapToResponseDto(current!),
                        isActive ? "گردونه از قبل فعال است" : "گردونه از قبل غیرفعال است");
                }

                wheel.IsActive = isActive;
                wheel.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var refreshed = await _luckyWheelRepository.GetByIdWithDetailsReadOnlyAsync(id);

                return ApiResponse<LuckyWheelResponseDto>.CreateSuccess(
                    MapToResponseDto(refreshed!),
                    isActive ? "گردونه فعال شد" : "گردونه غیرفعال شد");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error setting active status for lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting active status for lucky wheel {WheelId} for user {UserId}", id, userId);
                return ApiResponse<LuckyWheelResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<LuckyWheelResponseDto>?> ValidateCreateRequestAsync(int userId, CreateLuckyWheelDto createDto)
        {
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
                return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                    "دفترچه‌های انتخاب‌شده نامعتبر است",
                    notebookErrors,
                    ErrorCodes.ValidationFailed);
            }

            if (createDto.SaveToPhonebook)
            {
                var phonebookErrors = ValidatePhonebookSettings(createDto.SaveToPhonebook, createDto.NotebookIds);
                if (phonebookErrors.Count > 0)
                {
                    return ApiResponse<LuckyWheelResponseDto>.BadRequest(
                        "تنظیمات دفترچه تلفن نامعتبر است",
                        phonebookErrors,
                        ErrorCodes.ValidationFailed);
                }
            }

            return null;
        }

        private async Task<(ApiResponse<LuckyWheelResponseDto>? Error, string? NormalizedSlug)> ValidateSlugAsync(
            string slug,
            int? excludeWheelId = null)
        {
            var normalizedSlug = UserFormSlugHelper.Normalize(slug);
            if (normalizedSlug == null)
            {
                return (ApiResponse<LuckyWheelResponseDto>.BadRequest(
                    "فرمت slug نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed), null);
            }

            if (await _luckyWheelRepository.SlugExistsAsync(normalizedSlug, excludeWheelId))
            {
                return (ApiResponse<LuckyWheelResponseDto>.BadRequest(
                    "این slug قبلاً استفاده شده است",
                    errorCode: ErrorCodes.ValidationFailed), null);
            }

            return (null, normalizedSlug);
        }

        private static bool HasAnyChanges(UpdateLuckyWheelDto updateDto)
        {
            return updateDto.Title != null
                || updateDto.Description != null
                || !string.IsNullOrWhiteSpace(updateDto.Slug)
                || updateDto.SaveToPhonebook.HasValue
                || updateDto.NotebookIds != null
                || (updateDto.Items != null);
        }

        private async Task ReplaceItemsAsync(LuckyWheel wheel, List<LuckyWheelItemDto> items)
        {
            await _context.LuckyWheelItems
                .Where(i => i.LuckyWheelId == wheel.Id)
                .ExecuteDeleteAsync();

            wheel.Items.Clear();

            var orderedItems = items
                .OrderBy(i => i.DisplayOrder)
                .Select(MapItem)
                .ToList();

            foreach (var item in orderedItems)
            {
                wheel.Items.Add(item);
            }
        }

        private async Task ClearNotebookLinksAsync(LuckyWheel wheel)
        {
            foreach (var link in wheel.Notebooks.ToList())
            {
                _context.Entry(link).State = EntityState.Detached;
            }

            wheel.Notebooks.Clear();

            await _context.LuckyWheelNotebooks
                .Where(n => n.LuckyWheelId == wheel.Id)
                .ExecuteDeleteAsync();
        }

        private LuckyWheelResponseDto MapToResponseDto(LuckyWheel wheel)
        {
            var publishValidationErrors = GetPublishReadinessErrors(wheel);
            return new LuckyWheelResponseDto
            {
                Id = wheel.Id,
                Title = wheel.Title,
                Description = wheel.Description,
                Slug = wheel.Slug,
                Status = wheel.Status.ToString(),
                SaveToPhonebook = wheel.SaveToPhonebook,
                IsActive = wheel.IsActive,
                PublicUrl = BuildPublicUrl(wheel.Slug),
                IsReadyToPublish = publishValidationErrors.Count == 0,
                PublishValidationErrors = publishValidationErrors,
                NotebookIds = wheel.Notebooks.Select(n => n.ContactNotebookId).ToList(),
                Items = wheel.Items
                    .OrderBy(i => i.DisplayOrder)
                    .Select(MapItemToDto)
                    .ToList(),
                CreatedAt = EnsureUtc(wheel.CreatedAt),
                UpdatedAt = EnsureUtc(wheel.UpdatedAt),
                PublishedAt = EnsureUtc(wheel.PublishedAt)
            };
        }

        private static LuckyWheelItemDto MapItemToDto(LuckyWheelItem item)
        {
            return new LuckyWheelItemDto
            {
                Name = item.Name,
                Probability = item.Probability,
                DisplayOrder = item.DisplayOrder
            };
        }

        private static LuckyWheelItem MapItem(LuckyWheelItemDto dto)
        {
            return new LuckyWheelItem
            {
                Name = dto.Name.Trim(),
                Probability = Math.Round(dto.Probability, 2, MidpointRounding.AwayFromZero),
                DisplayOrder = dto.DisplayOrder
            };
        }

        private string? BuildPublicUrl(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(_luckyWheelOptions.PublicBaseUrl))
            {
                return null;
            }

            return $"{_luckyWheelOptions.PublicBaseUrl.TrimEnd('/')}/{slug}";
        }

        private static List<string> ValidateItemsForEditing(List<LuckyWheelItemDto> items)
        {
            var errors = new List<string>();

            if (items.Count > LuckyWheelConstants.MaxItems)
            {
                errors.Add($"حداکثر {LuckyWheelConstants.MaxItems} جایزه برای هر گردونه مجاز است");
                return errors;
            }

            var displayOrders = new HashSet<int>();

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add("نام جایزه الزامی است");
                }

                if (item.Probability <= 0 || item.Probability > 100)
                {
                    errors.Add($"درصد شانس جایزه «{item.Name}» باید بین 0.01 تا 100 باشد");
                }

                if (!displayOrders.Add(item.DisplayOrder))
                {
                    errors.Add($"ترتیب نمایش {item.DisplayOrder} تکراری است");
                }
            }

            return errors;
        }

        private static List<string> ValidateItemsForPublish(List<LuckyWheelItemDto> items)
        {
            var errors = ValidateItemsForEditing(items);
            if (errors.Count > 0)
            {
                return errors;
            }

            if (items.Count < LuckyWheelConstants.MinItems)
            {
                errors.Add($"حداقل {LuckyWheelConstants.MinItems} جایزه برای گردونه لازم است");
                return errors;
            }

            var totalProbability = items.Sum(i => Math.Round(i.Probability, 2, MidpointRounding.AwayFromZero));
            if (totalProbability != LuckyWheelConstants.RequiredProbabilityTotal)
            {
                errors.Add($"مجموع درصد شانس‌ها باید دقیقاً {LuckyWheelConstants.RequiredProbabilityTotal} باشد (فعلی: {totalProbability})");
            }

            return errors;
        }

        private List<string> GetPublishReadinessErrors(LuckyWheel wheel)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(wheel.Title))
            {
                errors.Add("عنوان گردونه الزامی است");
            }

            errors.AddRange(ValidateItemsForPublish(wheel.Items.Select(MapItemToDto).ToList()));

            if (wheel.SaveToPhonebook)
            {
                errors.AddRange(ValidatePhonebookSettings(
                    wheel.SaveToPhonebook,
                    wheel.Notebooks.Select(n => n.ContactNotebookId).ToList()));
            }

            return errors.Distinct().ToList();
        }

        private static List<string> ValidatePhonebookSettings(bool saveToPhonebook, IReadOnlyList<int> notebookIds)
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

        private async Task<string> GenerateUniqueSlugAsync(string title, int excludeWheelId)
        {
            var baseSlug = UserFormSlugHelper.SlugifyTitle(title);
            var existingSlugs = (await _luckyWheelRepository.GetExistingSlugsWithPrefixAsync(baseSlug, excludeWheelId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var suffix = 0; suffix <= LuckyWheelConstants.SlugGenerationMaxAttempts; suffix++)
            {
                var candidate = UserFormSlugHelper.BuildCandidateSlug(baseSlug, suffix);
                if (!existingSlugs.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseSlug}-{Guid.NewGuid():N}"[..LuckyWheelConstants.MaxSlugLength].Trim('-');
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

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
