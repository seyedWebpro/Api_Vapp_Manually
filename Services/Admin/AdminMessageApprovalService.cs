using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api_Vapp.Services.Admin
{
    public class AdminMessageApprovalService : IAdminMessageApprovalService
    {
        private readonly Api_Context _context;
        private readonly IMessageService _messageService;
        private readonly ILogger<AdminMessageApprovalService> _logger;

        public AdminMessageApprovalService(
            Api_Context context,
            IMessageService messageService,
            ILogger<AdminMessageApprovalService> logger)
        {
            _context = context;
            _messageService = messageService;
            _logger = logger;
        }

        public Task<ApiResponse<PagedResponse<SmsApprovalRequestResponseDto>>> GetPendingAsync(int page = 1, int pageSize = 20)
        {
            return GetAllAsync(AdminApprovalStatuses.Pending, page, pageSize);
        }

        public async Task<ApiResponse<PagedResponse<SmsApprovalRequestResponseDto>>> GetAllAsync(string? status = null, int page = 1, int pageSize = 20)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var query = _context.SmsApprovalRequests.AsNoTracking()
                    .Include(r => r.User)
                    .Where(r => !r.IsDeleted);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(r => r.Status == status);

                var totalCount = await query.CountAsync();
                var items = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return ApiResponse<PagedResponse<SmsApprovalRequestResponseDto>>.CreateSuccess(
                    PagedResponse<SmsApprovalRequestResponseDto>.Create(
                        items.Select(Map).ToList(), totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SMS approval requests");
                return ApiResponse<PagedResponse<SmsApprovalRequestResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsApprovalRequestResponseDto>> GetByIdAsync(int id)
        {
            try
            {
                var request = await _context.SmsApprovalRequests.AsNoTracking()
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

                if (request == null)
                    return ApiResponse<SmsApprovalRequestResponseDto>.NotFound("درخواست تأیید یافت نشد");

                return ApiResponse<SmsApprovalRequestResponseDto>.CreateSuccess(Map(request));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SMS approval request {RequestId}", id);
                return ApiResponse<SmsApprovalRequestResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> ApproveAsync(int id, int adminUserId)
        {
            try
            {
                var claimed = await _context.SmsApprovalRequests
                    .Where(r => r.Id == id && r.Status == AdminApprovalStatuses.Pending && !r.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(r => r.Status, AdminApprovalStatuses.Processing)
                        .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));

                if (claimed == 0)
                    return ApiResponse<bool>.BadRequest("این درخواست قبلاً بررسی شده است");

                var request = await _context.SmsApprovalRequests
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

                if (request == null)
                    return ApiResponse<bool>.NotFound("درخواست تأیید یافت نشد");

                if (request.RequestType == SmsApprovalRequestTypes.Campaign && request.MessageCampaignId.HasValue)
                {
                    // مهم: وضعیت کمپین فقط بعد از ارسال موفق Approved می‌شود؛
                    // در غیر این صورت روی شکست، کاربر می‌تواند بدون تأیید دوباره ارسال کند.
                    var sendResult = await _messageService.ConfirmAndSendCampaignAsync(
                        request.MessageCampaignId.Value,
                        request.UserId,
                        bypassAdminApproval: true);

                    var campaign = await _context.MessageCampaigns
                        .FirstOrDefaultAsync(c => c.Id == request.MessageCampaignId.Value && !c.IsDeleted);

                    // ConfirmAndSend حتی با ۰ ارسال موفق، Success=true برمی‌گرداند — صریحاً چک می‌کنیم
                    var sendReallySucceeded = sendResult.Success
                        && campaign != null
                        && campaign.Status == "Sent"
                        && campaign.SentCount > 0;

                    if (!sendReallySucceeded)
                    {
                        await RevertToPendingAsync(request);
                        return ApiResponse<bool>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(
                                sendResult.Success ? "هیچ پیامکی ارسال نشد" : sendResult.Message,
                                ControlledErrorHelper.SendFailed));
                    }

                    campaign!.AdminApprovalStatus = AdminApprovalStatuses.Approved;
                    campaign.AdminApprovedAt = DateTime.UtcNow;
                    campaign.AdminApprovedByUserId = adminUserId;
                    campaign.UpdatedAt = DateTime.UtcNow;

                    if (campaign.AutomatedMessageId.HasValue)
                    {
                        await SyncAutomationExecutionsAfterSendAsync(
                            campaign.AutomatedMessageId.Value,
                            campaign.Id);
                    }
                }
                else if (request.RequestType == SmsApprovalRequestTypes.DirectMessage)
                {
                    if (string.IsNullOrWhiteSpace(request.SendPayloadJson))
                    {
                        await RevertToPendingAsync(request);
                        return ApiResponse<bool>.BadRequest("اطلاعات ارسال یافت نشد. لطفاً کاربر دوباره درخواست ارسال ثبت کند.");
                    }

                    SendDirectMessageDto? sendDto;
                    try
                    {
                        sendDto = JsonSerializer.Deserialize<SendDirectMessageDto>(request.SendPayloadJson);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid SendPayloadJson for approval request {RequestId}", id);
                        await RevertToPendingAsync(request);
                        return ApiResponse<bool>.BadRequest("اطلاعات ارسال نامعتبر است");
                    }

                    if (sendDto == null)
                    {
                        await RevertToPendingAsync(request);
                        return ApiResponse<bool>.BadRequest("اطلاعات ارسال یافت نشد");
                    }

                    MessageSession? session = null;
                    if (request.MessageSessionId.HasValue)
                    {
                        session = await _context.MessageSessions
                            .FirstOrDefaultAsync(s => s.Id == request.MessageSessionId.Value && !s.IsDeleted);
                    }

                    var sendResult = await _messageService.SendDirectMessageAsync(
                        request.UserId,
                        request.MessageId,
                        sendDto,
                        session,
                        bypassAdminApproval: true);

                    var directOk = sendResult.Success
                        && sendResult.Data != null
                        && sendResult.Data.SentCount > 0;

                    if (!directOk)
                    {
                        await RevertToPendingAsync(request);
                        return ApiResponse<bool>.BadRequest(
                            ControlledErrorHelper.SanitizeArgumentMessage(
                                sendResult.Success ? "هیچ پیامکی ارسال نشد" : sendResult.Message,
                                ControlledErrorHelper.SendFailed));
                    }
                }

                request.Status = AdminApprovalStatuses.Approved;
                request.ReviewedByUserId = adminUserId;
                request.ReviewedAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<bool>.CreateSuccess(true, "درخواست تأیید و ارسال انجام شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving SMS request {RequestId}", id);
                await SafeRevertAfterApproveFailureAsync(id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.SmsFailed);
            }
        }

        public async Task<ApiResponse<bool>> RejectAsync(int id, int adminUserId, RejectApprovalDto dto)
        {
            try
            {
                var request = await _context.SmsApprovalRequests
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

                if (request == null)
                    return ApiResponse<bool>.NotFound("درخواست تأیید یافت نشد");

                if (request.Status != AdminApprovalStatuses.Pending && request.Status != AdminApprovalStatuses.Processing)
                    return ApiResponse<bool>.BadRequest("این درخواست قبلاً بررسی شده است");

                request.Status = AdminApprovalStatuses.Rejected;
                request.ReviewedByUserId = adminUserId;
                request.ReviewedAt = DateTime.UtcNow;
                request.RejectionReason = dto.Reason.Trim();
                request.UpdatedAt = DateTime.UtcNow;

                if (request.MessageCampaignId.HasValue)
                {
                    var campaign = await _context.MessageCampaigns
                        .FirstOrDefaultAsync(c => c.Id == request.MessageCampaignId.Value && !c.IsDeleted);
                    if (campaign != null)
                    {
                        campaign.AdminApprovalStatus = AdminApprovalStatuses.Rejected;
                        campaign.AdminRejectionReason = dto.Reason.Trim();
                        campaign.Status = "Draft";
                        campaign.UpdatedAt = DateTime.UtcNow;

                        if (campaign.AutomatedMessageId.HasValue)
                        {
                            var contactIds = await _context.MessageRecipients
                                .Where(r => r.CampaignId == campaign.Id && r.ContactId.HasValue)
                                .Select(r => r.ContactId!.Value)
                                .Distinct()
                                .ToListAsync();

                            if (contactIds.Count > 0)
                            {
                                var todayStart = DateTime.UtcNow.Date;
                                var todayEnd = todayStart.AddDays(1);
                                var executions = await _context.AutomationExecutions
                                    .Where(ae => ae.AutomatedMessageId == campaign.AutomatedMessageId.Value
                                        && ae.ContactId.HasValue
                                        && contactIds.Contains(ae.ContactId.Value)
                                        && ae.ExecutedAt >= todayStart
                                        && ae.ExecutedAt < todayEnd
                                        && ae.Status == "PendingApproval")
                                    .ToListAsync();

                                foreach (var execution in executions)
                                {
                                    execution.Status = "Rejected";
                                    execution.ErrorMessage = dto.Reason.Trim();
                                    execution.SentCount = 0;
                                }
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return ApiResponse<bool>.CreateSuccess(true, "درخواست رد شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting SMS request {RequestId}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task RevertToPendingAsync(SmsApprovalRequest request)
        {
            if (request.MessageCampaignId.HasValue)
            {
                var campaign = await _context.MessageCampaigns
                    .FirstOrDefaultAsync(c => c.Id == request.MessageCampaignId.Value && !c.IsDeleted);

                if (campaign != null)
                {
                    // اگر SMS واقعاً رفته، هرگز Pending نکن (جلوگیری از ارسال دوباره)
                    if (campaign.Status == "Sent" || campaign.SentCount > 0)
                    {
                        request.Status = AdminApprovalStatuses.Approved;
                        request.UpdatedAt = DateTime.UtcNow;
                        campaign.AdminApprovalStatus = AdminApprovalStatuses.Approved;
                        campaign.AdminApprovedAt ??= DateTime.UtcNow;
                        campaign.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        return;
                    }

                    campaign.AdminApprovalStatus = AdminApprovalStatuses.Pending;
                    campaign.AdminApprovedAt = null;
                    campaign.AdminApprovedByUserId = null;
                    campaign.Status = "PendingApproval";
                    campaign.ErrorMessage = null;
                    campaign.FailedCount = 0;
                    campaign.UpdatedAt = DateTime.UtcNow;

                    // گیرندگان Failed را برای تلاش مجدد آماده کن (فقط وقتی هیچ ارسالی موفق نبوده)
                    var failedRecipients = await _context.MessageRecipients
                        .Where(r => r.CampaignId == campaign.Id && r.Status == "Failed")
                        .ToListAsync();
                    foreach (var recipient in failedRecipients)
                    {
                        recipient.Status = "Pending";
                        recipient.ErrorMessage = null;
                        recipient.SmsServiceId = null;
                        recipient.SentAt = null;
                    }
                }
            }

            request.Status = AdminApprovalStatuses.Pending;
            request.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// بعد از Exception در Approve: فقط اگر هنوز Processing باشد و SMS نرفته باشد، به Pending برگردان.
        /// </summary>
        private async Task SafeRevertAfterApproveFailureAsync(int id)
        {
            var request = await _context.SmsApprovalRequests
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (request == null)
                return;

            // نهایی‌شده‌ها را دست نزن
            if (request.Status is AdminApprovalStatuses.Approved or AdminApprovalStatuses.Rejected)
                return;

            await RevertToPendingAsync(request);
        }

        /// <summary>
        /// وضعیت اجرای اتوماسیون را با نتیجه واقعی گیرندگان کمپین هم‌تراز می‌کند.
        /// </summary>
        private async Task SyncAutomationExecutionsAfterSendAsync(int automatedMessageId, int campaignId)
        {
            var recipients = await _context.MessageRecipients
                .AsNoTracking()
                .Where(r => r.CampaignId == campaignId && r.ContactId.HasValue)
                .Select(r => new { ContactId = r.ContactId!.Value, r.Status })
                .ToListAsync();

            if (recipients.Count == 0)
                return;

            var statusByContact = recipients
                .GroupBy(r => r.ContactId)
                .ToDictionary(g => g.Key, g => g.First().Status);

            var contactIds = statusByContact.Keys.ToList();
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            var executions = await _context.AutomationExecutions
                .Where(ae => ae.AutomatedMessageId == automatedMessageId
                    && ae.ContactId.HasValue
                    && contactIds.Contains(ae.ContactId.Value)
                    && ae.ExecutedAt >= todayStart
                    && ae.ExecutedAt < todayEnd
                    && ae.Status == "PendingApproval")
                .ToListAsync();

            foreach (var execution in executions)
            {
                var recipientStatus = statusByContact[execution.ContactId!.Value];
                if (recipientStatus == "Sent")
                {
                    execution.Status = "Success";
                    execution.SentCount = 1;
                    execution.ErrorMessage = null;
                }
                else
                {
                    execution.Status = "Failed";
                    execution.SentCount = 0;
                    execution.ErrorMessage = ControlledErrorHelper.SendFailed;
                }
            }
        }

        private static SmsApprovalRequestResponseDto Map(SmsApprovalRequest request) => new()
        {
            Id = request.Id,
            UserId = request.UserId,
            UserPhoneNumber = request.User?.PhoneNumber,
            UserFullName = request.User?.FullName,
            RequestType = request.RequestType,
            MessageCampaignId = request.MessageCampaignId,
            MessageId = request.MessageId,
            MessageSessionId = request.MessageSessionId,
            ContentPreview = request.ContentPreview,
            TitlePreview = request.TitlePreview,
            RecipientsCount = request.RecipientsCount,
            Status = request.Status,
            ReviewedByUserId = request.ReviewedByUserId,
            ReviewedAt = request.ReviewedAt,
            RejectionReason = request.RejectionReason,
            CreatedAt = request.CreatedAt
        };
    }

    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly Api_Context _context;
        private readonly ILogger<AdminDashboardService> _logger;

        public AdminDashboardService(Api_Context context, ILogger<AdminDashboardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<AdminDashboardStatsDto>> GetStatsAsync()
        {
            try
            {
                var stats = new AdminDashboardStatsDto
                {
                    PendingSmsApprovals = await _context.SmsApprovalRequests.CountAsync(r => r.Status == AdminApprovalStatuses.Pending && !r.IsDeleted),
                    PendingTemplateApprovals = await _context.MessageTemplates.CountAsync(t => t.ApprovalStatus == AdminApprovalStatuses.Pending && !t.IsDeleted),
                    OpenTickets = await _context.SupportTickets.CountAsync(t => (t.Status == TicketStatuses.Open || t.Status == TicketStatuses.InProgress) && !t.IsDeleted),
                    TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted),
                    ActiveSubscriptions = await _context.UserSubscriptions.CountAsync(us => us.Status == "Active" && us.ExpiresAt > DateTime.UtcNow && !us.IsDeleted)
                };

                return ApiResponse<AdminDashboardStatsDto>.CreateSuccess(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard stats");
                return ApiResponse<AdminDashboardStatsDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AdminDashboardChartsDto>> GetChartsAsync()
        {
            try
            {
                var utcNow = DateTime.UtcNow;
                var lineStart = utcNow.Date.AddDays(-6);

                var userDailyRaw = await _context.Users
                    .Where(u => !u.IsDeleted && u.CreatedAt >= lineStart)
                    .GroupBy(u => u.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync();

                var userGrowth = new List<AdminDashboardChartPointDto>();
                for (var i = 0; i < 7; i++)
                {
                    var day = lineStart.AddDays(i);
                    var count = userDailyRaw.FirstOrDefault(x => x.Date == day)?.Count ?? 0;
                    userGrowth.Add(new AdminDashboardChartPointDto
                    {
                        Label = day.ToString("yyyy-MM-dd"),
                        Value = count
                    });
                }

                var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                var monthlyActivity = new List<AdminDashboardChartPointDto>
                {
                    new() { Label = "کاربران جدید", Value = await _context.Users.CountAsync(u => !u.IsDeleted && u.CreatedAt >= monthStart) },
                    new() { Label = "تیکت‌های جدید", Value = await _context.SupportTickets.CountAsync(t => !t.IsDeleted && t.CreatedAt >= monthStart) },
                    new() { Label = "اشتراک‌های جدید", Value = await _context.UserSubscriptions.CountAsync(us => !us.IsDeleted && us.CreatedAt >= monthStart) },
                    new() { Label = "درخواست پیام", Value = await _context.SmsApprovalRequests.CountAsync(r => !r.IsDeleted && r.CreatedAt >= monthStart) },
                    new() { Label = "قالب جدید", Value = await _context.MessageTemplates.CountAsync(t => !t.IsDeleted && t.CreatedAt >= monthStart) },
                };

                return ApiResponse<AdminDashboardChartsDto>.CreateSuccess(new AdminDashboardChartsDto
                {
                    UserGrowthLast7Days = userGrowth,
                    MonthlyActivity = monthlyActivity
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard charts");
                return ApiResponse<AdminDashboardChartsDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }
    }
}
