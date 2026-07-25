using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Admin
{
    public class AdminSupportTicketService : IAdminSupportTicketService
    {
        private readonly Api_Context _context;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<AdminSupportTicketService> _logger;

        public AdminSupportTicketService(
            Api_Context context,
            IFileUploadService fileUploadService,
            ILogger<AdminSupportTicketService> logger)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        public async Task<ApiResponse<PagedResponse<SupportTicketResponseDto>>> GetAllAsync(
            string? status = null,
            string? priority = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var query = _context.SupportTickets.AsNoTracking()
                    .Where(t => !t.IsDeleted);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(t => t.Status == status.Trim());

                if (!string.IsNullOrWhiteSpace(priority))
                    query = query.Where(t => t.Priority == priority.Trim());

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                    .ThenByDescending(t => t.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new SupportTicketResponseDto
                    {
                        Id = t.Id,
                        TicketNumber = FormatTicketNumber(t.Id),
                        UserId = t.UserId,
                        UserPhoneNumber = t.User.PhoneNumber,
                        UserFullName = t.User.FullName,
                        Subject = t.Subject,
                        Module = t.Module,
                        Status = t.Status,
                        Priority = t.Priority,
                        AssignedToUserId = t.AssignedToUserId,
                        AssignedToName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt,
                        ClosedAt = t.ClosedAt,
                        ReplyCount = t.Messages.Count(m => !m.IsDeleted)
                    })
                    .ToListAsync();

                foreach (var item in items)
                    EnrichLabels(item);

                return ApiResponse<PagedResponse<SupportTicketResponseDto>>.CreateSuccess(
                    PagedResponse<SupportTicketResponseDto>.Create(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading support tickets");
                return ApiResponse<PagedResponse<SupportTicketResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> GetByIdAsync(int id)
        {
            try
            {
                var ticket = await LoadTicketAsync(id);
                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(MapTicket(ticket));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading support ticket {TicketId}", id);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> ReplyAsync(
            int id,
            int adminUserId,
            ReplySupportTicketDto dto,
            IFormFile? attachmentFile = null)
        {
            try
            {
                var content = dto.Content?.Trim() ?? string.Empty;
                var hasAttachment = attachmentFile is { Length: > 0 };

                if (string.IsNullOrWhiteSpace(content) && !hasAttachment)
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("متن یا فایل پاسخ الزامی است");

                var ticket = await _context.SupportTickets
                    .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                if (TicketStatuses.IsClosedLike(ticket.Status))
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("این تیکت بسته شده است؛ ابتدا آن را باز کنید");

                string? attachmentUrl = null;
                if (hasAttachment)
                {
                    var imageValidation = ValidateTicketAttachment(attachmentFile!);
                    if (imageValidation != null)
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(imageValidation);

                    try
                    {
                        attachmentUrl = await UploadTicketAttachmentAsync(attachmentFile!, id);
                    }
                    catch (ArgumentException ex)
                    {
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading ticket reply attachment for ticket {TicketId}", id);
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                    }
                }

                _context.TicketMessages.Add(new TicketMessage
                {
                    TicketId = id,
                    SenderUserId = adminUserId,
                    IsAdminReply = true,
                    Content = string.IsNullOrWhiteSpace(content) ? "📎 فایل پیوست" : content,
                    AttachmentUrl = attachmentUrl,
                    CreatedAt = DateTime.UtcNow
                });

                if (ticket.Status == TicketStatuses.Open)
                    ticket.Status = TicketStatuses.InProgress;

                ticket.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var reloaded = await LoadTicketAsync(id);
                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(MapTicket(reloaded!), "پاسخ ثبت شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replying to ticket {TicketId}", id);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> UpdateStatusAsync(int id, UpdateSupportTicketStatusDto dto)
        {
            try
            {
                if (!TicketStatuses.IsKnown(dto.Status))
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("وضعیت تیکت نامعتبر است", errorCode: ErrorCodes.InvalidInput);

                var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                var newStatus = dto.Status.Trim();
                ticket.Status = newStatus;
                ticket.AssignedToUserId = dto.AssignedToUserId;
                ticket.UpdatedAt = DateTime.UtcNow;

                if (TicketStatuses.IsClosedLike(newStatus))
                    ticket.ClosedAt = DateTime.UtcNow;
                else
                    ticket.ClosedAt = null;

                await _context.SaveChangesAsync();
                var reloaded = await LoadTicketAsync(id);
                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(MapTicket(reloaded!), "وضعیت تیکت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ticket status {TicketId}", id);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<SupportTicket?> LoadTicketAsync(int id)
        {
            return await _context.SupportTickets.AsNoTracking()
                .Include(t => t.User)
                .Include(t => t.AssignedToUser)
                .Include(t => t.Messages.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.SenderUser)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        }

        internal static SupportTicketResponseDto MapTicket(SupportTicket ticket)
        {
            var dto = new SupportTicketResponseDto
            {
                Id = ticket.Id,
                TicketNumber = FormatTicketNumber(ticket.Id),
                UserId = ticket.UserId,
                UserPhoneNumber = ticket.User?.PhoneNumber,
                UserFullName = ticket.User?.FullName,
                Subject = ticket.Subject,
                Module = ticket.Module,
                Status = ticket.Status,
                Priority = ticket.Priority,
                AssignedToUserId = ticket.AssignedToUserId,
                AssignedToName = ticket.AssignedToUser?.FullName,
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt,
                ClosedAt = ticket.ClosedAt,
                ReplyCount = ticket.Messages.Count(m => !m.IsDeleted),
                Messages = ticket.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new TicketMessageResponseDto
                    {
                        Id = m.Id,
                        SenderUserId = m.SenderUserId,
                        SenderName = m.SenderUser?.FullName,
                        IsAdminReply = m.IsAdminReply,
                        Content = m.Content,
                        AttachmentUrl = m.AttachmentUrl,
                        CreatedAt = m.CreatedAt
                    }).ToList()
            };

            EnrichLabels(dto);
            return dto;
        }

        internal static string FormatTicketNumber(int id) => $"TK-{id:D4}";

        internal static void EnrichLabels(SupportTicketResponseDto dto)
        {
            dto.TicketNumber = FormatTicketNumber(dto.Id);
            dto.ModuleFa = TicketModules.GetPersianLabel(dto.Module);
            dto.StatusFa = TicketStatuses.GetPersianLabel(dto.Status);
            dto.PriorityFa = TicketPriorities.GetPersianLabel(dto.Priority);
        }

        internal static string? ValidateTicketAttachment(IFormFile file) =>
            SecureFileValidator.ValidateTicketAttachment(file);

        private async Task<string> UploadTicketAttachmentAsync(IFormFile file, int ticketId)
        {
            var isPdf = file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
            var subFolder = isPdf
                ? FileUploadConstants.SubFolder_Documents
                : FileUploadConstants.SubFolder_Images;

            return await _fileUploadService.UploadFileAsync(
                file,
                FileUploadConstants.EntityType_Ticket,
                ticketId,
                subFolder);
        }
    }
}
