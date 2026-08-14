using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.ReferralProgram;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api_Vapp.Services
{
    public class ReferralProgramService : IReferralProgramService
    {
        private readonly Api_Context _context;
        private readonly IReferralProgramRepository _programRepository;
        private readonly IReferralProgramDraftRepository _draftRepository;
        private readonly IReferralUsageRepository _usageRepository;
        private readonly IReferralContactCodeRepository _contactCodeRepository;
        private readonly IUserSmsBillingService _userSmsBilling;
        private readonly IAuditService _audit;
        private readonly ILogger<ReferralProgramService> _logger;

        private const int DraftExpirationHours = 24;
        private const decimal MinFixedAmount = 1000m;
        private const decimal MaxFixedAmount = 10_000_000m;
        private const decimal MaxPercentage = 100m;
        private const decimal MaxPurchaseAmount = 100_000_000m;
        private const int MaxRedeemsPerCodePerDay = 30;

        public ReferralProgramService(
            Api_Context context,
            IReferralProgramRepository programRepository,
            IReferralProgramDraftRepository draftRepository,
            IReferralUsageRepository usageRepository,
            IReferralContactCodeRepository contactCodeRepository,
            IUserSmsBillingService userSmsBilling,
            IAuditService audit,
            ILogger<ReferralProgramService> logger)
        {
            _context = context;
            _programRepository = programRepository;
            _draftRepository = draftRepository;
            _usageRepository = usageRepository;
            _contactCodeRepository = contactCodeRepository;
            _userSmsBilling = userSmsBilling;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<ReferralProgramListDto>> GetProgramsAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var programs = await _programRepository.GetByUserIdAsync(userId, pageNumber, pageSize, isActive);
            var totalCount = await _programRepository.GetCountByUserIdAsync(userId, isActive);
            var activeCount = await _programRepository.GetCountByUserIdAsync(userId, true);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var dtos = new List<ReferralProgramDto>();
            foreach (var program in programs)
            {
                dtos.Add(MapToDto(program));
            }

            return ApiResponse<ReferralProgramListDto>.CreateSuccess(new ReferralProgramListDto
            {
                Programs = dtos,
                TotalCount = totalCount,
                ActiveCount = activeCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        public async Task<ApiResponse<ReferralDashboardStatsDto>> GetDashboardStatsAsync(int userId)
        {
            try
            {
                var activeProgramsCount = await _programRepository.GetActiveCountByUserIdAsync(userId);
                var successfulReferrals = await _usageRepository.GetSuccessfulReferralsCountAsync(userId);
                var totalRewardsPaid = await _usageRepository.GetTotalRewardsPaidAsync(userId);
                var activeUsersCount = await _usageRepository.GetDistinctActiveContactsCountAsync(userId);

                return ApiResponse<ReferralDashboardStatsDto>.CreateSuccess(new ReferralDashboardStatsDto
                {
                    SuccessfulReferrals = successfulReferrals,
                    TotalRewardsPaid = totalRewardsPaid,
                    FormattedTotalRewardsPaid = $"{totalRewardsPaid:N0} تومان",
                    ActiveProgramsCount = activeProgramsCount,
                    ActiveUsersCount = activeUsersCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت آمار داشبورد پاداش برای کاربر {UserId}", userId);
                return ApiResponse<ReferralDashboardStatsDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ReferralProgramDto>> GetByIdAsync(int id, int userId)
        {
            var program = await _programRepository.GetByIdAndUserIdAsync(id, userId);
            if (program == null)
            {
                return ApiResponse<ReferralProgramDto>.NotFound("برنامه پاداش یافت نشد");
            }

            var codesCount = await _contactCodeRepository.GetCountByProgramIdAsync(id, userId);
            return ApiResponse<ReferralProgramDto>.CreateSuccess(MapToDto(program, codesCount));
        }

        public async Task<ApiResponse<ReferralProgramDto>> ToggleStatusAsync(int id, int userId)
        {
            var program = await _programRepository.GetByIdAndUserIdAsync(id, userId);
            if (program == null)
            {
                return ApiResponse<ReferralProgramDto>.NotFound("برنامه پاداش یافت نشد");
            }

            program.IsActive = !program.IsActive;
            program.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Referral,
                Action = AuditActions.ReferralProgramActivated,
                EntityType = AuditEntityTypes.ReferralProgram,
                EntityId = program.Id.ToString(),
                ActorUserId = userId,
                After = new { isActive = program.IsActive }
            });

            var statusText = program.IsActive ? "فعال" : "غیرفعال";
            return ApiResponse<ReferralProgramDto>.CreateSuccess(MapToDto(program), $"برنامه پاداش {statusText} شد");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id, int userId)
        {
            var program = await _programRepository.GetByIdAndUserIdAsync(id, userId);
            if (program == null)
            {
                return ApiResponse<bool>.NotFound("برنامه پاداش یافت نشد");
            }

            program.IsDeleted = true;
            program.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _contactCodeRepository.SoftDeleteByProgramIdAsync(id, userId);

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Referral,
                Action = AuditActions.ReferralProgramDeleted,
                EntityType = AuditEntityTypes.ReferralProgram,
                EntityId = program.Id.ToString(),
                ActorUserId = userId
            });

            return ApiResponse<bool>.CreateSuccess(true, "برنامه پاداش حذف شد");
        }

        public async Task<ApiResponse<List<ReferralNotebookDto>>> GetNotebooksAsync(int userId)
        {
            var notebooks = await _context.ContactNotebooks
                .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                .OrderBy(cn => cn.Name)
                .Select(cn => new ReferralNotebookDto
                {
                    Id = cn.Id,
                    Name = cn.Name,
                    MembersCount = cn.Contacts.Count(c => !c.IsDeleted)
                })
                .ToListAsync();

            return ApiResponse<List<ReferralNotebookDto>>.CreateSuccess(notebooks);
        }

        public async Task<ApiResponse<ReferralContactListDto>> GetContactsAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 20,
            string? searchTerm = null,
            int? notebookId = null)
        {
            _logger.LogInformation(
                "شروع دریافت مخاطبین پاداش — UserId: {UserId}, Page: {Page}, NotebookId: {NotebookId}",
                userId, pageNumber, notebookId);

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            if (notebookId.HasValue && notebookId.Value > 0)
            {
                var ownsNotebook = await _context.ContactNotebooks
                    .AnyAsync(cn => cn.Id == notebookId.Value && cn.UserId == userId && !cn.IsDeleted);
                if (!ownsNotebook)
                {
                    return ApiResponse<ReferralContactListDto>.NotFound("دفترچه یافت نشد");
                }
            }

            var query = _context.Contacts
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                    && c.ContactNotebook != null
                    && c.ContactNotebook.UserId == userId
                    && !c.ContactNotebook.IsDeleted);

            if (notebookId.HasValue && notebookId.Value > 0)
            {
                query = query.Where(c => c.ContactNotebookId == notebookId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.MobileNumber.Contains(term) ||
                    (c.FullName != null && c.FullName.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var contacts = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ReferralContactDto
                {
                    Id = c.Id,
                    FullName = c.FullName ?? string.Empty,
                    MobileNumber = c.MobileNumber,
                    ContactNotebookId = c.ContactNotebookId,
                    ContactNotebookName = c.ContactNotebook != null ? c.ContactNotebook.Name : string.Empty
                })
                .ToListAsync();

            _logger.LogInformation(
                "پایان دریافت مخاطبین پاداش — UserId: {UserId}, Count: {Count}",
                userId, contacts.Count);

            return ApiResponse<ReferralContactListDto>.CreateSuccess(new ReferralContactListDto
            {
                Contacts = contacts,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        public async Task<ApiResponse<ReferralStep1ValidationResponseDto>> ValidateStep1Async(int userId, ReferralStep1Dto step1Dto)
        {
            var errors = ValidateStep1Fields(step1Dto);
            var response = new ReferralStep1ValidationResponseDto
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };

            if (!response.IsValid)
            {
                return ApiResponse<ReferralStep1ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", errors);
            }

            if (await _programRepository.ExistsByTitleAsync(userId, step1Dto.Title.Trim()))
            {
                return ApiResponse<ReferralStep1ValidationResponseDto>.BadRequest("برنامه‌ای با این نام قبلاً ثبت شده است");
            }

            var draftId = $"{userId}_{Guid.NewGuid()}";
            var expiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);
            var draft = new ReferralProgramDraft
            {
                UserId = userId,
                DraftId = draftId,
                Step1Data = JsonSerializer.Serialize(step1Dto),
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };

            await _draftRepository.AddAsync(draft);

            response.DraftId = draftId;
            response.DraftExpiresAt = EnsureUtc(expiresAt);

            return ApiResponse<ReferralStep1ValidationResponseDto>.CreateSuccess(response, "اطلاعات مرحله 1 معتبر است");
        }

        public async Task<ApiResponse<ReferralStep2ValidationResponseDto>> ValidateStep2Async(int userId, ReferralStep2Dto step2Dto)
        {
            var errors = new List<string>();
            var audienceDescription = string.Empty;
            var totalContactsCount = 0;

            if (string.IsNullOrWhiteSpace(step2Dto.DraftId))
            {
                errors.Add("شناسه پیش‌نویس الزامی است. لطفاً از مرحله 1 دوباره شروع کنید");
            }

            if (string.IsNullOrWhiteSpace(step2Dto.TargetAudience))
            {
                errors.Add("نوع مخاطبین الزامی است");
            }
            else if (step2Dto.TargetAudience != ReferralTargetAudience.All &&
                     step2Dto.TargetAudience != ReferralTargetAudience.SpecificNotebooks &&
                     step2Dto.TargetAudience != ReferralTargetAudience.Individual)
            {
                errors.Add("نوع مخاطبین نامعتبر است");
            }

            if (errors.Count == 0)
            {
                var audienceResult = await ValidateAndCountAudienceAsync(userId, step2Dto);
                errors.AddRange(audienceResult.Errors);
                audienceDescription = audienceResult.Description;
            }

            if (errors.Count == 0)
            {
                errors.AddRange(await ValidateStep2TagFieldsAsync(userId, step2Dto));
            }

            if (errors.Count == 0)
            {
                var contactIds = await GetContactIdsAsync(
                    userId,
                    step2Dto,
                    step2Dto.SendToSpecificTags ? step2Dto.TargetTagIds : null,
                    step2Dto.SendToSpecificTags);
                totalContactsCount = contactIds.Count;

                if (totalContactsCount == 0)
                {
                    errors.Add("پس از اعمال فیلتر مخاطبین، هیچ گیرنده‌ای یافت نشد");
                }
                else if (step2Dto.SendToSpecificTags &&
                         step2Dto.TargetTagIds != null &&
                         step2Dto.TargetTagIds.Any())
                {
                    audienceDescription += $" + {step2Dto.TargetTagIds.Count} تگ";
                }
            }

            var response = new ReferralStep2ValidationResponseDto
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                TotalContactsCount = totalContactsCount,
                TargetAudienceDescription = audienceDescription
            };

            if (response.IsValid && !string.IsNullOrEmpty(step2Dto.DraftId))
            {
                var draft = await _draftRepository.GetActiveByDraftIdAsync(step2Dto.DraftId!, userId);
                if (draft == null)
                {
                    response.IsValid = false;
                    response.Errors.Add("پیش‌نویس یافت نشد یا منقضی شده است");
                    return ApiResponse<ReferralStep2ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", response.Errors);
                }

                draft.Step2Data = JsonSerializer.Serialize(step2Dto);
                draft.ExpiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);
                await _draftRepository.UpdateAsync(draft);
            }

            if (!response.IsValid)
            {
                return ApiResponse<ReferralStep2ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", response.Errors);
            }

            return ApiResponse<ReferralStep2ValidationResponseDto>.CreateSuccess(response, "اطلاعات مرحله 2 معتبر است");
        }

        public async Task<ApiResponse<ReferralSummaryDto>> GetSummaryAsync(int userId, GetReferralSummaryRequestDto request)
        {
            var loadResult = await LoadDraftStepsAsync(userId, request.DraftId);
            if (!loadResult.Success)
            {
                return ApiResponse<ReferralSummaryDto>.BadRequest(loadResult.ErrorMessage!);
            }

            var step3 = !string.IsNullOrEmpty(loadResult.Draft?.Step3Data)
                ? JsonSerializer.Deserialize<SaveReferralStep3SettingsDto>(loadResult.Draft!.Step3Data!)
                : null;

            return ApiResponse<ReferralSummaryDto>.CreateSuccess(
                await BuildSummaryAsync(userId, loadResult.Step1!, loadResult.Step2!, step3));
        }

        public async Task<ApiResponse<ReferralSummaryDto>> SaveStep3SettingsAsync(int userId, SaveReferralStep3RequestDto request)
        {
            var loadResult = await LoadDraftStepsAsync(userId, request.DraftId);
            if (!loadResult.Success)
            {
                return ApiResponse<ReferralSummaryDto>.BadRequest(loadResult.ErrorMessage!);
            }

            if (request.Settings == null)
            {
                return ApiResponse<ReferralSummaryDto>.BadRequest("تنظیمات مرحله 3 الزامی است");
            }

            var step3Errors = ValidateStep3Fields(request.Settings);
            if (step3Errors.Any())
            {
                return ApiResponse<ReferralSummaryDto>.BadRequest("خطا در اعتبارسنجی", step3Errors);
            }

            var draft = loadResult.Draft!;
            draft.Step3Data = JsonSerializer.Serialize(request.Settings);
            draft.ExpiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);
            await _draftRepository.UpdateAsync(draft);

            var summary = await BuildSummaryAsync(userId, loadResult.Step1!, loadResult.Step2!, request.Settings);
            return ApiResponse<ReferralSummaryDto>.CreateSuccess(summary, "تنظیمات مرحله 3 ذخیره شد");
        }

        public async Task<ApiResponse<ConfirmReferralProgramResponseDto>> ConfirmAsync(int userId, ConfirmReferralProgramDto request)
        {
            var draft = await _draftRepository.GetActiveByDraftIdAsync(request.DraftId, userId);
            if (draft == null)
            {
                return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest("پیش‌نویس یافت نشد یا منقضی شده است");
            }

            var step1 = JsonSerializer.Deserialize<ReferralStep1Dto>(draft.Step1Data);
            var step2 = string.IsNullOrEmpty(draft.Step2Data)
                ? null
                : JsonSerializer.Deserialize<ReferralStep2Dto>(draft.Step2Data);
            var step3 = string.IsNullOrEmpty(draft.Step3Data)
                ? null
                : JsonSerializer.Deserialize<SaveReferralStep3SettingsDto>(draft.Step3Data);

            if (step1 == null || step2 == null || step3 == null)
            {
                return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest("تمام مراحل باید تکمیل شوند");
            }

            var step1Errors = ValidateStep1Fields(step1);
            var step3Errors = ValidateStep3Fields(step3);
            if (step1Errors.Any() || step3Errors.Any())
            {
                return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest("داده‌های پیش‌نویس نامعتبر است", step1Errors.Concat(step3Errors).ToList());
            }

            if (await _programRepository.ExistsByTitleAsync(userId, step1.Title.Trim()))
            {
                return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest("برنامه‌ای با این نام قبلاً ثبت شده است");
            }

            var audienceResult = await ValidateAndCountAudienceAsync(userId, step2);
            if (audienceResult.Errors.Any())
            {
                return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest("خطا در مخاطبین", audienceResult.Errors);
            }

            var step2TagErrors = await ValidateStep2TagFieldsAsync(userId, step2);
            if (step2TagErrors.Any())
            {
                return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest("خطا در فیلتر تگ", step2TagErrors);
            }

            var contactIds = await GetContactIdsAsync(
                userId,
                step2,
                step2.SendToSpecificTags ? step2.TargetTagIds : null,
                step2.SendToSpecificTags);

            if (!contactIds.Any())
            {
                return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest(
                    "پس از اعمال فیلتر مخاطبین، هیچ گیرنده‌ای برای ارسال پیامک یافت نشد");
            }

            var publicCode = await GenerateUniquePublicCodeAsync(userId);
            var startDate = NormalizeIncomingUtc(step3.StartDate);
            DateTime? endDate = null;
            if (step3.EndDate.HasValue)
            {
                endDate = NormalizeIncomingUtc(step3.EndDate.Value);

                if (endDate <= startDate)
                {
                    return ApiResponse<ConfirmReferralProgramResponseDto>.BadRequest("تاریخ پایان باید بعد از تاریخ شروع باشد");
                }
            }

            var isReferrerRewardActive = step1.IsReferrerRewardActive && step1.ReferrerRewardValue > 0;
            var program = new ReferralProgram
            {
                UserId = userId,
                Title = step1.Title.Trim(),
                IsActive = step1.IsActive,
                RewardType = step1.RewardType,
                IsReferrerRewardActive = isReferrerRewardActive,
                ReferrerRewardValue = isReferrerRewardActive ? step1.ReferrerRewardValue : 0,
                IsCustomerRewardActive = step1.IsCustomerRewardActive,
                CustomerRewardValue = step1.IsCustomerRewardActive ? step1.CustomerRewardValue : null,
                PublicCode = publicCode,
                TargetAudience = step2.TargetAudience,
                TargetNotebookIds = step2.TargetNotebookIds != null ? JsonSerializer.Serialize(step2.TargetNotebookIds) : null,
                TargetContactIds = step2.TargetContactIds != null ? JsonSerializer.Serialize(step2.TargetContactIds) : null,
                SendToSpecificTags = step2.SendToSpecificTags,
                TargetTagIds = step2.SendToSpecificTags && step2.TargetTagIds != null
                    ? JsonSerializer.Serialize(step2.TargetTagIds)
                    : null,
                StartDate = startDate,
                EndDate = endDate,
                CreatedAt = DateTime.UtcNow
            };

            await _context.ReferralPrograms.AddAsync(program);
            await _context.SaveChangesAsync();

            var personalCodes = await CreatePersonalCodesAsync(program, contactIds);
            var smsResult = await SendReferralInviteSmsAsync(program, personalCodes);
            program.NotifiedContactsCount = smsResult.SentCount;
            program.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _draftRepository.DeleteAsync(request.DraftId, userId);

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Referral,
                Action = AuditActions.ReferralProgramCreated,
                EntityType = AuditEntityTypes.ReferralProgram,
                EntityId = program.Id.ToString(),
                ActorUserId = userId,
                After = new
                {
                    title = program.Title,
                    publicCode = program.PublicCode,
                    personalCodesCount = personalCodes.Count,
                    isActive = program.IsActive
                }
            });

            _logger.LogInformation(
                "برنامه پاداش {ProgramId} با {CodesCount} کد شخصی برای کاربر {UserId} ایجاد شد",
                program.Id, personalCodes.Count, userId);

            return ApiResponse<ConfirmReferralProgramResponseDto>.CreateSuccess(
                new ConfirmReferralProgramResponseDto
                {
                    Program = MapToDto(program, personalCodes.Count),
                    SmsSentCount = smsResult.SentCount,
                    SmsFailedCount = smsResult.FailedCount
                },
                "برنامه پاداش با موفقیت ثبت شد",
                201);
        }

        public async Task<ApiResponse<InquireReferralCodeResponseDto>> InquireCodeAsync(int userId, InquireReferralCodeDto request)
        {
            var resolved = await ResolvePersonalCodeAsync(userId, request.Code);
            if (resolved == null)
            {
                var notFound = await BuildCodeNotFoundResponseAsync(userId, request.Code);
                return ApiResponse<InquireReferralCodeResponseDto>.CreateSuccess(
                    notFound,
                    notFound.StatusMessage ?? ReferralProgramValidity.CodeNotFoundMessage);
            }

            var program = resolved.Program;
            var state = ReferralProgramValidity.Evaluate(program);

            if (!state.IsValid)
            {
                return ApiResponse<InquireReferralCodeResponseDto>.CreateSuccess(new InquireReferralCodeResponseDto
                {
                    IsValid = false,
                    IsExpired = state.IsExpired,
                    IsNotStarted = state.IsNotStarted,
                    IsActive = program.IsActive,
                    InvalidReason = state.InvalidReason,
                    StatusMessage = state.StatusMessage,
                    ProgramId = program.Id,
                    ProgramName = program.Title,
                    PublicCode = resolved.UsedCode,
                    RewardType = program.RewardType,
                    StartDate = EnsureUtc(program.StartDate),
                    EndDate = EnsureUtc(program.EndDate)
                }, state.InvalidReason ?? "کد نامعتبر است");
            }

            if (request.PurchaseAmount.HasValue &&
                (request.PurchaseAmount.Value <= 0 || request.PurchaseAmount.Value > MaxPurchaseAmount))
            {
                return ApiResponse<InquireReferralCodeResponseDto>.BadRequest(
                    $"مبلغ خرید باید بین ۱ تا {MaxPurchaseAmount:N0} تومان باشد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var customerDiscount = BuildCustomerDiscountInfo(program, request.PurchaseAmount);
            var referrerReward = BuildReferrerRewardInfo(program, request.PurchaseAmount);

            var response = new InquireReferralCodeResponseDto
            {
                IsValid = true,
                IsExpired = false,
                IsNotStarted = false,
                IsActive = program.IsActive,
                StatusMessage = state.StatusMessage,
                ProgramId = program.Id,
                ProgramName = program.Title,
                PublicCode = resolved.UsedCode,
                RewardType = program.RewardType,
                IsCustomerRewardActive = program.IsCustomerRewardActive,
                CustomerDiscountAmount = customerDiscount.Amount,
                FormattedCustomerDiscount = customerDiscount.Formatted,
                IsReferrerRewardActive = program.IsReferrerRewardActive,
                ReferrerRewardAmount = referrerReward.Amount,
                FormattedReferrerReward = referrerReward.Formatted,
                ReferrerContactId = resolved.ReferrerContactId,
                ReferrerContactName = resolved.ReferrerContactName,
                // موبایل معرف عمداً در استعلام برگردانده نمی‌شود (کاهش افشای اطلاعات)
                ReferrerContactMobile = null,
                StartDate = EnsureUtc(program.StartDate),
                EndDate = EnsureUtc(program.EndDate)
            };

            return ApiResponse<InquireReferralCodeResponseDto>.CreateSuccess(response, state.StatusMessage);
        }

        public async Task<ApiResponse<RedeemReferralCodeResponseDto>> RedeemCodeAsync(int userId, RedeemReferralCodeDto request)
        {
            try
            {
                return await RedeemCodeCoreAsync(userId, request);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "خطای دیتابیس در ثبت مصرف کد {Code} برای کاربر {UserId}", request.Code, userId);
                return ApiResponse<RedeemReferralCodeResponseDto>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ثبت مصرف کد {Code} برای کاربر {UserId}", request.Code, userId);
                return ApiResponse<RedeemReferralCodeResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<RedeemReferralCodeResponseDto>> RedeemCodeCoreAsync(int userId, RedeemReferralCodeDto request)
        {
            var resolved = await ResolvePersonalCodeAsync(userId, request.Code);
            if (resolved == null)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.NotFound("کد یافت نشد");
            }

            var program = resolved.Program;
            var state = ReferralProgramValidity.Evaluate(program);
            if (!state.IsValid)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(state.InvalidReason ?? "کد قابل استفاده نیست");
            }

            if (!request.PurchaseAmount.HasValue ||
                request.PurchaseAmount.Value <= 0 ||
                request.PurchaseAmount.Value > MaxPurchaseAmount)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                    $"مبلغ خرید باید بین ۱ تا {MaxPurchaseAmount:N0} تومان باشد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            if (!request.CustomerContactId.HasValue || request.CustomerContactId.Value <= 0)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                    "شناسه مخاطب خریدار الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var customerContactId = request.CustomerContactId.Value;
            var customerError = await ValidateContactOwnershipAsync(userId, customerContactId);
            if (customerError != null)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(customerError);
            }

            var referrerContactId = resolved.ReferrerContactId;
            if (!referrerContactId.HasValue)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                    "کد معرف شخصی معتبر نیست");
            }

            var referrerStillValid = await ValidateContactOwnershipAsync(userId, referrerContactId.Value);
            if (referrerStillValid != null)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest("مخاطب معرف معتبر نیست یا حذف شده است");
            }

            if (customerContactId == referrerContactId.Value)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                    "استفاده از کد معرف خود خریدار مجاز نیست",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var sameMobileAbuse = await AreContactsSameMobileAsync(customerContactId, referrerContactId.Value);
            if (sameMobileAbuse)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                    "کد معرف و خریدار متعلق به یک شماره موبایل هستند و قابل استفاده نیست",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? null
                : request.IdempotencyKey.Trim();

            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                var existingByKey = await _context.ReferralUsages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.UserId == userId &&
                        u.IdempotencyKey == idempotencyKey &&
                        u.Status == ReferralUsageStatuses.Completed);

                if (existingByKey != null)
                {
                    return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                        "این تراکنش قبلاً ثبت شده است",
                        errorCode: ErrorCodes.InvalidInput);
                }
            }

            var abuseError = await ValidateRedeemAbuseGuardsAsync(
                userId,
                resolved.UsedCode,
                customerContactId,
                program.Id);
            if (abuseError != null)
            {
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                    abuseError,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var customerDiscount = program.IsCustomerRewardActive && program.CustomerRewardValue.HasValue
                ? CalculateRewardAmount(program.RewardType, program.CustomerRewardValue.Value, request.PurchaseAmount) ?? 0
                : 0;

            var referrerReward = program.IsReferrerRewardActive && program.ReferrerRewardValue > 0
                ? CalculateRewardAmount(program.RewardType, program.ReferrerRewardValue, request.PurchaseAmount) ?? 0
                : 0;

            customerDiscount = CapRewardAmount(customerDiscount);
            referrerReward = CapRewardAmount(referrerReward);

            var validityDays = GetRewardValidityDays(program);
            var now = DateTime.UtcNow;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // قفل نرم ضد race برای همان کد + مشتری در همان روز
                var dayStart = now.Date;
                var dayEnd = dayStart.AddDays(1);
                var duplicateInTx = await _context.ReferralUsages
                    .AnyAsync(u =>
                        u.UserId == userId &&
                        u.PublicCode == resolved.UsedCode &&
                        u.CustomerContactId == customerContactId &&
                        u.Status == ReferralUsageStatuses.Completed &&
                        u.CreatedAt >= dayStart &&
                        u.CreatedAt < dayEnd);

                if (duplicateInTx)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                        "این کد امروز برای این خریدار قبلاً ثبت شده است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var usage = new ReferralUsage
                {
                    ReferralProgramId = program.Id,
                    UserId = userId,
                    PublicCode = resolved.UsedCode,
                    PurchaseAmount = request.PurchaseAmount,
                    CustomerDiscountAmount = customerDiscount,
                    ReferrerRewardAmount = referrerReward,
                    CustomerContactId = customerContactId,
                    ReferrerContactId = referrerContactId,
                    IdempotencyKey = idempotencyKey,
                    Status = ReferralUsageStatuses.Completed,
                    Description = request.Description,
                    CreatedAt = now
                };

                await _context.ReferralUsages.AddAsync(usage);
                await _context.SaveChangesAsync();

                var customerCredited = false;
                var referrerCredited = false;

                if (customerDiscount > 0)
                {
                    customerCredited = await CreditContactCashbackAsync(
                        userId,
                        customerContactId,
                        customerDiscount,
                        $"تخفیف برنامه پاداش «{program.Title}»",
                        validityDays);
                }

                if (referrerReward > 0)
                {
                    referrerCredited = await CreditContactCashbackAsync(
                        userId,
                        referrerContactId.Value,
                        referrerReward,
                        $"پاداش معرف برنامه «{program.Title}»",
                        validityDays);
                }

                await transaction.CommitAsync();

                var referrerSmsSent = false;
                if (referrerReward > 0)
                {
                    referrerSmsSent = await SendReferrerRewardSmsAsync(
                        program,
                        referrerContactId.Value,
                        resolved.ReferrerContactMobile,
                        referrerReward);
                }

                _logger.LogInformation(
                    "مصرف کد {Code} برای برنامه {ProgramId} توسط کاربر {UserId} ثبت شد - UsageId: {UsageId}, ReferrerSms: {SmsSent}",
                    resolved.UsedCode, program.Id, userId, usage.Id, referrerSmsSent);

                var successMessage = BuildRedeemSuccessMessage(
                    customerCredited,
                    referrerCredited,
                    customerContactId,
                    referrerContactId,
                    customerDiscount,
                    referrerReward,
                    referrerSmsSent);

                return ApiResponse<RedeemReferralCodeResponseDto>.CreateSuccess(
                    new RedeemReferralCodeResponseDto
                    {
                        UsageId = usage.Id,
                        ProgramId = program.Id,
                        ProgramName = program.Title,
                        PublicCode = resolved.UsedCode,
                        PurchaseAmount = request.PurchaseAmount,
                        CustomerDiscountAmount = customerDiscount,
                        FormattedCustomerDiscount = $"{customerDiscount:N0} تومان",
                        ReferrerRewardAmount = referrerReward,
                        FormattedReferrerReward = $"{referrerReward:N0} تومان",
                        ReferrerContactId = referrerContactId,
                        ReferrerContactName = resolved.ReferrerContactName,
                        CustomerRewardCredited = customerCredited,
                        ReferrerRewardCredited = referrerCredited,
                        ReferrerRewardSmsSent = referrerSmsSent
                    },
                    successMessage,
                    201);
            }
            catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(dbEx, "ثبت تکراری مصرف کد {Code} برای کاربر {UserId}", request.Code, userId);
                return ApiResponse<RedeemReferralCodeResponseDto>.BadRequest(
                    "این تراکنش قبلاً ثبت شده است",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطا در تراکنش مصرف کد {Code} برای کاربر {UserId}", request.Code, userId);
                throw;
            }
        }

        private async Task<string?> ValidateRedeemAbuseGuardsAsync(
            int userId,
            string personalCode,
            int customerContactId,
            int programId)
        {
            var now = DateTime.UtcNow;
            var dayStart = now.Date;
            var dayEnd = dayStart.AddDays(1);

            var sameCustomerToday = await _context.ReferralUsages.AnyAsync(u =>
                u.UserId == userId &&
                u.PublicCode == personalCode &&
                u.CustomerContactId == customerContactId &&
                u.Status == ReferralUsageStatuses.Completed &&
                u.CreatedAt >= dayStart &&
                u.CreatedAt < dayEnd);

            if (sameCustomerToday)
            {
                return "این کد امروز برای این خریدار قبلاً ثبت شده است";
            }

            var redeemsToday = await _context.ReferralUsages.CountAsync(u =>
                u.UserId == userId &&
                u.PublicCode == personalCode &&
                u.Status == ReferralUsageStatuses.Completed &&
                u.CreatedAt >= dayStart &&
                u.CreatedAt < dayEnd);

            if (redeemsToday >= MaxRedeemsPerCodePerDay)
            {
                return "سقف مجاز مصرف این کد در امروز تکمیل شده است";
            }

            // جلوگیری از مصرف کد برنامه حذف‌شده/غیرفعال از مسیرهای موازی
            var programStillOk = await _context.ReferralPrograms.AnyAsync(p =>
                p.Id == programId &&
                p.UserId == userId &&
                !p.IsDeleted &&
                p.IsActive);

            if (!programStillOk)
            {
                return "برنامه پاداش قابل استفاده نیست";
            }

            return null;
        }

        private async Task<bool> AreContactsSameMobileAsync(int contactIdA, int contactIdB)
        {
            var mobiles = await _context.Contacts
                .AsNoTracking()
                .Where(c => (c.Id == contactIdA || c.Id == contactIdB) && !c.IsDeleted)
                .Select(c => c.MobileNumber)
                .ToListAsync();

            if (mobiles.Count < 2)
            {
                return false;
            }

            var normalized = mobiles
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return normalized.Count == 1;
        }

        private static decimal CapRewardAmount(decimal amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            return amount > MaxFixedAmount ? MaxFixedAmount : amount;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("IX_ReferralUsages_UserId_IdempotencyKey", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ApiResponse<ReferralContactCodeListDto>> GetContactCodesAsync(
            int programId,
            int userId,
            int pageNumber = 1,
            int pageSize = 20)
        {
            try
            {
                var program = await _programRepository.GetByIdAndUserIdAsync(programId, userId);
                if (program == null)
                {
                    return ApiResponse<ReferralContactCodeListDto>.NotFound("برنامه پاداش یافت نشد");
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var codes = await _contactCodeRepository.GetByProgramIdAsync(programId, userId, pageNumber, pageSize);
                var totalCount = await _contactCodeRepository.GetCountByProgramIdAsync(programId, userId);
                var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

                var dtos = codes.Select(c => new ReferralContactCodeDto
                {
                    Id = c.Id,
                    ReferralProgramId = c.ReferralProgramId,
                    ContactId = c.ContactId,
                    ContactName = c.Contact?.FullName ?? string.Empty,
                    ContactMobile = c.Contact?.MobileNumber ?? string.Empty,
                    Code = c.Code,
                    CreatedAt = EnsureUtc(c.CreatedAt)
                }).ToList();

                return ApiResponse<ReferralContactCodeListDto>.CreateSuccess(new ReferralContactCodeListDto
                {
                    Codes = dtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت کدهای شخصی برنامه {ProgramId} برای کاربر {UserId}", programId, userId);
                return ApiResponse<ReferralContactCodeListDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ReferralUsageHistoryListDto>> GetHistoryAsync(
            int programId,
            int userId,
            int pageNumber = 1,
            int pageSize = 10,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            try
            {
                var program = await _programRepository.GetByIdAndUserIdAsync(programId, userId);
                if (program == null)
                {
                    return ApiResponse<ReferralUsageHistoryListDto>.NotFound("برنامه پاداش یافت نشد");
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var normalizedFromDate = EnsureUtc(fromDate);
                var normalizedToDate = EnsureUtc(toDate);

                if (normalizedFromDate.HasValue && normalizedToDate.HasValue &&
                    normalizedFromDate > normalizedToDate)
                {
                    return ApiResponse<ReferralUsageHistoryListDto>.BadRequest(
                        "تاریخ شروع فیلتر نمی‌تواند بعد از تاریخ پایان باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var usages = await _usageRepository.GetByProgramIdAsync(
                    programId, userId, pageNumber, pageSize, normalizedFromDate, normalizedToDate);
                var totalCount = await _usageRepository.GetCountByProgramIdAsync(
                    programId, userId, normalizedFromDate, normalizedToDate);
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var totals = await _usageRepository.GetTotalsByProgramIdAsync(
                    programId, userId, normalizedFromDate, normalizedToDate);

                var usageDtos = usages.Select(u => MapUsageToDto(u, program.Title)).ToList();

                return ApiResponse<ReferralUsageHistoryListDto>.CreateSuccess(new ReferralUsageHistoryListDto
                {
                    Usages = usageDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    TotalCustomerDiscount = totals.CustomerTotal,
                    FormattedTotalCustomerDiscount = $"{totals.CustomerTotal:N0} تومان",
                    TotalReferrerReward = totals.ReferrerTotal,
                    FormattedTotalReferrerReward = $"{totals.ReferrerTotal:N0} تومان"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت تاریخچه برنامه پاداش {ProgramId} برای کاربر {UserId}", programId, userId);
                return ApiResponse<ReferralUsageHistoryListDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ReferralProgramDto>> UpdateProgramAsync(
            int id,
            int userId,
            UpdateReferralProgramDto updateDto)
        {
            try
            {
                var program = await _programRepository.GetByIdAndUserIdAsync(id, userId);
                if (program == null)
                {
                    return ApiResponse<ReferralProgramDto>.NotFound("برنامه پاداش یافت نشد");
                }

                if (!HasUpdateChanges(updateDto))
                {
                    return ApiResponse<ReferralProgramDto>.BadRequest(
                        "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (!string.IsNullOrWhiteSpace(updateDto.Title))
                {
                    var title = updateDto.Title.Trim();
                    if (await _programRepository.ExistsByTitleAsync(userId, title, id))
                    {
                        return ApiResponse<ReferralProgramDto>.BadRequest("برنامه‌ای با این نام قبلاً ثبت شده است");
                    }

                    program.Title = title;
                }

                if (updateDto.IsActive.HasValue)
                {
                    program.IsActive = updateDto.IsActive.Value;
                }

                if (updateDto.IsReferrerRewardActive.HasValue)
                {
                    program.IsReferrerRewardActive = updateDto.IsReferrerRewardActive.Value;
                    if (!program.IsReferrerRewardActive)
                    {
                        program.ReferrerRewardValue = 0;
                    }
                }

                if (updateDto.ReferrerRewardValue.HasValue)
                {
                    if (!program.IsReferrerRewardActive)
                    {
                        return ApiResponse<ReferralProgramDto>.BadRequest("پاداش معرف غیرفعال است");
                    }

                    var referrerErrors = ValidateRewardValue(program.RewardType, updateDto.ReferrerRewardValue.Value, "پاداش معرف");
                    if (referrerErrors.Any())
                    {
                        return ApiResponse<ReferralProgramDto>.BadRequest("خطا در اعتبارسنجی", referrerErrors, ErrorCodes.ValidationFailed);
                    }

                    program.ReferrerRewardValue = updateDto.ReferrerRewardValue.Value;
                }

                if (updateDto.IsCustomerRewardActive.HasValue)
                {
                    program.IsCustomerRewardActive = updateDto.IsCustomerRewardActive.Value;
                    if (!program.IsCustomerRewardActive)
                    {
                        program.CustomerRewardValue = null;
                    }
                }

                if (updateDto.CustomerRewardValue.HasValue)
                {
                    if (!program.IsCustomerRewardActive)
                    {
                        return ApiResponse<ReferralProgramDto>.BadRequest("پاداش مشتری غیرفعال است");
                    }

                    var customerErrors = ValidateRewardValue(program.RewardType, updateDto.CustomerRewardValue.Value, "پاداش مشتری");
                    if (customerErrors.Any())
                    {
                        return ApiResponse<ReferralProgramDto>.BadRequest("خطا در اعتبارسنجی", customerErrors, ErrorCodes.ValidationFailed);
                    }

                    program.CustomerRewardValue = updateDto.CustomerRewardValue.Value;
                }

                if (!program.IsReferrerRewardActive && !program.IsCustomerRewardActive)
                {
                    return ApiResponse<ReferralProgramDto>.BadRequest(
                        "حداقل یکی از پاداش معرف یا مشتری باید فعال باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (updateDto.EndDate.HasValue)
                {
                    var endDate = NormalizeIncomingUtc(updateDto.EndDate.Value);

                    if (endDate <= program.StartDate)
                    {
                        return ApiResponse<ReferralProgramDto>.BadRequest("تاریخ پایان باید بعد از تاریخ شروع باشد");
                    }

                    program.EndDate = endDate;
                }

                program.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Referral,
                    Action = AuditActions.ReferralProgramUpdated,
                    EntityType = AuditEntityTypes.ReferralProgram,
                    EntityId = program.Id.ToString(),
                    ActorUserId = userId,
                    After = new { title = program.Title, isActive = program.IsActive }
                });

                var codesCount = await _contactCodeRepository.GetCountByProgramIdAsync(id, userId);
                return ApiResponse<ReferralProgramDto>.CreateSuccess(MapToDto(program, codesCount), "برنامه پاداش با موفقیت به‌روزرسانی شد");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "خطای دیتابیس در به‌روزرسانی برنامه پاداش {ProgramId} برای کاربر {UserId}", id, userId);
                return ApiResponse<ReferralProgramDto>.InternalServerError(
                    ControlledErrorHelper.Database,
                    ErrorCodes.DatabaseError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی برنامه پاداش {ProgramId} برای کاربر {UserId}", id, userId);
                return ApiResponse<ReferralProgramDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        #region Helpers

        private static bool HasUpdateChanges(UpdateReferralProgramDto updateDto)
        {
            return !string.IsNullOrWhiteSpace(updateDto.Title)
                || updateDto.IsActive.HasValue
                || updateDto.IsReferrerRewardActive.HasValue
                || updateDto.ReferrerRewardValue.HasValue
                || updateDto.IsCustomerRewardActive.HasValue
                || updateDto.CustomerRewardValue.HasValue
                || updateDto.EndDate.HasValue;
        }

        private static string BuildRedeemSuccessMessage(
            bool customerCredited,
            bool referrerCredited,
            int? customerContactId,
            int? referrerContactId,
            decimal customerDiscount,
            decimal referrerReward,
            bool referrerSmsSent)
        {
            var message = "مصرف کد با موفقیت ثبت شد";

            if (customerContactId.HasValue && customerDiscount > 0 && !customerCredited)
            {
                message += "؛ واریز تخفیف به مشتری انجام نشد";
            }

            if (referrerContactId.HasValue && referrerReward > 0 && !referrerCredited)
            {
                message += "؛ واریز پاداش به معرف انجام نشد";
            }

            if (referrerReward > 0 && !referrerSmsSent)
            {
                message += "؛ ارسال پیامک پاداش به معرف انجام نشد";
            }

            return message;
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

        private static DateTime NormalizeIncomingUtc(DateTime value)
        {
            return EnsureUtc(value);
        }

        private static List<string> ValidateStep1Fields(ReferralStep1Dto step1Dto)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(step1Dto.Title))
            {
                errors.Add("نام برنامه پاداش الزامی است");
            }

            if (step1Dto.RewardType != ReferralRewardTypes.Percentage &&
                step1Dto.RewardType != ReferralRewardTypes.FixedAmount)
            {
                errors.Add("نوع پاداش نامعتبر است");
                return errors;
            }

            if (!step1Dto.IsReferrerRewardActive && !step1Dto.IsCustomerRewardActive)
            {
                errors.Add("حداقل یکی از پاداش معرف یا مشتری باید فعال باشد");
                return errors;
            }

            if (step1Dto.IsReferrerRewardActive)
            {
                if (step1Dto.RewardType == ReferralRewardTypes.Percentage)
                {
                    if (step1Dto.ReferrerRewardValue <= 0 || step1Dto.ReferrerRewardValue > MaxPercentage)
                    {
                        errors.Add("درصد پاداش معرف باید بین 1 تا 100 باشد");
                    }
                }
                else if (step1Dto.ReferrerRewardValue < MinFixedAmount || step1Dto.ReferrerRewardValue > MaxFixedAmount)
                {
                    errors.Add("مبلغ پاداش معرف باید بین 1,000 تا 10,000,000 تومان باشد");
                }
            }

            if (step1Dto.IsCustomerRewardActive)
            {
                if (!step1Dto.CustomerRewardValue.HasValue)
                {
                    errors.Add("مقدار پاداش مشتری الزامی است");
                }
                else if (step1Dto.RewardType == ReferralRewardTypes.Percentage)
                {
                    if (step1Dto.CustomerRewardValue <= 0 || step1Dto.CustomerRewardValue > MaxPercentage)
                    {
                        errors.Add("درصد پاداش مشتری باید بین 1 تا 100 باشد");
                    }
                }
                else if (step1Dto.CustomerRewardValue < MinFixedAmount || step1Dto.CustomerRewardValue > MaxFixedAmount)
                {
                    errors.Add("مبلغ پاداش مشتری باید بین 1,000 تا 10,000,000 تومان باشد");
                }
            }

            if (step1Dto.RewardType == ReferralRewardTypes.Percentage &&
                step1Dto.IsReferrerRewardActive &&
                step1Dto.IsCustomerRewardActive &&
                step1Dto.CustomerRewardValue.HasValue)
            {
                var totalPercent = step1Dto.ReferrerRewardValue + step1Dto.CustomerRewardValue.Value;
                if (totalPercent > MaxPercentage)
                {
                    errors.Add("جمع درصد پاداش معرف و مشتری نمی‌تواند بیشتر از ۱۰۰ باشد");
                }
            }

            return errors;
        }

        private async Task<List<string>> ValidateStep2TagFieldsAsync(int userId, ReferralStep2Dto step2Dto)
        {
            var errors = new List<string>();

            if (!step2Dto.SendToSpecificTags)
            {
                return errors;
            }

            if (step2Dto.TargetTagIds == null || !step2Dto.TargetTagIds.Any())
            {
                errors.Add("حداقل یک تگ باید انتخاب شود");
                return errors;
            }

            var validTags = await _context.MessageTags
                .Where(t => step2Dto.TargetTagIds.Contains(t.Id) && t.UserId == userId && !t.IsDeleted && t.IsActive)
                .CountAsync();

            if (validTags != step2Dto.TargetTagIds.Count)
            {
                errors.Add("برخی تگ‌های انتخاب‌شده نامعتبر هستند");
            }

            return errors;
        }

        private static List<string> ValidateStep3Fields(SaveReferralStep3SettingsDto settings)
        {
            var errors = new List<string>();

            var startDate = NormalizeIncomingUtc(settings.StartDate);
            if (settings.EndDate.HasValue)
            {
                var endDate = NormalizeIncomingUtc(settings.EndDate.Value);
                if (endDate <= startDate)
                {
                    errors.Add("تاریخ پایان باید بعد از تاریخ شروع باشد");
                }
            }

            return errors;
        }

        private async Task<(List<string> Errors, int TotalContactsCount, string Description)> ValidateAndCountAudienceAsync(
            int userId,
            ReferralStep2Dto step2Dto)
        {
            var errors = new List<string>();
            var description = string.Empty;
            var count = 0;

            if (step2Dto.TargetAudience == ReferralTargetAudience.All)
            {
                count = await CountAllContactsAsync(userId);
                description = "همه مخاطبین";
            }
            else if (step2Dto.TargetAudience == ReferralTargetAudience.SpecificNotebooks)
            {
                if (step2Dto.TargetNotebookIds == null || !step2Dto.TargetNotebookIds.Any())
                {
                    errors.Add("حداقل یک دفترچه باید انتخاب شود");
                }
                else
                {
                    var userNotebookIds = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    var invalid = step2Dto.TargetNotebookIds.Where(id => !userNotebookIds.Contains(id)).ToList();
                    if (invalid.Any())
                    {
                        errors.Add($"دفترچه‌های نامعتبر: {string.Join(", ", invalid)}");
                    }
                    else
                    {
                        count = await _context.Contacts
                            .Where(c => step2Dto.TargetNotebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                            .CountAsync();

                        var names = await _context.ContactNotebooks
                            .Where(cn => step2Dto.TargetNotebookIds.Contains(cn.Id))
                            .Select(cn => cn.Name)
                            .ToListAsync();
                        description = string.Join("، ", names);
                    }
                }
            }
            else if (step2Dto.TargetAudience == ReferralTargetAudience.Individual)
            {
                var selectedIds = NormalizeContactIds(step2Dto.TargetContactIds);
                step2Dto.TargetContactIds = selectedIds;

                if (selectedIds.Count == 0)
                {
                    errors.Add("حداقل یک مخاطب باید انتخاب شود");
                }
                else
                {
                    var userNotebookIds = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    var validContacts = await _context.Contacts
                        .Where(c => selectedIds.Contains(c.Id) &&
                                    userNotebookIds.Contains(c.ContactNotebookId) &&
                                    !c.IsDeleted)
                        .Select(c => c.Id)
                        .ToListAsync();

                    var invalidIds = selectedIds.Where(id => !validContacts.Contains(id)).ToList();
                    if (invalidIds.Count > 0)
                    {
                        errors.Add($"برخی مخاطبین انتخاب‌شده نامعتبر هستند یا متعلق به شما نیستند: [{string.Join(", ", invalidIds)}]");
                    }
                    else
                    {
                        count = validContacts.Count;
                        description = $"انتخاب دستی ({count} مخاطب)";
                    }
                }
            }

            if (count == 0 && !errors.Any())
            {
                errors.Add("هیچ مخاطبی برای ارسال یافت نشد");
            }

            return (errors, count, description);
        }

        private async Task<int> CountAllContactsAsync(int userId)
        {
            var notebookIds = await _context.ContactNotebooks
                .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                .Select(cn => cn.Id)
                .ToListAsync();

            return await _context.Contacts
                .Where(c => notebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                .CountAsync();
        }

        private async Task<List<int>> GetContactIdsAsync(
            int userId,
            ReferralStep2Dto step2Dto,
            List<int>? targetTagIds,
            bool sendToSpecificTags)
        {
            IQueryable<Contact> query = _context.Contacts.Where(c => !c.IsDeleted);

            var userNotebookIds = await _context.ContactNotebooks
                .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                .Select(cn => cn.Id)
                .ToListAsync();

            query = query.Where(c => userNotebookIds.Contains(c.ContactNotebookId));

            if (step2Dto.TargetAudience == ReferralTargetAudience.SpecificNotebooks &&
                step2Dto.TargetNotebookIds != null &&
                step2Dto.TargetNotebookIds.Any())
            {
                query = query.Where(c => step2Dto.TargetNotebookIds.Contains(c.ContactNotebookId));
            }
            else if (step2Dto.TargetAudience == ReferralTargetAudience.Individual)
            {
                var selectedIds = NormalizeContactIds(step2Dto.TargetContactIds);
                if (selectedIds.Count == 0)
                {
                    return new List<int>();
                }

                query = query.Where(c => selectedIds.Contains(c.Id));
            }

            if (sendToSpecificTags && targetTagIds != null && targetTagIds.Any())
            {
                var taggedContactIds = await _context.ContactTags
                    .Where(ct => targetTagIds.Contains(ct.TagId))
                    .Select(ct => ct.ContactId)
                    .Distinct()
                    .ToListAsync();

                query = query.Where(c => taggedContactIds.Contains(c.Id));
            }

            return await query.Select(c => c.Id).ToListAsync();
        }

        private async Task<ReferralSummaryDto> BuildSummaryAsync(
            int userId,
            ReferralStep1Dto step1,
            ReferralStep2Dto step2,
            SaveReferralStep3SettingsDto? step3)
        {
            var contactIds = await GetContactIdsAsync(
                userId,
                step2,
                step2.SendToSpecificTags ? step2.TargetTagIds : null,
                step2.SendToSpecificTags);

            var rewardTypeLabel = step1.RewardType == ReferralRewardTypes.Percentage ? "درصدی" : "مبلغ ثابت";
            var referrerReward = step1.IsReferrerRewardActive && step1.ReferrerRewardValue > 0
                ? FormatRewardValue(step1.RewardType, step1.ReferrerRewardValue)
                : "غیرفعال";
            var customerReward = step1.IsCustomerRewardActive && step1.CustomerRewardValue.HasValue
                ? FormatRewardValue(step1.RewardType, step1.CustomerRewardValue.Value)
                : "غیرفعال";

            var audience = GetAudienceDescription(step2);
            if (step2.SendToSpecificTags && step2.TargetTagIds != null && step2.TargetTagIds.Any())
            {
                audience += $" + {step2.TargetTagIds.Count} تگ";
            }

            return new ReferralSummaryDto
            {
                ProgramTitle = step1.Title,
                RewardType = rewardTypeLabel,
                ReferrerReward = referrerReward,
                CustomerReward = customerReward,
                StartDate = step3 != null ? FormatPersianDate(NormalizeIncomingUtc(step3.StartDate)) : "-",
                EndDate = step3?.EndDate != null
                    ? FormatPersianDate(NormalizeIncomingUtc(step3.EndDate.Value))
                    : "بدون پایان",
                Audience = audience,
                ContactsCount = contactIds.Count
            };
        }

        private static string GetAudienceDescription(ReferralStep2Dto step2)
        {
            return step2.TargetAudience switch
            {
                ReferralTargetAudience.All => "همه مخاطبین",
                ReferralTargetAudience.SpecificNotebooks => step2.TargetNotebookIds != null
                    ? $"دفترچه خاص ({step2.TargetNotebookIds.Count} دفترچه)"
                    : "دفترچه خاص",
                ReferralTargetAudience.Individual =>
                    NormalizeContactIds(step2.TargetContactIds) is { Count: > 0 } ids
                        ? $"انتخاب دستی ({ids.Count} مخاطب)"
                        : "انتخاب دستی",
                _ => "همه مخاطبین"
            };
        }

        private static List<int> NormalizeContactIds(IEnumerable<int>? contactIds)
        {
            if (contactIds == null)
            {
                return new List<int>();
            }

            return contactIds.Where(id => id > 0).Distinct().ToList();
        }

        private async Task<List<ReferralContactCode>> CreatePersonalCodesAsync(
            ReferralProgram program,
            List<int> contactIds)
        {
            var codes = new List<ReferralContactCode>();
            var now = DateTime.UtcNow;

            foreach (var contactId in contactIds.Distinct())
            {
                var code = await GenerateUniquePersonalCodeAsync(program.UserId);
                var entity = new ReferralContactCode
                {
                    ReferralProgramId = program.Id,
                    UserId = program.UserId,
                    ContactId = contactId,
                    Code = code,
                    CreatedAt = now
                };
                codes.Add(entity);
            }

            await _context.ReferralContactCodes.AddRangeAsync(codes);
            await _context.SaveChangesAsync();
            return codes;
        }

        private async Task<(int SentCount, int FailedCount)> SendReferralInviteSmsAsync(
            ReferralProgram program,
            List<ReferralContactCode> personalCodes)
        {
            if (!personalCodes.Any())
            {
                return (0, 0);
            }

            var contactIds = personalCodes.Select(c => c.ContactId).Distinct().ToList();
            var contacts = await _context.Contacts
                .Where(c => contactIds.Contains(c.Id) && !c.IsDeleted)
                .ToDictionaryAsync(c => c.Id);

            var sent = 0;
            var failed = 0;

            foreach (var personalCode in personalCodes)
            {
                if (!contacts.TryGetValue(personalCode.ContactId, out var contact) ||
                    string.IsNullOrWhiteSpace(contact.MobileNumber))
                {
                    failed++;
                    continue;
                }

                var message = BuildReferralInviteSmsMessage(program, personalCode.Code);

                try
                {
                    var sendResult = await _userSmsBilling.TrySendAsync(
                        program.UserId,
                        contact.MobileNumber,
                        message,
                        SmsSourceModules.ReferralProgram,
                        "برنامه پاداش",
                        $"هزینه پیامک برنامه پاداش «{program.Title}»",
                        program.Id,
                        program.Title ?? $"برنامه پاداش #{program.Id}");

                    if (sendResult.Sent)
                    {
                        sent++;
                    }
                    else
                    {
                        if (sendResult.SkippedInsufficientBalance)
                        {
                            _logger.LogInformation(
                                "Referral invite SMS skipped (insufficient wallet) for contact {ContactId}, program {ProgramId}",
                                contact.Id, program.Id);
                        }

                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خطا در ارسال پیامک دعوت پاداش به مخاطب {ContactId}", contact.Id);
                    failed++;
                }
            }

            return (sent, failed);
        }

        private async Task<bool> SendReferrerRewardSmsAsync(
            ReferralProgram program,
            int referrerContactId,
            string? mobile,
            decimal rewardAmount)
        {
            var phone = mobile;
            if (string.IsNullOrWhiteSpace(phone))
            {
                phone = await _context.Contacts
                    .AsNoTracking()
                    .Where(c => c.Id == referrerContactId && !c.IsDeleted)
                    .Select(c => c.MobileNumber)
                    .FirstOrDefaultAsync();
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                _logger.LogWarning(
                    "شماره موبایل معرف {ContactId} برای ارسال پیامک پاداش یافت نشد — ProgramId: {ProgramId}",
                    referrerContactId, program.Id);
                return false;
            }

            var message =
                $"شما {rewardAmount:N0} تومان پاداش گرفتید چون کسی با کد معرف شما خرید کرده است.\n" +
                $"برنامه: «{program.Title}»\n" +
                "برای دریافت پاداش به فروشگاه مراجعه کنید.";

            try
            {
                var sendResult = await _userSmsBilling.TrySendAsync(
                    program.UserId,
                    phone,
                    message,
                    SmsSourceModules.ReferralProgram,
                    "پاداش معرف",
                    $"هزینه پیامک پاداش معرف «{program.Title}»",
                    program.Id,
                    program.Title ?? $"برنامه پاداش #{program.Id}");

                if (!sendResult.Sent)
                {
                    _logger.LogInformation(
                        "Referrer reward SMS not sent for contact {ContactId}, program {ProgramId}, skippedBalance={Skipped}",
                        referrerContactId, program.Id, sendResult.SkippedInsufficientBalance);
                }

                return sendResult.Sent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در ارسال پیامک پاداش معرف به مخاطب {ContactId}", referrerContactId);
                return false;
            }
        }

        private static string BuildReferralInviteSmsMessage(ReferralProgram program, string personalCode)
        {
            var parts = new List<string>
            {
                $"برنامه پاداش «{program.Title}»",
                $"کد معرف شما: {personalCode}"
            };

            if (program.IsCustomerRewardActive && program.CustomerRewardValue.HasValue)
            {
                parts.Add($"تخفیف مشتری: {FormatRewardValue(program.RewardType, program.CustomerRewardValue.Value)}");
            }

            if (program.IsReferrerRewardActive && program.ReferrerRewardValue > 0)
            {
                parts.Add($"پاداش معرف: {FormatRewardValue(program.RewardType, program.ReferrerRewardValue)}");
            }

            parts.Add("کد را به دوستانتان بدهید تا با خرید از فروشگاه، پاداش فعال شود.");
            return string.Join("\n", parts);
        }

        private async Task<string> GenerateUniquePersonalCodeAsync(int userId)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var code = $"REF{Random.Shared.Next(100000, 999999)}";
                var existsInPrograms = await _programRepository.ExistsByPublicCodeAsync(userId, code);
                var existsInPersonal = await _contactCodeRepository.ExistsByCodeAsync(userId, code);
                if (!existsInPrograms && !existsInPersonal)
                {
                    return code;
                }
            }

            return $"REF{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }

        private async Task<string> GenerateUniquePublicCodeAsync(int userId)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var code = $"PRG{Random.Shared.Next(100000, 999999)}";
                if (!await _programRepository.ExistsByPublicCodeAsync(userId, code) &&
                    !await _contactCodeRepository.ExistsByCodeAsync(userId, code))
                {
                    return code;
                }
            }

            return $"PRG{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }

        private static decimal? CalculateRewardAmount(string rewardType, decimal rewardValue, decimal? purchaseAmount)
        {
            if (rewardType == ReferralRewardTypes.Percentage)
            {
                if (!purchaseAmount.HasValue || purchaseAmount <= 0)
                {
                    return null;
                }

                return Math.Round(purchaseAmount.Value * rewardValue / 100m, 0, MidpointRounding.AwayFromZero);
            }

            return rewardValue;
        }

        private sealed class ResolvedReferralCode
        {
            public ReferralProgram Program { get; init; } = null!;
            public string UsedCode { get; init; } = string.Empty;
            public int? ReferrerContactId { get; init; }
            public string? ReferrerContactName { get; init; }
            public string? ReferrerContactMobile { get; init; }
        }

        private async Task<ResolvedReferralCode?> ResolvePersonalCodeAsync(int userId, string code)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return null;
            }

            var personal = await _contactCodeRepository.GetByCodeAsync(userId, normalizedCode);
            if (personal == null)
            {
                return null;
            }

            if (personal.ReferralProgram == null || personal.ReferralProgram.IsDeleted)
            {
                return null;
            }

            if (personal.Contact == null || personal.Contact.IsDeleted)
            {
                return null;
            }

            return new ResolvedReferralCode
            {
                Program = personal.ReferralProgram,
                UsedCode = personal.Code,
                ReferrerContactId = personal.ContactId,
                ReferrerContactName = personal.Contact.FullName,
                ReferrerContactMobile = personal.Contact.MobileNumber
            };
        }

        private async Task<InquireReferralCodeResponseDto> BuildCodeNotFoundResponseAsync(int userId, string? code)
        {
            var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.StartsWith("PRG", StringComparison.Ordinal))
            {
                var program = await _programRepository.GetByPublicCodeAsync(userId, normalized);
                if (program != null)
                {
                    return new InquireReferralCodeResponseDto
                    {
                        IsValid = false,
                        IsActive = program.IsActive,
                        InvalidReason = ReferralProgramValidity.PublicCodeUsedMessage,
                        StatusMessage = ReferralProgramValidity.PublicCodeUsedMessage,
                        ProgramId = program.Id,
                        ProgramName = program.Title,
                        PublicCode = program.PublicCode,
                        RewardType = program.RewardType,
                        StartDate = EnsureUtc(program.StartDate),
                        EndDate = EnsureUtc(program.EndDate)
                    };
                }
            }

            return new InquireReferralCodeResponseDto
            {
                IsValid = false,
                InvalidReason = ReferralProgramValidity.CodeNotFoundMessage,
                StatusMessage = ReferralProgramValidity.CodeNotFoundMessage
            };
        }

        private static (decimal? Amount, string Formatted) BuildCustomerDiscountInfo(
            ReferralProgram program,
            decimal? purchaseAmount)
        {
            if (!program.IsCustomerRewardActive || !program.CustomerRewardValue.HasValue)
            {
                return (null, "پاداش مشتری فعال نیست");
            }

            if (program.RewardType == ReferralRewardTypes.Percentage &&
                (!purchaseAmount.HasValue || purchaseAmount <= 0))
            {
                return (null, $"{program.CustomerRewardValue.Value:N0}% (مبلغ خرید برای محاسبه لازم است)");
            }

            var amount = CalculateRewardAmount(program.RewardType, program.CustomerRewardValue.Value, purchaseAmount) ?? 0;
            return (amount, $"{amount:N0} تومان");
        }

        private static (decimal? Amount, string Formatted) BuildReferrerRewardInfo(
            ReferralProgram program,
            decimal? purchaseAmount)
        {
            if (!program.IsReferrerRewardActive || program.ReferrerRewardValue <= 0)
            {
                return (null, "پاداش معرف فعال نیست");
            }

            if (program.RewardType == ReferralRewardTypes.Percentage &&
                (!purchaseAmount.HasValue || purchaseAmount <= 0))
            {
                return (null, $"{program.ReferrerRewardValue:N0}% (مبلغ خرید برای محاسبه لازم است)");
            }

            var amount = CalculateRewardAmount(program.RewardType, program.ReferrerRewardValue, purchaseAmount) ?? 0;
            return (amount, $"{amount:N0} تومان");
        }

        private async Task<string?> ValidateContactOwnershipAsync(int userId, int contactId)
        {
            var contact = await _context.Contacts
                .Include(c => c.ContactNotebook)
                .FirstOrDefaultAsync(c => c.Id == contactId && !c.IsDeleted);

            if (contact == null)
            {
                return "مخاطب یافت نشد";
            }

            if (contact.ContactNotebook.UserId != userId || contact.ContactNotebook.IsDeleted)
            {
                return "دسترسی به مخاطب انتخاب‌شده مجاز نیست";
            }

            return null;
        }

        private static int GetRewardValidityDays(ReferralProgram program)
        {
            if (program.EndDate.HasValue)
            {
                var days = (int)Math.Ceiling((program.EndDate.Value - DateTime.UtcNow).TotalDays);
                return Math.Max(1, days);
            }

            return 30;
        }

        private static List<string> ValidateRewardValue(string rewardType, decimal value, string fieldLabel)
        {
            var errors = new List<string>();

            if (rewardType == ReferralRewardTypes.Percentage)
            {
                if (value <= 0 || value > MaxPercentage)
                {
                    errors.Add($"درصد {fieldLabel} باید بین 1 تا 100 باشد");
                }
            }
            else if (value < MinFixedAmount || value > MaxFixedAmount)
            {
                errors.Add($"مبلغ {fieldLabel} باید بین 1,000 تا 10,000,000 تومان باشد");
            }

            return errors;
        }

        private async Task<bool> CreditContactCashbackAsync(
            int userId,
            int contactId,
            decimal amount,
            string description,
            int validityDays)
        {
            if (amount <= 0)
            {
                return false;
            }

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == contactId && !c.IsDeleted);

            if (contact == null)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var balance = await _context.ContactCashbackBalances
                .FirstOrDefaultAsync(b => b.ContactId == contactId && b.UserId == userId);

            var balanceBefore = balance?.TotalBalance ?? 0;
            if (balance == null)
            {
                balance = new ContactCashbackBalance
                {
                    ContactId = contactId,
                    UserId = userId,
                    TotalBalance = 0,
                    UsableBalance = 0,
                    CreatedAt = now
                };
                await _context.ContactCashbackBalances.AddAsync(balance);
            }

            var expiryDate = now.AddDays(validityDays);
            await _context.ManualCashbackTransactions.AddAsync(new ManualCashbackTransaction
            {
                ContactId = contactId,
                UserId = userId,
                TransactionType = ManualCashbackTransactionTypes.Add,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceBefore + amount,
                Description = description,
                ExpiryDate = expiryDate,
                ValidityDays = validityDays,
                CreatedAt = now
            });

            balance.TotalBalance = balanceBefore + amount;
            var usableBefore = balance.UsableBalance;
            balance.UsableBalance = usableBefore + amount;
            balance.UpdatedAt = now;

            if (!balance.ExpiryDate.HasValue || expiryDate < balance.ExpiryDate)
            {
                balance.ExpiryDate = expiryDate;
                balance.ExpiryDays = validityDays;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        private static ReferralUsageDto MapUsageToDto(ReferralUsage usage, string programTitle)
        {
            return new ReferralUsageDto
            {
                Id = usage.Id,
                ReferralProgramId = usage.ReferralProgramId,
                ProgramTitle = programTitle,
                PublicCode = usage.PublicCode,
                PurchaseAmount = usage.PurchaseAmount,
                FormattedPurchaseAmount = usage.PurchaseAmount.HasValue ? $"{usage.PurchaseAmount:N0} تومان" : null,
                CustomerDiscountAmount = usage.CustomerDiscountAmount,
                FormattedCustomerDiscount = $"{usage.CustomerDiscountAmount:N0} تومان",
                ReferrerRewardAmount = usage.ReferrerRewardAmount,
                FormattedReferrerReward = $"{usage.ReferrerRewardAmount:N0} تومان",
                CustomerContactId = usage.CustomerContactId,
                CustomerContactName = usage.CustomerContact?.FullName,
                CustomerContactMobile = usage.CustomerContact?.MobileNumber,
                ReferrerContactId = usage.ReferrerContactId,
                ReferrerContactName = usage.ReferrerContact?.FullName,
                ReferrerContactMobile = usage.ReferrerContact?.MobileNumber,
                Status = usage.Status,
                Description = usage.Description,
                CreatedAt = EnsureUtc(usage.CreatedAt)
            };
        }

        private static string FormatRewardValue(string rewardType, decimal value)
        {
            return rewardType == ReferralRewardTypes.Percentage
                ? $"{value:N0}%"
                : $"{value:N0} تومان";
        }

        private static string FormatRewardAmount(string rewardType, decimal amount)
        {
            return rewardType == ReferralRewardTypes.Percentage
                ? $"{amount:N0} تومان"
                : $"{amount:N0} تومان";
        }

        private static string FormatPersianDate(DateTime date)
        {
            return ReferralProgramValidity.FormatPersianDate(date);
        }

        private ReferralProgramDto MapToDto(ReferralProgram program, int personalCodesCount = 0)
        {
            List<int>? notebookIds = null;
            List<int>? contactIds = null;
            List<int>? tagIds = null;

            if (!string.IsNullOrEmpty(program.TargetNotebookIds))
            {
                notebookIds = JsonSerializer.Deserialize<List<int>>(program.TargetNotebookIds);
            }

            if (!string.IsNullOrEmpty(program.TargetContactIds))
            {
                contactIds = JsonSerializer.Deserialize<List<int>>(program.TargetContactIds);
            }

            if (!string.IsNullOrEmpty(program.TargetTagIds))
            {
                tagIds = JsonSerializer.Deserialize<List<int>>(program.TargetTagIds);
            }

            var step2 = new ReferralStep2Dto
            {
                TargetAudience = program.TargetAudience,
                TargetNotebookIds = notebookIds,
                TargetContactIds = contactIds
            };

            return new ReferralProgramDto
            {
                Id = program.Id,
                Title = program.Title,
                IsActive = program.IsActive,
                RewardType = program.RewardType,
                IsReferrerRewardActive = program.IsReferrerRewardActive,
                ReferrerRewardValue = program.ReferrerRewardValue,
                FormattedReferrerReward = program.IsReferrerRewardActive && program.ReferrerRewardValue > 0
                    ? FormatRewardValue(program.RewardType, program.ReferrerRewardValue)
                    : "غیرفعال",
                IsCustomerRewardActive = program.IsCustomerRewardActive,
                CustomerRewardValue = program.CustomerRewardValue,
                FormattedCustomerReward = program.IsCustomerRewardActive && program.CustomerRewardValue.HasValue
                    ? FormatRewardValue(program.RewardType, program.CustomerRewardValue.Value)
                    : null,
                PublicCode = program.PublicCode,
                PersonalCodesCount = personalCodesCount,
                TargetAudience = program.TargetAudience,
                AudienceDescription = GetAudienceDescription(step2),
                TargetNotebookIds = notebookIds,
                TargetContactIds = contactIds,
                TargetTagIds = tagIds,
                SendToSpecificTags = program.SendToSpecificTags,
                StartDate = EnsureUtc(program.StartDate),
                EndDate = EnsureUtc(program.EndDate),
                NotifiedContactsCount = program.NotifiedContactsCount,
                IsCurrentlyValid = ReferralProgramValidity.Evaluate(program).IsValid,
                CreatedAt = EnsureUtc(program.CreatedAt),
                UpdatedAt = EnsureUtc(program.UpdatedAt)
            };
        }

        private async Task<(bool Success, string? ErrorMessage, ReferralProgramDraft? Draft, ReferralStep1Dto? Step1, ReferralStep2Dto? Step2)> LoadDraftStepsAsync(
            int userId,
            string draftId)
        {
            if (string.IsNullOrWhiteSpace(draftId))
            {
                return (false, "شناسه پیش‌نویس الزامی است", null, null, null);
            }

            var draft = await _draftRepository.GetActiveByDraftIdAsync(draftId, userId);
            if (draft == null)
            {
                return (false, "پیش‌نویس یافت نشد یا منقضی شده است", null, null, null);
            }

            var loadedStep1 = JsonSerializer.Deserialize<ReferralStep1Dto>(draft.Step1Data);
            if (loadedStep1 == null)
            {
                return (false, "خطا در خواندن مرحله 1", draft, null, null);
            }

            if (string.IsNullOrEmpty(draft.Step2Data))
            {
                return (false, "مرحله 2 هنوز تکمیل نشده است", draft, loadedStep1, null);
            }

            var loadedStep2 = JsonSerializer.Deserialize<ReferralStep2Dto>(draft.Step2Data);
            if (loadedStep2 == null)
            {
                return (false, "خطا در خواندن مرحله 2", draft, loadedStep1, null);
            }

            return (true, null, draft, loadedStep1, loadedStep2);
        }

        #endregion
    }
}
