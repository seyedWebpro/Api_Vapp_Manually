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

        public async Task<ApiResponse<PagedResponse<SupportTicketResponseDto>>> GetAllAsync(string? status = null, int page = 1, int pageSize = 20)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var query = _context.SupportTickets.AsNoTracking()
                    .Where(t => !t.IsDeleted);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(t => t.Status == status);

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new SupportTicketResponseDto
                    {
                        Id = t.Id,
                        UserId = t.UserId,
                        UserPhoneNumber = t.User.PhoneNumber,
                        UserFullName = t.User.FullName,
                        Subject = t.Subject,
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

        public async Task<ApiResponse<SupportTicketResponseDto>> ReplyAsync(int id, int adminUserId, ReplySupportTicketDto dto, IFormFile? imageFile = null)
        {
            try
            {
                var content = dto.Content?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(content) && (imageFile == null || imageFile.Length == 0))
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("متن یا تصویر پاسخ الزامی است");

                var ticket = await _context.SupportTickets
                    .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                string? attachmentUrl = null;
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageValidation = ValidateReplyImage(imageFile);
                    if (imageValidation != null)
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(imageValidation);

                    try
                    {
                        attachmentUrl = await _fileUploadService.UploadFileAsync(
                            imageFile,
                            FileUploadConstants.EntityType_Ticket,
                            id,
                            FileUploadConstants.SubFolder_Images);
                    }
                    catch (ArgumentException ex)
                    {
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading ticket reply image for ticket {TicketId}", id);
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                    }
                }

                _context.TicketMessages.Add(new TicketMessage
                {
                    TicketId = id,
                    SenderUserId = adminUserId,
                    IsAdminReply = true,
                    Content = content,
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
                var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                ticket.Status = dto.Status.Trim();
                ticket.AssignedToUserId = dto.AssignedToUserId;
                ticket.UpdatedAt = DateTime.UtcNow;

                if (dto.Status is TicketStatuses.Closed or TicketStatuses.Resolved)
                    ticket.ClosedAt = DateTime.UtcNow;

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

        private static SupportTicketResponseDto MapTicket(SupportTicket ticket) => new()
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            UserPhoneNumber = ticket.User?.PhoneNumber,
            UserFullName = ticket.User?.FullName,
            Subject = ticket.Subject,
            Status = ticket.Status,
            Priority = ticket.Priority,
            AssignedToUserId = ticket.AssignedToUserId,
            AssignedToName = ticket.AssignedToUser?.FullName,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ClosedAt = ticket.ClosedAt,
            ReplyCount = ticket.Messages.Count(m => !m.IsDeleted),
            Messages = ticket.Messages
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

        private static string? ValidateReplyImage(IFormFile imageFile)
        {
            const long maxFileSize = 5 * 1024 * 1024;
            if (imageFile.Length > maxFileSize)
            {
                var fileSizeMB = Math.Round(imageFile.Length / (1024.0 * 1024.0), 2);
                return $"حجم فایل ({fileSizeMB} مگابایت) بیشتر از حد مجاز (5 مگابایت) است";
            }

            var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/png", "image/gif", "image/webp"
            };

            if (!allowedContentTypes.Contains(imageFile.ContentType))
                return "فقط فایل‌های تصویری (JPG, PNG, GIF, WEBP) مجاز هستند";

            return null;
        }
    }

    public class UserSupportTicketService : IUserSupportTicketService
    {
        private readonly Api_Context _context;
        private readonly ILogger<UserSupportTicketService> _logger;

        public UserSupportTicketService(Api_Context context, ILogger<UserSupportTicketService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> CreateAsync(int userId, CreateSupportTicketDto dto)
        {
            try
            {
                var ticket = new SupportTicket
                {
                    UserId = userId,
                    Subject = dto.Subject.Trim(),
                    Priority = string.IsNullOrWhiteSpace(dto.Priority) ? TicketPriorities.Normal : dto.Priority.Trim(),
                    Status = TicketStatuses.Open,
                    CreatedAt = DateTime.UtcNow
                };

                ticket.Messages.Add(new TicketMessage
                {
                    SenderUserId = userId,
                    IsAdminReply = false,
                    Content = dto.Content.Trim(),
                    CreatedAt = DateTime.UtcNow
                });

                _context.SupportTickets.Add(ticket);
                await _context.SaveChangesAsync();

                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                ticket.User = user!;
                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(MapTicket(ticket), "تیکت ایجاد شد", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating support ticket for user {UserId}", userId);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<List<SupportTicketResponseDto>>> GetMyTicketsAsync(int userId)
        {
            try
            {
                var tickets = await _context.SupportTickets.AsNoTracking()
                    .Include(t => t.Messages.Where(m => !m.IsDeleted))
                    .ThenInclude(m => m.SenderUser)
                    .Where(t => t.UserId == userId && !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                return ApiResponse<List<SupportTicketResponseDto>>.CreateSuccess(tickets.Select(MapTicket).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user tickets {UserId}", userId);
                return ApiResponse<List<SupportTicketResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> GetMyTicketByIdAsync(int userId, int ticketId)
        {
            try
            {
                var ticket = await _context.SupportTickets.AsNoTracking()
                    .Include(t => t.Messages.Where(m => !m.IsDeleted))
                    .ThenInclude(m => m.SenderUser)
                    .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId && !t.IsDeleted);

                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(MapTicket(ticket));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user ticket {TicketId}", ticketId);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> ReplyAsync(int userId, int ticketId, ReplySupportTicketDto dto)
        {
            try
            {
                var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId && !t.IsDeleted);
                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                if (ticket.Status is TicketStatuses.Closed or TicketStatuses.Resolved)
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("این تیکت بسته شده است");

                _context.TicketMessages.Add(new TicketMessage
                {
                    TicketId = ticketId,
                    SenderUserId = userId,
                    IsAdminReply = false,
                    Content = dto.Content.Trim(),
                    CreatedAt = DateTime.UtcNow
                });

                ticket.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var reloaded = await _context.SupportTickets.AsNoTracking()
                    .Include(t => t.Messages.Where(m => !m.IsDeleted))
                    .ThenInclude(m => m.SenderUser)
                    .FirstAsync(t => t.Id == ticketId);

                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(MapTicket(reloaded), "پاسخ ثبت شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replying to ticket {TicketId} by user {UserId}", ticketId, userId);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static SupportTicketResponseDto MapTicket(SupportTicket ticket) => new()
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            UserPhoneNumber = ticket.User?.PhoneNumber,
            UserFullName = ticket.User?.FullName,
            Subject = ticket.Subject,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            ClosedAt = ticket.ClosedAt,
            ReplyCount = ticket.Messages.Count(m => !m.IsDeleted),
            Messages = ticket.Messages
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
    }
}
