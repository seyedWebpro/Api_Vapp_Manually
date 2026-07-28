using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Admin;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    public class UserSupportTicketService : IUserSupportTicketService
    {
        private readonly Api_Context _context;
        private readonly IFileUploadService _fileUploadService;
        private readonly IAuditService _audit;
        private readonly ILogger<UserSupportTicketService> _logger;

        public UserSupportTicketService(
            Api_Context context,
            IFileUploadService fileUploadService,
            IAuditService audit,
            ILogger<UserSupportTicketService> logger)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<SupportTicketStatsDto>> GetMyStatsAsync(int userId)
        {
            try
            {
                var tickets = await _context.SupportTickets.AsNoTracking()
                    .Where(t => t.UserId == userId && !t.IsDeleted)
                    .Select(t => t.Status)
                    .ToListAsync();

                var stats = new SupportTicketStatsDto
                {
                    TotalCount = tickets.Count,
                    WaitingForResponseCount = tickets.Count(s => s == TicketStatuses.Open),
                    AnsweredCount = tickets.Count(s => s == TicketStatuses.InProgress),
                    ClosedCount = tickets.Count(s => TicketStatuses.IsClosedLike(s))
                };

                return ApiResponse<SupportTicketStatsDto>.CreateSuccess(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ticket stats for user {UserId}", userId);
                return ApiResponse<SupportTicketStatsDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public Task<ApiResponse<List<TicketModuleOptionDto>>> GetModulesAsync()
        {
            var modules = TicketModules.PersianLabels
                .Select(kv => new TicketModuleOptionDto
                {
                    Code = kv.Key,
                    TitleFa = kv.Value
                })
                .OrderBy(m => m.TitleFa)
                .ToList();

            return Task.FromResult(ApiResponse<List<TicketModuleOptionDto>>.CreateSuccess(modules));
        }

        public async Task<ApiResponse<PagedResponse<SupportTicketResponseDto>>> GetMyTicketsAsync(
            int userId,
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
                    .Where(t => t.UserId == userId && !t.IsDeleted);

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
                        TicketNumber = AdminSupportTicketService.FormatTicketNumber(t.Id),
                        UserId = t.UserId,
                        Subject = t.Subject,
                        Module = t.Module,
                        Status = t.Status,
                        Priority = t.Priority,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt,
                        ClosedAt = t.ClosedAt,
                        ReplyCount = t.Messages.Count(m => !m.IsDeleted)
                    })
                    .ToListAsync();

                foreach (var item in items)
                    AdminSupportTicketService.EnrichLabels(item);

                return ApiResponse<PagedResponse<SupportTicketResponseDto>>.CreateSuccess(
                    PagedResponse<SupportTicketResponseDto>.Create(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user tickets {UserId}", userId);
                return ApiResponse<PagedResponse<SupportTicketResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> GetMyTicketByIdAsync(int userId, int ticketId)
        {
            try
            {
                var ticket = await LoadUserTicketAsync(userId, ticketId);
                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(AdminSupportTicketService.MapTicket(ticket));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user ticket {TicketId}", ticketId);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> CreateAsync(
            int userId,
            CreateSupportTicketDto dto,
            IFormFile? attachmentFile = null)
        {
            string? uploadedPath = null;
            int? createdTicketId = null;

            try
            {
                _logger.LogInformation("Creating support ticket for user {UserId}", userId);

                if (!TicketModules.IsKnown(dto.Module))
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("ماژول انتخاب‌شده نامعتبر است", errorCode: ErrorCodes.InvalidInput);

                var priority = TicketPriorities.Normalize(dto.Priority);
                var hasAttachment = attachmentFile is { Length: > 0 };

                if (hasAttachment)
                {
                    var validation = AdminSupportTicketService.ValidateTicketAttachment(attachmentFile!);
                    if (validation != null)
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(validation);
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

                var ticket = new SupportTicket
                {
                    UserId = userId,
                    Subject = dto.Subject.Trim(),
                    Module = TicketModules.Normalize(dto.Module),
                    Priority = priority,
                    Status = TicketStatuses.Open,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var firstMessage = new TicketMessage
                {
                    SenderUserId = userId,
                    IsAdminReply = false,
                    Content = dto.Content.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                ticket.Messages.Add(firstMessage);

                _context.SupportTickets.Add(ticket);
                await _context.SaveChangesAsync();
                createdTicketId = ticket.Id;

                if (hasAttachment)
                {
                    try
                    {
                        uploadedPath = await UploadTicketAttachmentAsync(attachmentFile!, ticket.Id);
                        firstMessage.AttachmentUrl = uploadedPath;
                        firstMessage.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    catch (ArgumentException ex)
                    {
                        await transaction.RollbackAsync();
                        await SafeDeleteTicketFilesAsync(ticket.Id);
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading attachment for new ticket {TicketId}", ticket.Id);
                        await transaction.RollbackAsync();
                        await SafeDeleteTicketFilesAsync(ticket.Id);
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                    }
                }

                await transaction.CommitAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.SupportTicketCreated,
                    EntityType = AuditEntityTypes.SupportTicket,
                    EntityId = ticket.Id.ToString(),
                    ActorUserId = userId,
                    After = new { subject = ticket.Subject, module = ticket.Module, priority = ticket.Priority, hasAttachment }
                });

                var reloaded = await LoadUserTicketAsync(userId, ticket.Id);
                _logger.LogInformation("Support ticket created — {TicketId} for user {UserId}", ticket.Id, userId);
                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(
                    AdminSupportTicketService.MapTicket(reloaded!),
                    "تیکت ایجاد شد",
                    201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating support ticket for user {UserId}", userId);
                if (createdTicketId.HasValue)
                    await SafeDeleteTicketFilesAsync(createdTicketId.Value);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SupportTicketResponseDto>> ReplyAsync(
            int userId,
            int ticketId,
            ReplySupportTicketDto dto,
            IFormFile? attachmentFile = null)
        {
            try
            {
                var content = dto.Content?.Trim() ?? string.Empty;
                var hasAttachment = attachmentFile is { Length: > 0 };

                if (string.IsNullOrWhiteSpace(content) && !hasAttachment)
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("متن یا فایل پیام الزامی است");

                var ticket = await _context.SupportTickets
                    .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId && !t.IsDeleted);

                if (ticket == null)
                    return ApiResponse<SupportTicketResponseDto>.NotFound("تیکت یافت نشد");

                if (TicketStatuses.IsClosedLike(ticket.Status))
                    return ApiResponse<SupportTicketResponseDto>.BadRequest("این تیکت بسته شده است");

                string? attachmentUrl = null;
                if (hasAttachment)
                {
                    var validation = AdminSupportTicketService.ValidateTicketAttachment(attachmentFile!);
                    if (validation != null)
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(validation);

                    try
                    {
                        attachmentUrl = await UploadTicketAttachmentAsync(attachmentFile!, ticketId);
                    }
                    catch (ArgumentException ex)
                    {
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading user reply attachment for ticket {TicketId}", ticketId);
                        return ApiResponse<SupportTicketResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                    }
                }

                _context.TicketMessages.Add(new TicketMessage
                {
                    TicketId = ticketId,
                    SenderUserId = userId,
                    IsAdminReply = false,
                    Content = string.IsNullOrWhiteSpace(content) ? "📎 فایل پیوست" : content,
                    AttachmentUrl = attachmentUrl,
                    CreatedAt = DateTime.UtcNow
                });

                // کاربر پیام جدید داد → دوباره در انتظار پاسخ پشتیبانی
                if (ticket.Status == TicketStatuses.InProgress)
                    ticket.Status = TicketStatuses.Open;

                ticket.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.SupportTicketUserReplied,
                    EntityType = AuditEntityTypes.SupportTicket,
                    EntityId = ticket.Id.ToString(),
                    ActorUserId = userId,
                    After = new { hasAttachment, status = ticket.Status }
                });

                var reloaded = await LoadUserTicketAsync(userId, ticketId);
                return ApiResponse<SupportTicketResponseDto>.CreateSuccess(
                    AdminSupportTicketService.MapTicket(reloaded!),
                    "پیام ثبت شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replying to ticket {TicketId} by user {UserId}", ticketId, userId);
                return ApiResponse<SupportTicketResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int userId, int ticketId)
        {
            try
            {
                _logger.LogInformation("Deleting support ticket {TicketId} by user {UserId}", ticketId, userId);

                var ticket = await _context.SupportTickets
                    .Include(t => t.Messages)
                    .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId && !t.IsDeleted);

                if (ticket == null)
                    return ApiResponse<bool>.NotFound("تیکت یافت نشد");

                var now = DateTime.UtcNow;
                ticket.IsDeleted = true;
                ticket.UpdatedAt = now;

                foreach (var message in ticket.Messages.Where(m => !m.IsDeleted))
                {
                    message.IsDeleted = true;
                    message.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();

                // پاک‌سازی فایل‌های دیسک بعد از soft-delete موفق
                await SafeDeleteTicketFilesAsync(ticketId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.SupportTicketUserDeleted,
                    EntityType = AuditEntityTypes.SupportTicket,
                    EntityId = ticket.Id.ToString(),
                    ActorUserId = userId
                });

                _logger.LogInformation("User {UserId} deleted ticket {TicketId} with file cleanup", userId, ticketId);
                return ApiResponse<bool>.CreateSuccess(true, "تیکت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ticket {TicketId} by user {UserId}", ticketId, userId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<SupportTicket?> LoadUserTicketAsync(int userId, int ticketId)
        {
            return await _context.SupportTickets.AsNoTracking()
                .Include(t => t.User)
                .Include(t => t.Messages.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.SenderUser)
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId && !t.IsDeleted);
        }

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

        private async Task SafeDeleteTicketFilesAsync(int ticketId)
        {
            try
            {
                await _fileUploadService.DeleteAllEntityFilesAsync(
                    FileUploadConstants.EntityType_Ticket,
                    ticketId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete ticket files for ticket {TicketId}", ticketId);
            }
        }
    }
}
