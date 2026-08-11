using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Notification;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    public class InAppNotificationService : IInAppNotificationService
    {
        private readonly Api_Context _context;
        private readonly ILogger<InAppNotificationService> _logger;

        public InAppNotificationService(Api_Context context, ILogger<InAppNotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CreateSafeAsync(
            int userId,
            string title,
            string body,
            string type,
            NotificationCategory category = NotificationCategory.Suggestions,
            int? relatedEntityId = null,
            string? relatedEntityType = null,
            string? actionUrl = null,
            string? metadataJson = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (userId <= 0
                    || string.IsNullOrWhiteSpace(title)
                    || string.IsNullOrWhiteSpace(body)
                    || string.IsNullOrWhiteSpace(type))
                {
                    return;
                }

                _context.InAppNotifications.Add(new InAppNotification
                {
                    UserId = userId,
                    Title = title.Trim(),
                    Body = body.Trim(),
                    Type = type.Trim(),
                    Category = category,
                    IsRead = false,
                    RelatedEntityId = relatedEntityId,
                    RelatedEntityType = string.IsNullOrWhiteSpace(relatedEntityType)
                        ? null
                        : relatedEntityType.Trim(),
                    ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
                    Metadata = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                });

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "In-app notification created — UserId={UserId}, Type={Type}",
                    userId, type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "خطا در ایجاد اعلان درون‌برنامه‌ای — UserId={UserId}, Type={Type}",
                    userId, type);
            }
        }

        public async Task<ApiResponse<PagedResponse<InAppNotificationDto>>> GetMyNotificationsAsync(
            int userId,
            int page = 1,
            int pageSize = 20,
            bool? isRead = null,
            string? type = null)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var query = _context.InAppNotifications.AsNoTracking()
                    .Where(n => n.UserId == userId && !n.IsDeleted);

                if (isRead.HasValue)
                    query = query.Where(n => n.IsRead == isRead.Value);

                if (!string.IsNullOrWhiteSpace(type))
                    query = query.Where(n => n.Type == type.Trim());

                var totalCount = await query.CountAsync();
                var entities = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = entities.Select(Map).ToList();

                return ApiResponse<PagedResponse<InAppNotificationDto>>.CreateSuccess(
                    PagedResponse<InAppNotificationDto>.Create(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading in-app notifications — UserId={UserId}", userId);
                return ApiResponse<PagedResponse<InAppNotificationDto>>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UnreadNotificationCountDto>> GetUnreadCountAsync(int userId)
        {
            try
            {
                var count = await _context.InAppNotifications.AsNoTracking()
                    .CountAsync(n => n.UserId == userId && !n.IsDeleted && !n.IsRead);

                return ApiResponse<UnreadNotificationCountDto>.CreateSuccess(
                    new UnreadNotificationCountDto { Count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting unread notifications — UserId={UserId}", userId);
                return ApiResponse<UnreadNotificationCountDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> MarkAsReadAsync(int userId, int notificationId)
        {
            try
            {
                var notification = await _context.InAppNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId
                        && n.UserId == userId
                        && !n.IsDeleted);

                if (notification == null)
                    return ApiResponse<bool>.NotFound("اعلان یافت نشد");

                if (notification.IsRead)
                    return ApiResponse<bool>.CreateSuccess(true, "اعلان قبلاً خوانده شده است");

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                notification.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<bool>.CreateSuccess(true, "اعلان خوانده شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error marking notification read — UserId={UserId}, Id={Id}",
                    userId, notificationId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> MarkAllAsReadAsync(int userId)
        {
            try
            {
                var now = DateTime.UtcNow;
                var unread = await _context.InAppNotifications
                    .Where(n => n.UserId == userId && !n.IsDeleted && !n.IsRead)
                    .ToListAsync();

                foreach (var n in unread)
                {
                    n.IsRead = true;
                    n.ReadAt = now;
                    n.UpdatedAt = now;
                }

                if (unread.Count > 0)
                    await _context.SaveChangesAsync();

                return ApiResponse<bool>.CreateSuccess(true, "همه اعلان‌ها خوانده شدند");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications read — UserId={UserId}", userId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int userId, int notificationId)
        {
            try
            {
                var notification = await _context.InAppNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId && !n.IsDeleted);

                if (notification == null)
                    return ApiResponse<bool>.NotFound("اعلان یافت نشد");

                notification.IsDeleted = true;
                notification.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<bool>.CreateSuccess(true, "اعلان حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error deleting notification — UserId={UserId}, Id={Id}",
                    userId, notificationId);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<int>> DeleteManyAsync(int userId, IReadOnlyCollection<int> notificationIds)
        {
            try
            {
                if (notificationIds == null || notificationIds.Count == 0)
                    return ApiResponse<int>.BadRequest("حداقل یک اعلان باید انتخاب شود");

                var ids = notificationIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (ids.Count == 0)
                    return ApiResponse<int>.BadRequest("شناسه اعلان‌ها نامعتبر است");

                var notifications = await _context.InAppNotifications
                    .Where(n => n.UserId == userId && !n.IsDeleted && ids.Contains(n.Id))
                    .ToListAsync();

                if (notifications.Count == 0)
                    return ApiResponse<int>.CreateSuccess(0, "اعلانی برای حذف یافت نشد");

                var now = DateTime.UtcNow;
                foreach (var notification in notifications)
                {
                    notification.IsDeleted = true;
                    notification.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                return ApiResponse<int>.CreateSuccess(notifications.Count, "اعلان‌های انتخاب‌شده حذف شدند");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notifications in bulk — UserId={UserId}", userId);
                return ApiResponse<int>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static InAppNotificationDto Map(InAppNotification n) => new()
        {
            Id = n.Id,
            Title = n.Title,
            Body = n.Body,
            Type = n.Type,
            Category = n.Category.ToString(),
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            ActionUrl = n.ActionUrl,
            RelatedEntityId = n.RelatedEntityId,
            RelatedEntityType = n.RelatedEntityType,
            Metadata = n.Metadata,
            CreatedAt = n.CreatedAt
        };
    }
}
