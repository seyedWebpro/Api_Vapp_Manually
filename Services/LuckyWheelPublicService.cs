using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class LuckyWheelPublicService : ILuckyWheelPublicService
    {
        private readonly ILuckyWheelRepository _luckyWheelRepository;
        private readonly Api_Context _context;
        private readonly PublicPhonebookService _phonebookService;
        private readonly ILogger<LuckyWheelPublicService> _logger;

        public LuckyWheelPublicService(
            ILuckyWheelRepository luckyWheelRepository,
            Api_Context context,
            PublicPhonebookService phonebookService,
            ILogger<LuckyWheelPublicService> logger)
        {
            _luckyWheelRepository = luckyWheelRepository;
            _context = context;
            _phonebookService = phonebookService;
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
                if (wheel == null)
                {
                    return ApiResponse<LuckyWheelPublicDto>.NotFound("گردونه یافت نشد یا غیرفعال است");
                }

                return ApiResponse<LuckyWheelPublicDto>.CreateSuccess(MapToPublicDto(wheel));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public lucky wheel for slug {Slug}", slug);
                return ApiResponse<LuckyWheelPublicDto>.InternalServerError(ControlledErrorHelper.Unexpected);
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

            if (string.IsNullOrWhiteSpace(dto.ParticipantFullName))
            {
                return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                    "نام الزامی است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            var mobile = BookingMobileHelper.Normalize(dto.ParticipantMobile);
            if (!BookingMobileHelper.IsValidIranianMobile(mobile))
            {
                return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                    "شماره موبایل نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var wheel = await _context.LuckyWheels
                    .AsSplitQuery()
                    .Include(w => w.Items.OrderBy(item => item.DisplayOrder))
                    .Include(w => w.Notebooks)
                    .FirstOrDefaultAsync(w =>
                        w.Slug == normalizedSlug &&
                        !w.IsDeleted &&
                        w.Status == LuckyWheelStatus.Published &&
                        w.IsActive);

                if (wheel == null)
                {
                    return ApiResponse<SpinLuckyWheelPublicResponseDto>.NotFound("گردونه یافت نشد یا غیرفعال است");
                }

                if (wheel.Items.Count == 0)
                {
                    return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                        "گردونه آماده چرخش نیست",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (await _luckyWheelRepository.HasParticipantWithMobileAsync(wheel.Id, mobile))
                {
                    return ApiResponse<SpinLuckyWheelPublicResponseDto>.BadRequest(
                        "این شماره قبلاً در این گردونه شرکت کرده است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var wonItem = LuckyWheelSpinHelper.PickWeightedItem(wheel.Items.ToList());
                var now = DateTime.UtcNow;

                var participant = new LuckyWheelParticipant
                {
                    LuckyWheelId = wheel.Id,
                    ParticipantFullName = dto.ParticipantFullName.Trim(),
                    ParticipantMobile = mobile,
                    WonLuckyWheelItemId = wonItem.Id,
                    CreatedAt = now
                };

                await _context.LuckyWheelParticipants.AddAsync(participant);
                await _context.SaveChangesAsync();

                if (wheel.SaveToPhonebook && wheel.Notebooks.Count > 0)
                {
                    var notebookIds = wheel.Notebooks.Select(n => n.ContactNotebookId).ToList();
                    var contactId = await _phonebookService.SaveParticipantAsync(
                        notebookIds,
                        mobile,
                        participant.ParticipantFullName);

                    if (contactId.HasValue)
                    {
                        participant.ContactId = contactId;
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Public wheel spin {ParticipantId} for wheel {WheelId}, won item {ItemId}",
                    participant.Id,
                    wheel.Id,
                    wonItem.Id);

                return ApiResponse<SpinLuckyWheelPublicResponseDto>.CreateSuccess(
                    new SpinLuckyWheelPublicResponseDto
                    {
                        ParticipantId = participant.Id,
                        WonItemId = wonItem.Id,
                        WonItemName = wonItem.Name
                    },
                    "چرخش با موفقیت ثبت شد",
                    201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error spinning public lucky wheel for slug {Slug}", slug);
                return ApiResponse<SpinLuckyWheelPublicResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
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
