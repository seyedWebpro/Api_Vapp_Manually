using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Admin
{
    public class AdminUserSubscriptionService : IAdminUserSubscriptionService
    {
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IUserPushNotifier _pushNotifier;

        public AdminUserSubscriptionService(
            Api_Context context,
            IAuditService audit,
            IUserPushNotifier pushNotifier)
        {
            _context = context;
            _audit = audit;
            _pushNotifier = pushNotifier;
        }

        public async Task<ApiResponse<List<UserSubscriptionResponseDto>>> GetAllAsync(int? userId = null, string? status = null)
        {
            var query = _context.UserSubscriptions.AsNoTracking()
                .Include(us => us.User)
                .Include(us => us.Plan)
                .Where(us => !us.IsDeleted);

            if (userId.HasValue)
                query = query.Where(us => us.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(us => us.Status == status);

            var items = await query.OrderByDescending(us => us.CreatedAt).ToListAsync();
            return ApiResponse<List<UserSubscriptionResponseDto>>.CreateSuccess(items.Select(Map).ToList());
        }

        public async Task<ApiResponse<UserSubscriptionResponseDto>> AssignAsync(AssignUserSubscriptionDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.UserId && !u.IsDeleted);
            if (user == null)
                return ApiResponse<UserSubscriptionResponseDto>.NotFound("کاربر یافت نشد");

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == dto.SubscriptionPlanId && !p.IsDeleted && p.IsActive);
            if (plan == null)
                return ApiResponse<UserSubscriptionResponseDto>.NotFound("پلن اشتراک یافت نشد");

            var startDate = dto.StartDate?.ToUniversalTime() ?? DateTime.UtcNow;
            var now = DateTime.UtcNow;

            // یک اشتراک مؤثر برای کاربر — قبلی‌های فعال لغو می‌شوند (هم‌راستا با خرید)
            var activeSubscriptions = await _context.UserSubscriptions
                .Where(us =>
                    us.UserId == dto.UserId
                    && !us.IsDeleted
                    && us.Status == "Active"
                    && us.ExpiresAt > now)
                .ToListAsync();

            var carriedDays = 0;
            foreach (var existing in activeSubscriptions)
            {
                var remaining = (int)Math.Ceiling((existing.ExpiresAt - now).TotalDays);
                if (remaining > carriedDays)
                    carriedDays = remaining;

                existing.Status = "Cancelled";
                existing.UpdatedAt = now;
            }

            var subscription = new UserSubscription
            {
                UserId = dto.UserId,
                SubscriptionPlanId = dto.SubscriptionPlanId,
                StartDate = startDate,
                ExpiresAt = startDate.AddDays(plan.DurationDays + Math.Max(0, carriedDays)),
                Status = "Active",
                CreatedAt = now
            };

            _context.UserSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            subscription.User = user;
            subscription.Plan = plan;

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Subscription,
                Action = AuditActions.UserSubscriptionAssigned,
                EntityType = AuditEntityTypes.UserSubscription,
                EntityId = subscription.Id.ToString(),
                TargetUserId = subscription.UserId,
                After = new
                {
                    subscription.Id,
                    subscription.UserId,
                    subscription.SubscriptionPlanId,
                    planName = plan.Name,
                    subscription.StartDate,
                    subscription.ExpiresAt,
                    subscription.Status
                }
            });

            return ApiResponse<UserSubscriptionResponseDto>.CreateSuccess(Map(subscription), "اشتراک به کاربر اختصاص داده شد", 201);
        }

        public async Task<ApiResponse<bool>> CancelAsync(int id)
        {
            var subscription = await _context.UserSubscriptions
                .Include(us => us.Plan)
                .FirstOrDefaultAsync(us => us.Id == id && !us.IsDeleted);
            if (subscription == null)
                return ApiResponse<bool>.NotFound("اشتراک یافت نشد");

            var before = new
            {
                subscription.Id,
                subscription.UserId,
                subscription.SubscriptionPlanId,
                subscription.Status,
                subscription.ExpiresAt
            };

            subscription.Status = "Cancelled";
            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Subscription,
                Action = AuditActions.UserSubscriptionRevoked,
                EntityType = AuditEntityTypes.UserSubscription,
                EntityId = subscription.Id.ToString(),
                TargetUserId = subscription.UserId,
                Before = before,
                After = new
                {
                    subscription.Id,
                    subscription.UserId,
                    subscription.SubscriptionPlanId,
                    subscription.Status,
                    subscription.ExpiresAt
                }
            });

            var cancelPush = PushNotificationCopy.SubscriptionCancelled(subscription.Plan?.Name);
            await _pushNotifier.NotifyAsync(
                subscription.UserId,
                NotificationCategory.ImportantNotifications,
                cancelPush.Title,
                cancelPush.Body);

            return ApiResponse<bool>.CreateSuccess(true, "اشتراک لغو شد");
        }

        private static UserSubscriptionResponseDto Map(UserSubscription us) => new()
        {
            Id = us.Id,
            UserId = us.UserId,
            UserPhoneNumber = us.User?.PhoneNumber,
            UserFullName = us.User?.FullName,
            SubscriptionPlanId = us.SubscriptionPlanId,
            PlanName = us.Plan?.Name ?? string.Empty,
            TierCode = us.Plan?.TierCode ?? string.Empty,
            StartDate = us.StartDate,
            ExpiresAt = us.ExpiresAt,
            Status = us.Status,
            CreatedAt = us.CreatedAt
        };
    }
}
