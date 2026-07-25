using Api_Vapp.Constants;
using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت کارت ویزیت دیجیتال — معماری مشابه فرم‌ساز
    /// </summary>
    public class BusinessCardService : IBusinessCardService
    {
        private readonly IBusinessCardRepository _businessCardRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly BusinessCardOptions _options;
        private readonly IFileUploadService _fileUploadService;
        private readonly IAuditService _audit;
        private readonly ILogger<BusinessCardService> _logger;

        public BusinessCardService(
            IBusinessCardRepository businessCardRepository,
            Api_Vapp.Data.Api_Context context,
            IOptions<BusinessCardOptions> options,
            IFileUploadService fileUploadService,
            IAuditService audit,
            ILogger<BusinessCardService> logger)
        {
            _businessCardRepository = businessCardRepository;
            _context = context;
            _options = options.Value;
            _fileUploadService = fileUploadService;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<BusinessCardResponseDto>> CreateDraftAsync(int userId, CreateBusinessCardDto createDto)
        {
            try
            {
                var sectionErrors = ValidateSectionsPayload(
                    createDto.SliderImages,
                    createDto.ServiceItems,
                    createDto.ContactEmail);
                if (sectionErrors.Count > 0)
                {
                    return ApiResponse<BusinessCardResponseDto>.BadRequest(
                        "داده‌های بخش‌های کارت نامعتبر است",
                        sectionErrors,
                        ErrorCodes.ValidationFailed);
                }

                string? slug = null;
                if (!string.IsNullOrWhiteSpace(createDto.Slug))
                {
                    var slugValidation = await ValidateSlugAsync(createDto.Slug, excludeCardId: null);
                    if (slugValidation.Error != null)
                    {
                        return slugValidation.Error;
                    }

                    slug = slugValidation.NormalizedSlug;
                }

                var card = new BusinessCard
                {
                    UserId = userId,
                    Title = createDto.Title?.Trim() ?? string.Empty,
                    LogoUrl = NormalizeOptionalText(createDto.LogoUrl),
                    Slug = slug,
                    TemplateKey = NormalizeOptionalText(createDto.TemplateKey),
                    Status = BusinessCardStatus.Draft,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    SliderEnabled = createDto.SliderEnabled ?? false,
                    DescriptionEnabled = createDto.DescriptionEnabled ?? true,
                    ServicesEnabled = createDto.ServicesEnabled ?? false,
                    MapEnabled = createDto.MapEnabled ?? false,
                    ContactEnabled = createDto.ContactEnabled ?? true,
                    DescriptionTitle = NormalizeOptionalText(createDto.DescriptionTitle),
                    DescriptionText = NormalizeOptionalText(createDto.DescriptionText),
                    MapLatitude = createDto.MapLatitude,
                    MapLongitude = createDto.MapLongitude,
                    MapAddress = NormalizeOptionalText(createDto.MapAddress),
                    ContactPhone = NormalizeOptionalText(createDto.ContactPhone),
                    ContactEmail = NormalizeOptionalText(createDto.ContactEmail),
                    ContactInstagram = NormalizeOptionalText(createDto.ContactInstagram)
                };

                ApplySliderImages(card, createDto.SliderImages);
                ApplyServiceItems(card, createDto.ServiceItems);

                await _context.BusinessCards.AddAsync(card);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Business card draft created with ID {CardId} for user {UserId}", card.Id, userId);

                return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                    MapToResponseDto(card),
                    "پیش‌نویس کارت ویزیت با موفقیت ایجاد شد",
                    201);
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<BusinessCardResponseDto>(dbEx, "creating business card draft", userId: userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating business card draft for user {UserId}", userId);
                return ApiResponse<BusinessCardResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BusinessCardResponseDto>> UpdateInfoAsync(int id, int userId, UpdateBusinessCardInfoDto? updateDto)
        {
            try
            {
                if (updateDto == null || !HasAnyInfoChanges(updateDto))
                {
                    return ApiResponse<BusinessCardResponseDto>.BadRequest(
                        "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var cardResult = await GetTrackedCardForUserAsync(id, userId);
                if (cardResult.Error != null)
                {
                    return cardResult.Error;
                }

                var card = cardResult.Card!;

                if (!string.IsNullOrWhiteSpace(updateDto.Slug))
                {
                    var slugValidation = await ValidateSlugAsync(updateDto.Slug, id);
                    if (slugValidation.Error != null)
                    {
                        return slugValidation.Error;
                    }

                    card.Slug = slugValidation.NormalizedSlug;
                }

                if (updateDto.Title != null)
                {
                    if (string.IsNullOrWhiteSpace(updateDto.Title))
                    {
                        return ApiResponse<BusinessCardResponseDto>.BadRequest(
                            "نام کسب‌وکار نمی‌تواند خالی باشد",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    card.Title = updateDto.Title.Trim();
                }

                if (updateDto.ClearLogo == true)
                {
                    card.LogoUrl = null;
                }
                else if (updateDto.LogoUrl != null)
                {
                    card.LogoUrl = NormalizeOptionalText(updateDto.LogoUrl);
                }

                card.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Business card info updated — CardId: {CardId}", id);

                return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                    MapToResponseDto(card),
                    "اطلاعات کارت ویزیت با موفقیت به‌روزرسانی شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<BusinessCardResponseDto>(dbEx, "updating business card info", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating business card info {CardId} for user {UserId}", id, userId);
                return ApiResponse<BusinessCardResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BusinessCardResponseDto>> UpdateSectionsAsync(int id, int userId, UpdateBusinessCardSectionsDto? updateDto)
        {
            try
            {
                if (updateDto == null || !HasAnySectionChanges(updateDto))
                {
                    return ApiResponse<BusinessCardResponseDto>.BadRequest(
                        "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var sectionErrors = ValidateSectionsPayload(
                    updateDto.SliderImages,
                    updateDto.ServiceItems,
                    updateDto.ContactEmail);
                if (sectionErrors.Count > 0)
                {
                    return ApiResponse<BusinessCardResponseDto>.BadRequest(
                        "داده‌های بخش‌های کارت نامعتبر است",
                        sectionErrors,
                        ErrorCodes.ValidationFailed);
                }

                var cardResult = await GetTrackedCardForUserAsync(id, userId);
                if (cardResult.Error != null)
                {
                    return cardResult.Error;
                }

                var card = cardResult.Card!;

                if (updateDto.SliderEnabled.HasValue)
                    card.SliderEnabled = updateDto.SliderEnabled.Value;
                if (updateDto.DescriptionEnabled.HasValue)
                    card.DescriptionEnabled = updateDto.DescriptionEnabled.Value;
                if (updateDto.ServicesEnabled.HasValue)
                    card.ServicesEnabled = updateDto.ServicesEnabled.Value;
                if (updateDto.MapEnabled.HasValue)
                    card.MapEnabled = updateDto.MapEnabled.Value;
                if (updateDto.ContactEnabled.HasValue)
                    card.ContactEnabled = updateDto.ContactEnabled.Value;

                if (updateDto.DescriptionTitle != null)
                    card.DescriptionTitle = NormalizeOptionalText(updateDto.DescriptionTitle);
                if (updateDto.DescriptionText != null)
                    card.DescriptionText = NormalizeOptionalText(updateDto.DescriptionText);

                if (updateDto.MapLatitude.HasValue)
                    card.MapLatitude = updateDto.MapLatitude;
                if (updateDto.MapLongitude.HasValue)
                    card.MapLongitude = updateDto.MapLongitude;
                if (updateDto.MapAddress != null)
                    card.MapAddress = NormalizeOptionalText(updateDto.MapAddress);

                if (updateDto.ContactPhone != null)
                    card.ContactPhone = NormalizeOptionalText(updateDto.ContactPhone);
                if (updateDto.ContactEmail != null)
                    card.ContactEmail = NormalizeOptionalText(updateDto.ContactEmail);
                if (updateDto.ContactInstagram != null)
                    card.ContactInstagram = NormalizeOptionalText(updateDto.ContactInstagram);

                if (updateDto.SliderImages != null)
                {
                    _context.BusinessCardSliderImages.RemoveRange(card.SliderImages);
                    card.SliderImages.Clear();
                    ApplySliderImages(card, updateDto.SliderImages);
                }

                if (updateDto.ServiceItems != null)
                {
                    _context.BusinessCardServiceItems.RemoveRange(card.ServiceItems);
                    card.ServiceItems.Clear();
                    ApplyServiceItems(card, updateDto.ServiceItems);
                }

                card.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Business card sections updated — CardId: {CardId}", id);

                return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                    MapToResponseDto(card),
                    "بخش‌های کارت ویزیت با موفقیت به‌روزرسانی شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<BusinessCardResponseDto>(dbEx, "updating business card sections", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating business card sections {CardId} for user {UserId}", id, userId);
                return ApiResponse<BusinessCardResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BusinessCardResponseDto>> PublishAsync(int id, int userId, PublishBusinessCardDto? publishDto = null)
        {
            try
            {
                var card = await _businessCardRepository.GetByIdWithDetailsTrackedAsync(id);
                if (card == null)
                {
                    return ApiResponse<BusinessCardResponseDto>.NotFound("کارت ویزیت یافت نشد");
                }

                if (card.UserId != userId)
                {
                    return ApiResponse<BusinessCardResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                if (card.Status == BusinessCardStatus.Published)
                {
                    return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                        MapToResponseDto(card),
                        "کارت ویزیت قبلاً منتشر شده است");
                }

                var previousStatus = card.Status;
                var publishError = await ValidateAndApplyPublishAsync(card, id, publishDto);
                if (publishError != null)
                {
                    return publishError;
                }

                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.BusinessCard,
                    Action = AuditActions.BusinessCardStatusChanged,
                    EntityType = AuditEntityTypes.BusinessCard,
                    EntityId = card.Id.ToString(),
                    ActorUserId = userId,
                    Before = new { status = previousStatus.ToString() },
                    After = new { status = card.Status.ToString(), isActive = card.IsActive, slug = card.Slug }
                });

                _logger.LogInformation("Business card {CardId} published with slug {Slug}", id, card.Slug);

                return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                    MapToResponseDto(card),
                    "کارت ویزیت با موفقیت منتشر شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<BusinessCardResponseDto>(dbEx, "publishing business card", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing business card {CardId} for user {UserId}", id, userId);
                return ApiResponse<BusinessCardResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BusinessCardListResponseDto>> GetCardsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1)
                {
                    return ApiResponse<BusinessCardListResponseDto>.BadRequest(
                        "شماره صفحه باید بزرگتر از صفر باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (pageSize < 1 || pageSize > 100)
                {
                    return ApiResponse<BusinessCardListResponseDto>.BadRequest(
                        "تعداد در هر صفحه باید بین 1 تا 100 باشد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var (items, totalCount) = await _businessCardRepository.GetByUserIdPagedAsync(userId, pageNumber, pageSize);

                var summaries = items.Select(card => new BusinessCardSummaryDto
                {
                    Id = card.Id,
                    Title = card.Title,
                    LogoUrl = card.LogoUrl,
                    Slug = card.Slug,
                    Status = card.Status.ToString(),
                    IsActive = GetEffectiveIsActive(card),
                    PublicUrl = BuildPublicUrl(card.Slug),
                    CreatedAt = EnsureUtc(card.CreatedAt),
                    PublishedAt = EnsureUtc(card.PublishedAt)
                }).ToList();

                return ApiResponse<BusinessCardListResponseDto>.CreateSuccess(new BusinessCardListResponseDto
                {
                    Cards = PagedResponse<BusinessCardSummaryDto>.Create(summaries, totalCount, pageNumber, pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing business cards for user {UserId}", userId);
                return ApiResponse<BusinessCardListResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BusinessCardResponseDto>> GetByIdAsync(int id, int userId)
        {
            try
            {
                var card = await _businessCardRepository.GetByIdWithDetailsReadOnlyAsync(id);
                if (card == null)
                {
                    return ApiResponse<BusinessCardResponseDto>.NotFound("کارت ویزیت یافت نشد");
                }

                if (card.UserId != userId)
                {
                    return ApiResponse<BusinessCardResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                return ApiResponse<BusinessCardResponseDto>.CreateSuccess(MapToResponseDto(card));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting business card {CardId} for user {UserId}", id, userId);
                return ApiResponse<BusinessCardResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id, int userId)
        {
            try
            {
                var card = await _businessCardRepository.GetOwnedCardAsync(id, userId, tracked: true);
                if (card == null)
                {
                    return ApiResponse<bool>.NotFound("کارت ویزیت یافت نشد");
                }

                card.IsDeleted = true;
                card.Slug = null;
                card.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                try
                {
                    var deletedFiles = await _fileUploadService.DeleteAllEntityFilesAsync(
                        FileUploadConstants.EntityType_BusinessCard,
                        id);

                    if (deletedFiles > 0)
                    {
                        _logger.LogInformation(
                            "Deleted {Count} file(s) for business card {CardId}",
                            deletedFiles,
                            id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting files for business card {CardId}", id);
                }

                _logger.LogInformation("Business card {CardId} soft-deleted for user {UserId}", id, userId);

                return ApiResponse<bool>.CreateSuccess(true, "کارت ویزیت با موفقیت حذف شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<bool>(dbEx, "deleting business card", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting business card {CardId} for user {UserId}", id, userId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BusinessCardResponseDto>> SetActiveStatusAsync(int id, int userId, bool isActive)
        {
            try
            {
                var card = await _businessCardRepository.GetByIdWithDetailsTrackedAsync(id);
                if (card == null)
                {
                    return ApiResponse<BusinessCardResponseDto>.NotFound("کارت ویزیت یافت نشد");
                }

                if (card.UserId != userId)
                {
                    return ApiResponse<BusinessCardResponseDto>.Forbidden(
                        ControlledErrorHelper.Unauthorized,
                        ErrorCodes.Forbidden);
                }

                if (card.Status != BusinessCardStatus.Published)
                {
                    return ApiResponse<BusinessCardResponseDto>.BadRequest(
                        "فقط کارت‌های منتشرشده قابل فعال/غیرفعال کردن هستند",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (card.IsActive == isActive)
                {
                    return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                        MapToResponseDto(card),
                        isActive ? "کارت ویزیت از قبل فعال است" : "کارت ویزیت از قبل غیرفعال است");
                }

                var previousIsActive = card.IsActive;
                card.IsActive = isActive;
                card.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.BusinessCard,
                    Action = AuditActions.BusinessCardStatusChanged,
                    EntityType = AuditEntityTypes.BusinessCard,
                    EntityId = card.Id.ToString(),
                    ActorUserId = userId,
                    Before = new { isActive = previousIsActive },
                    After = new { isActive = card.IsActive, status = card.Status.ToString(), slug = card.Slug }
                });

                return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                    MapToResponseDto(card),
                    isActive ? "کارت ویزیت فعال شد" : "کارت ویزیت غیرفعال شد");
            }
            catch (DbUpdateException dbEx)
            {
                return MapDbUpdateException<BusinessCardResponseDto>(dbEx, "setting business card active status", id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting active status for business card {CardId} for user {UserId}", id, userId);
                return ApiResponse<BusinessCardResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<BusinessCardResponseDto>?> ValidateAndApplyPublishAsync(
            BusinessCard card,
            int cardId,
            PublishBusinessCardDto? publishDto)
        {
            if (string.IsNullOrWhiteSpace(card.Title))
            {
                return ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "نام کسب‌وکار الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (!HasAtLeastOneActiveSection(card))
            {
                return ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "حداقل یک بخش فعال برای انتشار لازم است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            string slug;
            if (!string.IsNullOrWhiteSpace(publishDto?.Slug))
            {
                var slugValidation = await ValidateSlugAsync(publishDto.Slug, cardId);
                if (slugValidation.Error != null)
                {
                    return slugValidation.Error;
                }

                slug = slugValidation.NormalizedSlug!;
            }
            else if (!string.IsNullOrWhiteSpace(card.Slug))
            {
                slug = card.Slug;
            }
            else
            {
                slug = await GenerateUniqueSlugAsync(card.Title, cardId);
            }

            card.Slug = slug;
            card.Status = BusinessCardStatus.Published;
            card.IsActive = true;
            card.PublishedAt = DateTime.UtcNow;
            card.UpdatedAt = DateTime.UtcNow;
            return null;
        }

        private static bool HasAtLeastOneActiveSection(BusinessCard card)
        {
            return card.SliderEnabled
                   || card.DescriptionEnabled
                   || card.ServicesEnabled
                   || card.MapEnabled
                   || card.ContactEnabled;
        }

        private async Task<(BusinessCard? Card, ApiResponse<BusinessCardResponseDto>? Error)> GetTrackedCardForUserAsync(
            int id,
            int userId)
        {
            var card = await _businessCardRepository.GetByIdWithDetailsTrackedForUserAsync(id, userId);
            if (card != null)
            {
                return (card, null);
            }

            var anyCard = await _businessCardRepository.GetByIdAsync(id);
            if (anyCard == null)
            {
                return (null, ApiResponse<BusinessCardResponseDto>.NotFound("کارت ویزیت یافت نشد"));
            }

            return (null, ApiResponse<BusinessCardResponseDto>.Forbidden(
                ControlledErrorHelper.Unauthorized,
                ErrorCodes.Forbidden));
        }

        private async Task<(string? NormalizedSlug, ApiResponse<BusinessCardResponseDto>? Error)> ValidateSlugAsync(
            string slug,
            int? excludeCardId)
        {
            var normalized = BusinessCardSlugHelper.Normalize(slug);
            if (normalized == null)
            {
                return (null, ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "slug فقط می‌تواند شامل حروف انگلیسی کوچک، عدد و خط تیره باشد",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            if (await _businessCardRepository.SlugExistsAsync(normalized, excludeCardId))
            {
                return (null, ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "این لینک قبلاً استفاده شده است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            return (normalized, null);
        }

        private async Task<string> GenerateUniqueSlugAsync(string title, int excludeCardId)
        {
            var baseSlug = BusinessCardSlugHelper.SlugifyTitle(title);
            var existing = await _businessCardRepository.GetExistingSlugsWithPrefixAsync(baseSlug, excludeCardId);
            var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < BusinessCardConstants.SlugGenerationMaxAttempts; i++)
            {
                var candidate = BusinessCardSlugHelper.BuildCandidateSlug(baseSlug, i);
                if (!existingSet.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(BusinessCardConstants.MaxSlugLength, 32)];
        }

        private static void ApplySliderImages(BusinessCard card, List<BusinessCardSliderImageDto> images)
        {
            var order = 0;
            foreach (var image in images.OrderBy(i => i.DisplayOrder))
            {
                var url = NormalizeOptionalText(image.ImageUrl);
                if (url == null)
                {
                    continue;
                }

                card.SliderImages.Add(new BusinessCardSliderImage
                {
                    ImageUrl = url,
                    DisplayOrder = order++
                });
            }
        }

        private static void ApplyServiceItems(BusinessCard card, List<BusinessCardServiceItemDto> items)
        {
            var order = 0;
            foreach (var item in items.OrderBy(i => i.DisplayOrder))
            {
                card.ServiceItems.Add(new BusinessCardServiceItem
                {
                    Title = item.Title.Trim(),
                    Price = item.Price,
                    ImageUrl = NormalizeOptionalText(item.ImageUrl),
                    DisplayOrder = order++
                });
            }
        }

        private static List<string> ValidateSectionsPayload(
            List<BusinessCardSliderImageDto>? sliderImages,
            List<BusinessCardServiceItemDto>? serviceItems,
            string? contactEmail)
        {
            var errors = new List<string>();

            if (sliderImages != null && sliderImages.Count > BusinessCardConstants.MaxSliderImages)
            {
                errors.Add($"حداکثر {BusinessCardConstants.MaxSliderImages} تصویر در اسلایدر مجاز است");
            }

            if (sliderImages != null)
            {
                foreach (var image in sliderImages)
                {
                    if (string.IsNullOrWhiteSpace(image.ImageUrl))
                    {
                        errors.Add("آدرس تصویر اسلایدر الزامی است");
                        break;
                    }
                }
            }

            if (serviceItems != null && serviceItems.Count > BusinessCardConstants.MaxServiceItems)
            {
                errors.Add($"حداکثر {BusinessCardConstants.MaxServiceItems} تعرفه مجاز است");
            }

            if (serviceItems != null)
            {
                foreach (var item in serviceItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Title))
                    {
                        errors.Add("عنوان تعرفه الزامی است");
                    }

                    if (item.Price < 0)
                    {
                        errors.Add("مبلغ تعرفه نمی‌تواند منفی باشد");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(contactEmail) &&
                !contactEmail.Contains('@', StringComparison.Ordinal))
            {
                errors.Add("فرمت ایمیل نامعتبر است");
            }

            return errors;
        }

        private static bool HasAnyInfoChanges(UpdateBusinessCardInfoDto dto)
        {
            return dto.Title != null
                   || dto.LogoUrl != null
                   || dto.ClearLogo == true
                   || dto.Slug != null;
        }

        private static bool HasAnySectionChanges(UpdateBusinessCardSectionsDto dto)
        {
            return dto.SliderEnabled.HasValue
                   || dto.DescriptionEnabled.HasValue
                   || dto.ServicesEnabled.HasValue
                   || dto.MapEnabled.HasValue
                   || dto.ContactEnabled.HasValue
                   || dto.DescriptionTitle != null
                   || dto.DescriptionText != null
                   || dto.MapLatitude.HasValue
                   || dto.MapLongitude.HasValue
                   || dto.MapAddress != null
                   || dto.ContactPhone != null
                   || dto.ContactEmail != null
                   || dto.ContactInstagram != null
                   || dto.SliderImages != null
                   || dto.ServiceItems != null;
        }

        private static bool GetEffectiveIsActive(BusinessCard card)
        {
            return card.Status == BusinessCardStatus.Published && card.IsActive;
        }

        private BusinessCardResponseDto MapToResponseDto(BusinessCard card)
        {
            return new BusinessCardResponseDto
            {
                Id = card.Id,
                Title = card.Title,
                LogoUrl = card.LogoUrl,
                Slug = card.Slug,
                TemplateKey = card.TemplateKey,
                TemplateId = card.TemplateId,
                Status = card.Status.ToString(),
                IsActive = GetEffectiveIsActive(card),
                PublicUrl = BuildPublicUrl(card.Slug),
                SliderEnabled = card.SliderEnabled,
                DescriptionEnabled = card.DescriptionEnabled,
                ServicesEnabled = card.ServicesEnabled,
                MapEnabled = card.MapEnabled,
                ContactEnabled = card.ContactEnabled,
                DescriptionTitle = card.DescriptionTitle,
                DescriptionText = card.DescriptionText,
                MapLatitude = card.MapLatitude,
                MapLongitude = card.MapLongitude,
                MapAddress = card.MapAddress,
                ContactPhone = card.ContactPhone,
                ContactEmail = card.ContactEmail,
                ContactInstagram = card.ContactInstagram,
                SliderImages = card.SliderImages
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new BusinessCardSliderImageDto
                    {
                        ImageUrl = i.ImageUrl,
                        DisplayOrder = i.DisplayOrder
                    })
                    .ToList(),
                ServiceItems = card.ServiceItems
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new BusinessCardServiceItemDto
                    {
                        Id = i.Id,
                        Title = i.Title,
                        Price = i.Price,
                        ImageUrl = i.ImageUrl,
                        DisplayOrder = i.DisplayOrder
                    })
                    .ToList(),
                CreatedAt = EnsureUtc(card.CreatedAt),
                UpdatedAt = EnsureUtc(card.UpdatedAt),
                PublishedAt = EnsureUtc(card.PublishedAt)
            };
        }

        private string? BuildPublicUrl(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            {
                return null;
            }

            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{slug}";
        }

        private static string? NormalizeOptionalText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
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

        private ApiResponse<T> MapDbUpdateException<T>(
            DbUpdateException dbEx,
            string operation,
            int? cardId = null,
            int? userId = null)
        {
            if (IsUniqueConstraintViolation(dbEx))
            {
                _logger.LogWarning(
                    dbEx,
                    "Unique constraint violation while {Operation} — CardId: {CardId}, UserId: {UserId}",
                    operation,
                    cardId,
                    userId);

                return ApiResponse<T>.BadRequest(
                    "اطلاعات ارسالی با داده‌های موجود تداخل دارد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            _logger.LogError(
                dbEx,
                "Database error while {Operation} — CardId: {CardId}, UserId: {UserId}",
                operation,
                cardId,
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
