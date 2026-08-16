using Api_Vapp.Constants;
using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت کارت ویزیت دیجیتال — معماری مشابه فرم‌ساز
    /// </summary>
    public class BusinessCardService : IBusinessCardService
    {
        private readonly IBusinessCardRepository _businessCardRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly IMessageService _messageService;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly BusinessCardOptions _options;
        private readonly IFileUploadService _fileUploadService;
        private readonly IAuditService _audit;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BusinessCardService> _logger;

        public BusinessCardService(
            IBusinessCardRepository businessCardRepository,
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            IMessageService messageService,
            Api_Vapp.Data.Api_Context context,
            IOptions<BusinessCardOptions> options,
            IFileUploadService fileUploadService,
            IAuditService audit,
            IMemoryCache cache,
            ILogger<BusinessCardService> logger)
        {
            _businessCardRepository = businessCardRepository;
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _messageService = messageService;
            _context = context;
            _options = options.Value;
            _fileUploadService = fileUploadService;
            _audit = audit;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiResponse<BusinessCardResponseDto>> CreateDraftAsync(int userId, CreateBusinessCardDto createDto)
        {
            try
            {
                _logger.LogInformation("شروع ایجاد پیش‌نویس کارت ویزیت — UserId: {UserId}", userId);

                var sectionErrors = ValidateSectionsPayload(
                    createDto.SliderImages,
                    createDto.ServiceItems,
                    createDto.SocialLinks,
                    createDto.ContactEmail,
                    createDto.BankAccountNumber,
                    createDto.BankCardNumber,
                    createDto.BankShebaNumber);
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
                    LogoUrl = NormalizeStoredFilePath(createDto.LogoUrl),
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
                    BankingEnabled = createDto.BankingEnabled ?? false,
                    DescriptionTitle = NormalizeOptionalText(createDto.DescriptionTitle),
                    DescriptionText = NormalizeOptionalText(createDto.DescriptionText),
                    MapLatitude = createDto.MapLatitude,
                    MapLongitude = createDto.MapLongitude,
                    MapAddress = NormalizeOptionalText(createDto.MapAddress),
                    ContactPhone = NormalizeOptionalText(createDto.ContactPhone),
                    ContactEmail = NormalizeOptionalText(createDto.ContactEmail),
                    ContactInstagram = NormalizeOptionalText(createDto.ContactInstagram)
                };

                ApplyBankingFields(
                    card,
                    createDto.BankAccountNumber,
                    createDto.BankCardNumber,
                    createDto.BankShebaNumber,
                    applyAccount: true,
                    applyCard: true,
                    applySheba: true);

                ApplySliderImages(card, createDto.SliderImages);
                ApplyServiceItems(card, createDto.ServiceItems);

                if (createDto.SocialLinks.Count > 0)
                {
                    ApplySocialLinks(card, createDto.SocialLinks);
                }
                else if (!string.IsNullOrWhiteSpace(card.ContactInstagram))
                {
                    ApplySocialLinks(card, new List<BusinessCardSocialLinkDto>
                    {
                        new()
                        {
                            NetworkType = "instagram",
                            Value = card.ContactInstagram,
                            DisplayOrder = 0
                        }
                    });
                }

                await _context.BusinessCards.AddAsync(card);
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.BusinessCard,
                    Action = AuditActions.BusinessCardCreated,
                    EntityType = AuditEntityTypes.BusinessCard,
                    EntityId = card.Id.ToString(),
                    ActorUserId = userId,
                    After = new { title = card.Title, templateKey = card.TemplateKey, status = card.Status.ToString() }
                });

                _logger.LogInformation("پایان ایجاد پیش‌نویس کارت ویزیت — CardId: {CardId}, UserId: {UserId}", card.Id, userId);

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
                _logger.LogInformation("شروع به‌روزرسانی اطلاعات کارت — CardId: {CardId}, UserId: {UserId}", id, userId);

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
                    if (!string.IsNullOrWhiteSpace(card.LogoUrl))
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(
                                card.LogoUrl,
                                FileUploadConstants.EntityType_BusinessCard,
                                id,
                                FileUploadConstants.SubFolder_Logo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "خطا در حذف فایل لوگوی کارت {CardId}", id);
                        }
                    }

                    card.LogoUrl = null;
                }
                else if (updateDto.LogoUrl != null)
                {
                    card.LogoUrl = NormalizeStoredFilePath(updateDto.LogoUrl);
                }

                card.UpdatedAt = DateTime.UtcNow;
                QuickSendContentApprovalHelper.ResetToPending(card);
                await _context.SaveChangesAsync();

                BusinessCardPublicService.InvalidatePublicCache(_cache, card.Slug);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.BusinessCard,
                    Action = AuditActions.BusinessCardUpdated,
                    EntityType = AuditEntityTypes.BusinessCard,
                    EntityId = card.Id.ToString(),
                    ActorUserId = userId,
                    After = new { title = card.Title, slug = card.Slug, logoUrl = card.LogoUrl }
                });

                _logger.LogInformation("پایان به‌روزرسانی اطلاعات کارت — CardId: {CardId}", id);

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
                _logger.LogInformation("شروع به‌روزرسانی بخش‌های کارت — CardId: {CardId}, UserId: {UserId}", id, userId);

                if (updateDto == null || !HasAnySectionChanges(updateDto))
                {
                    return ApiResponse<BusinessCardResponseDto>.BadRequest(
                        "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var sectionErrors = ValidateSectionsPayload(
                    updateDto.SliderImages,
                    updateDto.ServiceItems,
                    updateDto.SocialLinks,
                    updateDto.ContactEmail,
                    updateDto.BankAccountNumber,
                    updateDto.BankCardNumber,
                    updateDto.BankShebaNumber);
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
                if (updateDto.BankingEnabled.HasValue)
                    card.BankingEnabled = updateDto.BankingEnabled.Value;

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

                ApplyBankingFields(
                    card,
                    updateDto.BankAccountNumber,
                    updateDto.BankCardNumber,
                    updateDto.BankShebaNumber,
                    applyAccount: updateDto.BankAccountNumber != null,
                    applyCard: updateDto.BankCardNumber != null,
                    applySheba: updateDto.BankShebaNumber != null);

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

                if (updateDto.SocialLinks != null)
                {
                    _context.BusinessCardSocialLinks.RemoveRange(card.SocialLinks);
                    card.SocialLinks.Clear();
                    ApplySocialLinks(card, updateDto.SocialLinks);
                }
                else if (updateDto.ContactInstagram != null
                         && card.SocialLinks.Count == 0
                         && !string.IsNullOrWhiteSpace(card.ContactInstagram))
                {
                    // سازگاری با کلاینت قدیمی که فقط contactInstagram می‌فرستد
                    ApplySocialLinks(card, new List<BusinessCardSocialLinkDto>
                    {
                        new()
                        {
                            NetworkType = "instagram",
                            Value = card.ContactInstagram,
                            DisplayOrder = 0
                        }
                    });
                }

                card.UpdatedAt = DateTime.UtcNow;
                QuickSendContentApprovalHelper.ResetToPending(card);
                await _context.SaveChangesAsync();

                BusinessCardPublicService.InvalidatePublicCache(_cache, card.Slug);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.BusinessCard,
                    Action = AuditActions.BusinessCardUpdated,
                    EntityType = AuditEntityTypes.BusinessCard,
                    EntityId = card.Id.ToString(),
                    ActorUserId = userId,
                    After = new
                    {
                        sliderEnabled = card.SliderEnabled,
                        descriptionEnabled = card.DescriptionEnabled,
                        servicesEnabled = card.ServicesEnabled,
                        mapEnabled = card.MapEnabled,
                        contactEnabled = card.ContactEnabled
                    }
                });

                _logger.LogInformation("پایان به‌روزرسانی بخش‌های کارت — CardId: {CardId}", id);

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
                _logger.LogInformation("شروع انتشار کارت ویزیت — CardId: {CardId}, UserId: {UserId}", id, userId);

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

                QuickSendContentApprovalHelper.ResetToPending(card);
                await _context.SaveChangesAsync();

                BusinessCardPublicService.InvalidatePublicCache(_cache, card.Slug);

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

                _logger.LogInformation("پایان انتشار کارت ویزیت — CardId: {CardId}, Slug: {Slug}", id, card.Slug);

                return ApiResponse<BusinessCardResponseDto>.CreateSuccess(
                    MapToResponseDto(card),
                    QuickSendContentApprovalHelper.BuildPublishSubmittedMessage("کارت ویزیت"));
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

        public async Task<ApiResponse<BusinessCardListResponseDto>> GetCardsAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null)
        {
            try
            {
                _logger.LogInformation("شروع لیست کارت‌های ویزیت — UserId: {UserId}, Page: {PageNumber}", userId, pageNumber);

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

                var (items, totalCount) = await _businessCardRepository.GetByUserIdPagedAsync(userId, pageNumber, pageSize, isActive);

                var summaries = items.Select(card => new BusinessCardSummaryDto
                {
                    Id = card.Id,
                    Title = card.Title,
                    LogoUrl = ToPublicFileUrl(card.LogoUrl),
                    Slug = card.Slug,
                    Status = card.Status.ToString(),
                    IsActive = GetEffectiveIsActive(card),
                    PublicUrl = BuildPublicUrl(card.Slug),
                    CreatedAt = EnsureUtc(card.CreatedAt),
                    PublishedAt = EnsureUtc(card.PublishedAt),
                    ApprovalStatus = card.ApprovalStatus,
                    RejectionReason = card.RejectionReason,
                    ApprovedAt = EnsureUtc(card.ApprovedAt)
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
                _logger.LogInformation("شروع حذف کارت ویزیت — CardId: {CardId}, UserId: {UserId}", id, userId);

                var card = await _businessCardRepository.GetOwnedCardAsync(id, userId, tracked: true);
                if (card == null)
                {
                    return ApiResponse<bool>.NotFound("کارت ویزیت یافت نشد");
                }

                var previousSlug = card.Slug;
                card.IsDeleted = true;
                card.Slug = null;
                card.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                BusinessCardPublicService.InvalidatePublicCache(_cache, previousSlug);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.BusinessCard,
                    Action = AuditActions.BusinessCardDeleted,
                    EntityType = AuditEntityTypes.BusinessCard,
                    EntityId = id.ToString(),
                    ActorUserId = userId,
                    Before = new { title = card.Title, slug = previousSlug }
                });

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

                _logger.LogInformation("پایان حذف کارت ویزیت — CardId: {CardId}, UserId: {UserId}", id, userId);

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

                BusinessCardPublicService.InvalidatePublicCache(_cache, card.Slug);

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

        public async Task<ApiResponse<string>> UploadImageAsync(int id, int userId, IFormFile imageFile, string? imageType = null)
        {
            try
            {
                var validationError = SecureFileValidator.ValidateImage(
                    imageFile,
                    SecureFileValidator.ContactImageMaxBytes,
                    "۱۰ مگابایت");
                if (validationError != null)
                {
                    return ApiResponse<string>.BadRequest(validationError, errorCode: ErrorCodes.ValidationFailed);
                }

                var imageSubFolder = ResolveImageSubFolder(imageType);
                if (imageSubFolder == null)
                {
                    return ApiResponse<string>.BadRequest(
                        "imageType نامعتبر است (مقادیر مجاز: logo, slider, service, image)",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var isLogoUpload = string.Equals(
                    imageSubFolder,
                    FileUploadConstants.SubFolder_Logo,
                    StringComparison.Ordinal);

                if (isLogoUpload)
                {
                    return await UploadAndPersistLogoAsync(id, userId, imageFile);
                }

                var ownershipError = await EnsureCardOwnedAsync(id, userId);
                if (ownershipError != null)
                {
                    return ownershipError;
                }

                var relativePath = await _fileUploadService.UploadFileAsync(
                    imageFile,
                    FileUploadConstants.EntityType_BusinessCard,
                    id,
                    imageSubFolder);

                var imageUrl = _fileUploadService.GetFileUrl(relativePath);
                _logger.LogInformation(
                    "تصویر کارت ویزیت آپلود شد — CardId: {CardId}, SubFolder: {SubFolder}",
                    id,
                    imageSubFolder);

                return ApiResponse<string>.CreateSuccess(imageUrl, "تصویر با موفقیت آپلود شد");
            }
            catch (ArgumentException ex)
            {
                return ApiResponse<string>.BadRequest(
                    ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed),
                    errorCode: ErrorCodes.ValidationFailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading business card image for card {CardId} and user {UserId}", id, userId);
                return ApiResponse<string>.InternalServerError(ControlledErrorHelper.FileUploadFailed);
            }
        }

        /// <summary>
        /// ارسال سریع لینک عمومی کارت ویزیت به یک مخاطب (SMS) — الگوی مشابه SocialMediaLink
        /// </summary>
        public async Task<ApiResponse<DirectSendResultDto>> QuickSendBusinessCardAsync(
            int userId,
            QuickSendBusinessCardDto quickSendDto)
        {
            _logger.LogInformation(
                "ارسال سریع کارت ویزیت — UserId: {UserId}, ContactId: {ContactId}, BusinessCardId: {BusinessCardId}",
                userId,
                quickSendDto.ContactId,
                quickSendDto.BusinessCardId);

            try
            {
                if (quickSendDto.ContactId <= 0 || quickSendDto.BusinessCardId <= 0)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "شناسه مخاطب و کارت ویزیت الزامی است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var contact = await _contactRepository.GetByIdAsync(quickSendDto.ContactId);
                if (contact == null || contact.IsDeleted)
                    return ApiResponse<DirectSendResultDto>.NotFound("مخاطب یافت نشد");

                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                    return ApiResponse<DirectSendResultDto>.Forbidden("مخاطب متعلق به شما نیست");

                var card = await _businessCardRepository.GetOwnedCardAsync(
                    quickSendDto.BusinessCardId,
                    userId,
                    tracked: false);

                if (card == null)
                    return ApiResponse<DirectSendResultDto>.NotFound("کارت ویزیت یافت نشد");

                if (card.UserId != userId)
                    return ApiResponse<DirectSendResultDto>.Forbidden("کارت ویزیت متعلق به شما نیست");

                if (card.Status != BusinessCardStatus.Published || !card.IsActive)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "فقط کارت ویزیت منتشرشده و فعال قابل ارسال است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var blocked = QuickSendContentApprovalHelper.TryBlockIfNotApproved(
                    card.ApprovalStatus,
                    card.RejectionReason,
                    "کارت ویزیت");
                if (blocked != null)
                    return blocked;

                var publicUrl = BuildPublicUrl(card.Slug);
                if (string.IsNullOrWhiteSpace(publicUrl))
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "لینک عمومی کارت ویزیت در دسترس نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var createMessageResult = await _messageService.CreateMessageAsync(userId, new CreateMessageDto
                {
                    Content = publicUrl
                });

                if (!createMessageResult.Success || createMessageResult.Data == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        createMessageResult.Message ?? "خطا در ایجاد پیام",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var messageId = createMessageResult.Data.Id;

                var selectResult = await _messageService.SelectRecipientsAsync(userId, new SelectRecipientsDto
                {
                    MessageId = messageId,
                    SelectionType = "Individual",
                    MobileNumbers = new List<string> { contact.MobileNumber },
                    FullNames = new List<string> { contact.FullName ?? string.Empty }
                });

                if (!selectResult.Success || selectResult.Data == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        selectResult.Message ?? "خطا در انتخاب گیرندگان",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var session = await _context.MessageSessions
                    .Where(s =>
                        s.MessageId == messageId &&
                        s.UserId == userId &&
                        !s.IsDeleted &&
                        !s.IsUsed)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "خطا در ایجاد Session برای ارسال",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var sendResult = await _messageService.SendDirectMessageAsync(
                    userId,
                    messageId,
                    new SendDirectMessageDto
                    {
                        SendType = CampaignSendType.Quick,
                        PreventDuplicate = false,
                        DuplicatePreventionHours = 24,
                        SendToSpecificTags = false
                    },
                    session,
                    bypassAdminApproval: true);

                _logger.LogInformation(
                    "ارسال سریع کارت ویزیت انجام شد — MessageId: {MessageId}, ContactId: {ContactId}, BusinessCardId: {BusinessCardId}",
                    messageId,
                    quickSendDto.ContactId,
                    quickSendDto.BusinessCardId);

                return sendResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "خطا در ارسال سریع کارت ویزیت — ContactId: {ContactId}, BusinessCardId: {BusinessCardId}",
                    quickSendDto.ContactId,
                    quickSendDto.BusinessCardId);
                return ApiResponse<DirectSendResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<string>> UploadAndPersistLogoAsync(int id, int userId, IFormFile imageFile)
        {
            var cardResult = await GetTrackedCardForUserAsync(id, userId);
            if (cardResult.Error != null)
            {
                return ConvertCardErrorToStringResponse(cardResult.Error);
            }

            var card = cardResult.Card!;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!string.IsNullOrWhiteSpace(card.LogoUrl))
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(
                            card.LogoUrl,
                            FileUploadConstants.EntityType_BusinessCard,
                            id,
                            FileUploadConstants.SubFolder_Logo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "خطا در حذف لوگوی قبلی کارت {CardId}", id);
                    }
                }

                var relativePath = await _fileUploadService.UploadFileAsync(
                    imageFile,
                    FileUploadConstants.EntityType_BusinessCard,
                    id,
                    FileUploadConstants.SubFolder_Logo);

                card.LogoUrl = relativePath;
                card.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                BusinessCardPublicService.InvalidatePublicCache(_cache, card.Slug);

                var imageUrl = _fileUploadService.GetFileUrl(relativePath);
                _logger.LogInformation("لوگوی کارت ویزیت آپلود شد — CardId: {CardId}", id);

                return ApiResponse<string>.CreateSuccess(imageUrl, "لوگو با موفقیت آپلود شد");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطا در آپلود لوگوی کارت {CardId}", id);

                if (ex is ArgumentException)
                {
                    return ApiResponse<string>.BadRequest(
                        ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed),
                        errorCode: ErrorCodes.ValidationFailed);
                }

                throw;
            }
        }

        private async Task<ApiResponse<string>?> EnsureCardOwnedAsync(int id, int userId)
        {
            var owned = await _businessCardRepository.GetOwnedCardAsync(id, userId);
            if (owned != null)
            {
                return null;
            }

            var anyCard = await _businessCardRepository.GetByIdAsync(id);
            if (anyCard == null)
            {
                return ApiResponse<string>.NotFound("کارت ویزیت یافت نشد");
            }

            return ApiResponse<string>.Forbidden(
                ControlledErrorHelper.Unauthorized,
                ErrorCodes.Forbidden);
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

            var contentErrors = GetPublishContentErrors(card);
            if (contentErrors.Count > 0)
            {
                return ApiResponse<BusinessCardResponseDto>.BadRequest(
                    contentErrors[0],
                    contentErrors,
                    ErrorCodes.ValidationFailed);
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
                   || card.ContactEnabled
                   || card.BankingEnabled;
        }

        /// <summary>
        /// قوانین محتوایی انتشار — هم‌تراز BusinessCardSectionValidator موبایل
        /// </summary>
        private static List<string> GetPublishContentErrors(BusinessCard card)
        {
            var errors = new List<string>();

            if (card.SliderEnabled && card.SliderImages.Count == 0)
            {
                errors.Add("برای بخش اسلایدر، حداقل یک تصویر انتخاب کنید");
            }

            if (card.DescriptionEnabled && string.IsNullOrWhiteSpace(card.DescriptionText))
            {
                errors.Add("برای بخش توضیحات، متن را وارد کنید");
            }

            if (card.ServicesEnabled && card.ServiceItems.Count == 0)
            {
                errors.Add("برای بخش تعرفه خدمات، حداقل یک مورد اضافه کنید");
            }

            if (card.MapEnabled && (!card.MapLatitude.HasValue || !card.MapLongitude.HasValue))
            {
                errors.Add("موقعیت روی نقشه را انتخاب کنید");
            }

            if (card.ContactEnabled
                && string.IsNullOrWhiteSpace(card.ContactPhone)
                && string.IsNullOrWhiteSpace(card.ContactEmail)
                && string.IsNullOrWhiteSpace(card.ContactInstagram)
                && card.SocialLinks.Count == 0)
            {
                errors.Add("برای بخش تماس، حداقل یک کانال ارتباطی وارد کنید");
            }

            if (card.BankingEnabled
                && string.IsNullOrWhiteSpace(card.BankAccountNumber)
                && string.IsNullOrWhiteSpace(card.BankCardNumber)
                && string.IsNullOrWhiteSpace(card.BankShebaNumber))
            {
                errors.Add("برای بخش بانکی، حداقل یک شماره حساب، کارت یا شبا وارد کنید");
            }

            if (!HasAtLeastOneActiveSection(card))
            {
                errors.Add("حداقل یک بخش باید فعال باشد");
            }

            return errors;
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

        private static ApiResponse<string> ConvertCardErrorToStringResponse(ApiResponse<BusinessCardResponseDto> errorResponse)
        {
            if (errorResponse.StatusCode == 404)
            {
                return ApiResponse<string>.NotFound(errorResponse.Message);
            }

            if (errorResponse.StatusCode == 403)
            {
                return ApiResponse<string>.Forbidden(errorResponse.Message, errorResponse.ErrorCode);
            }

            return ApiResponse<string>.BadRequest(errorResponse.Message, errorResponse.Errors, errorResponse.ErrorCode);
        }

        private static string? ResolveImageSubFolder(string? imageType)
        {
            var normalized = NormalizeOptionalText(imageType);
            if (string.IsNullOrEmpty(normalized))
            {
                return FileUploadConstants.SubFolder_Images;
            }

            return normalized.ToLowerInvariant() switch
            {
                "logo" => FileUploadConstants.SubFolder_Logo,
                "slider" => FileUploadConstants.SubFolder_Slider,
                "service" => FileUploadConstants.SubFolder_Service,
                "image" => FileUploadConstants.SubFolder_Images,
                _ => null
            };
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
                var url = NormalizeStoredFilePath(image.ImageUrl);
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
                    ImageUrl = NormalizeStoredFilePath(item.ImageUrl),
                    DisplayOrder = order++
                });
            }
        }

        private static void ApplySocialLinks(BusinessCard card, List<BusinessCardSocialLinkDto> links)
        {
            var order = 0;
            foreach (var link in links.OrderBy(i => i.DisplayOrder))
            {
                var networkType = BusinessCardSocialNetworkHelper.NormalizeType(link.NetworkType);
                var value = NormalizeOptionalText(link.Value);
                if (networkType == null || value == null)
                {
                    continue;
                }

                card.SocialLinks.Add(new BusinessCardSocialLink
                {
                    NetworkType = networkType,
                    Label = NormalizeOptionalText(link.Label),
                    Value = value,
                    DisplayOrder = order++
                });
            }

            // همگام‌سازی فیلد قدیمی اینستاگرام برای کلاینت‌های قبلی
            var firstInstagram = card.SocialLinks
                .OrderBy(l => l.DisplayOrder)
                .FirstOrDefault(l => l.NetworkType.Equals("instagram", StringComparison.OrdinalIgnoreCase));
            card.ContactInstagram = firstInstagram?.Value;
        }

        private static void ApplyBankingFields(
            BusinessCard card,
            string? accountNumber,
            string? cardNumber,
            string? shebaNumber,
            bool applyAccount,
            bool applyCard,
            bool applySheba)
        {
            if (applyAccount)
            {
                var (normalized, _) = BusinessCardSocialNetworkHelper.NormalizeAccountNumber(accountNumber);
                card.BankAccountNumber = normalized;
            }

            if (applyCard)
            {
                var (normalized, _) = BusinessCardSocialNetworkHelper.NormalizeCardNumber(cardNumber);
                card.BankCardNumber = normalized;
            }

            if (applySheba)
            {
                var (normalized, _) = BusinessCardSocialNetworkHelper.NormalizeSheba(shebaNumber);
                card.BankShebaNumber = normalized;
            }
        }

        private static List<string> ValidateSectionsPayload(
            List<BusinessCardSliderImageDto>? sliderImages,
            List<BusinessCardServiceItemDto>? serviceItems,
            List<BusinessCardSocialLinkDto>? socialLinks,
            string? contactEmail,
            string? bankAccountNumber,
            string? bankCardNumber,
            string? bankShebaNumber)
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

            if (socialLinks != null && socialLinks.Count > BusinessCardConstants.MaxSocialLinks)
            {
                errors.Add($"حداکثر {BusinessCardConstants.MaxSocialLinks} لینک شبکه اجتماعی مجاز است");
            }

            if (socialLinks != null)
            {
                foreach (var link in socialLinks)
                {
                    if (!BusinessCardSocialNetworkHelper.IsAllowed(link.NetworkType))
                    {
                        errors.Add("نوع شبکه اجتماعی نامعتبر است");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(link.Value))
                    {
                        errors.Add("مقدار لینک شبکه اجتماعی الزامی است");
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(contactEmail) &&
                !contactEmail.Contains('@', StringComparison.Ordinal))
            {
                errors.Add("فرمت ایمیل نامعتبر است");
            }

            if (bankAccountNumber != null)
            {
                var (_, accountError) = BusinessCardSocialNetworkHelper.NormalizeAccountNumber(bankAccountNumber);
                if (accountError != null)
                {
                    errors.Add(accountError);
                }
            }

            if (bankCardNumber != null)
            {
                var (_, cardError) = BusinessCardSocialNetworkHelper.NormalizeCardNumber(bankCardNumber);
                if (cardError != null)
                {
                    errors.Add(cardError);
                }
            }

            if (bankShebaNumber != null)
            {
                var (_, shebaError) = BusinessCardSocialNetworkHelper.NormalizeSheba(bankShebaNumber);
                if (shebaError != null)
                {
                    errors.Add(shebaError);
                }
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
                   || dto.BankingEnabled.HasValue
                   || dto.DescriptionTitle != null
                   || dto.DescriptionText != null
                   || dto.MapLatitude.HasValue
                   || dto.MapLongitude.HasValue
                   || dto.MapAddress != null
                   || dto.ContactPhone != null
                   || dto.ContactEmail != null
                   || dto.ContactInstagram != null
                   || dto.BankAccountNumber != null
                   || dto.BankCardNumber != null
                   || dto.BankShebaNumber != null
                   || dto.SliderImages != null
                   || dto.ServiceItems != null
                   || dto.SocialLinks != null;
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
                LogoUrl = ToPublicFileUrl(card.LogoUrl),
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
                BankingEnabled = card.BankingEnabled,
                DescriptionTitle = card.DescriptionTitle,
                DescriptionText = card.DescriptionText,
                MapLatitude = card.MapLatitude,
                MapLongitude = card.MapLongitude,
                MapAddress = card.MapAddress,
                ContactPhone = card.ContactPhone,
                ContactEmail = card.ContactEmail,
                ContactInstagram = card.ContactInstagram,
                BankAccountNumber = card.BankAccountNumber,
                BankCardNumber = card.BankCardNumber,
                BankShebaNumber = card.BankShebaNumber,
                SliderImages = card.SliderImages
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new BusinessCardSliderImageDto
                    {
                        ImageUrl = ToPublicFileUrl(i.ImageUrl) ?? i.ImageUrl,
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
                        ImageUrl = ToPublicFileUrl(i.ImageUrl),
                        DisplayOrder = i.DisplayOrder
                    })
                    .ToList(),
                SocialLinks = MapSocialLinks(card),
                CreatedAt = EnsureUtc(card.CreatedAt),
                UpdatedAt = EnsureUtc(card.UpdatedAt),
                PublishedAt = EnsureUtc(card.PublishedAt),
                ApprovalStatus = card.ApprovalStatus,
                RejectionReason = card.RejectionReason,
                ApprovedAt = EnsureUtc(card.ApprovedAt)
            };
        }

        private static List<BusinessCardSocialLinkDto> MapSocialLinks(BusinessCard card)
        {
            var links = card.SocialLinks
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new BusinessCardSocialLinkDto
                {
                    Id = i.Id,
                    NetworkType = i.NetworkType,
                    Label = BusinessCardSocialNetworkHelper.ResolveDisplayLabel(i.NetworkType, i.Label),
                    Value = i.Value,
                    DisplayOrder = i.DisplayOrder
                })
                .ToList();

            // سازگاری: اگر جدول خالی است ولی ContactInstagram قدیمی پر است
            if (links.Count == 0 && !string.IsNullOrWhiteSpace(card.ContactInstagram))
            {
                links.Add(new BusinessCardSocialLinkDto
                {
                    NetworkType = "instagram",
                    Label = BusinessCardSocialNetworkHelper.ResolveDisplayLabel("instagram", null),
                    Value = card.ContactInstagram,
                    DisplayOrder = 0
                });
            }

            return links;
        }

        private string? ToPublicFileUrl(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return null;
            }

            return _fileUploadService.GetFileUrl(storedPath);
        }

        /// <summary>
        /// مسیر ذخیره‌شده باید نسبی بماند (مثل Contact/User)، نه URL کامل پاسخ GetFileUrl.
        /// </summary>
        private static string? NormalizeStoredFilePath(string? value)
        {
            var normalized = NormalizeOptionalText(value);
            if (normalized == null)
            {
                return null;
            }

            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
                {
                    normalized = uri.AbsolutePath.TrimStart('/');
                }
            }

            return normalized.TrimStart('/');
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
