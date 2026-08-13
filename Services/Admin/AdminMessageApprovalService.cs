using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api_Vapp.Services.Admin
{
    public class AdminMessageApprovalService : IAdminMessageApprovalService
    {
        private readonly Api_Context _context;
        private readonly IMessageService _messageService;
        private readonly IAuditService _audit;
        private readonly ILogger<AdminMessageApprovalService> _logger;
        private readonly IUserAppNotifier _appNotifier;

        public AdminMessageApprovalService(
            Api_Context context,
            IMessageService messageService,
            IAuditService audit,
            ILogger<AdminMessageApprovalService> logger,
            IUserAppNotifier appNotifier)
        {
            _context = context;
            _messageService = messageService;
            _audit = audit;
            _logger = logger;
            _appNotifier = appNotifier;
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
                        // اگر SMS واقعاً fail شده، وضعیت Failed را نگه دار (نه revert کور به Pending)
                        if (campaign != null && campaign.Status == "Failed" && campaign.SentCount == 0)
                        {
                            await MarkApprovalSendFailedAsync(request, campaign, adminUserId);
                            return ApiResponse<bool>.BadRequest(
                                ControlledErrorHelper.SanitizeArgumentMessage(
                                    sendResult.Success ? "هیچ پیامکی ارسال نشد" : sendResult.Message,
                                    ControlledErrorHelper.SendFailed));
                        }

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

                    // اگر زمان‌بندی‌شده و هنوز موعد نرسیده: فقط تأیید کن تا Background در زمان مقرر بفرستد
                    var scheduledAtUtc = NormalizeToUtc(sendDto.ScheduledAt);
                    var isFutureSchedule = sendDto.SendType == CampaignSendType.Scheduled
                        && scheduledAtUtc.HasValue
                        && scheduledAtUtc.Value > DateTime.UtcNow.AddSeconds(30);

                    if (isFutureSchedule)
                    {
                        if (session != null)
                        {
                            try
                            {
                                var criteria = JsonSerializer.Deserialize<Dictionary<string, object>>(session.SelectionCriteria ?? "{}")
                                    ?? new Dictionary<string, object>();
                                criteria["SendType"] = CampaignSendType.Scheduled.ToString();
                                criteria["ScheduledAt"] = scheduledAtUtc!.Value.ToString("O");
                                criteria["AdminApproved"] = true;
                                criteria["PreventDuplicate"] = sendDto.PreventDuplicate;
                                criteria["DuplicatePreventionHours"] = sendDto.DuplicatePreventionHours;
                                criteria["SendToSpecificTags"] = sendDto.SendToSpecificTags;
                                if (sendDto.SelectedTagIds != null && sendDto.SelectedTagIds.Any())
                                    criteria["SelectedTagIds"] = JsonSerializer.Serialize(sendDto.SelectedTagIds);

                                session.SelectionCriteria = JsonSerializer.Serialize(criteria);
                                session.IsUsed = false;
                                var minExpiry = scheduledAtUtc.Value.AddHours(24);
                                var defaultExpiry = DateTime.UtcNow.AddHours(24);
                                session.ExpiresAt = minExpiry > defaultExpiry ? minExpiry : defaultExpiry;
                                session.UpdatedAt = DateTime.UtcNow;
                                _context.MessageSessions.Update(session);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to mark session as admin-approved for scheduled send - SessionId: {SessionId}", session.Id);
                                await RevertToPendingAsync(request);
                                return ApiResponse<bool>.BadRequest("خطا در ثبت تأیید زمان‌بندی");
                            }
                        }

                        request.Status = AdminApprovalStatuses.Approved;
                        request.ReviewedByUserId = adminUserId;
                        request.ReviewedAt = DateTime.UtcNow;
                        request.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        await _audit.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Approval,
                            Action = AuditActions.SmsApprovalApproved,
                            EntityType = AuditEntityTypes.SmsApprovalRequest,
                            EntityId = request.Id.ToString(),
                            ActorUserId = adminUserId,
                            TargetUserId = request.UserId,
                            After = new
                            {
                                status = request.Status,
                                scheduledAt = scheduledAtUtc,
                                deferredSend = true
                            }
                        });

                        _logger.LogInformation(
                            "Scheduled direct message approved for later send - RequestId: {RequestId}, MessageId: {MessageId}, ScheduledAt: {ScheduledAt}",
                            id, request.MessageId, scheduledAtUtc);

                        await NotifyMessageDecisionAsync(
                            request,
                            approved: true,
                            scheduled: true);

                        return ApiResponse<bool>.CreateSuccess(true, "تأیید شد؛ پیام در زمان مقرر ارسال می‌شود");
                    }

                    // زمان رسیده یا Quick: فوراً ارسال کن
                    if (sendDto.SendType == CampaignSendType.Scheduled)
                        sendDto.SendType = CampaignSendType.Quick;

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

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Approval,
                    Action = AuditActions.SmsApprovalApproved,
                    EntityType = AuditEntityTypes.SmsApprovalRequest,
                    EntityId = request.Id.ToString(),
                    ActorUserId = adminUserId,
                    TargetUserId = request.UserId,
                    Before = new
                    {
                        id = request.Id,
                        status = AdminApprovalStatuses.Pending,
                        requestType = request.RequestType,
                        userId = request.UserId,
                        messageId = request.MessageId,
                        messageCampaignId = request.MessageCampaignId
                    },
                    After = new
                    {
                        status = request.Status,
                        reviewedByUserId = request.ReviewedByUserId,
                        reviewedAt = request.ReviewedAt
                    }
                });

                await NotifyMessageDecisionAsync(request, approved: true, scheduled: false);

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

                if (request.Status != AdminApprovalStatuses.Pending)
                {
                    if (request.Status == AdminApprovalStatuses.Processing)
                        return ApiResponse<bool>.BadRequest("درخواست در حال پردازش و ارسال است و امکان رد کردن ندارد");

                    return ApiResponse<bool>.BadRequest("این درخواست قبلاً بررسی شده است");
                }

                var statusBeforeReject = request.Status;

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

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Approval,
                    Action = AuditActions.SmsApprovalRejected,
                    EntityType = AuditEntityTypes.SmsApprovalRequest,
                    EntityId = request.Id.ToString(),
                    ActorUserId = adminUserId,
                    TargetUserId = request.UserId,
                    Before = new
                    {
                        id = request.Id,
                        status = statusBeforeReject,
                        requestType = request.RequestType,
                        userId = request.UserId,
                        messageId = request.MessageId,
                        messageCampaignId = request.MessageCampaignId
                    },
                    After = new
                    {
                        status = request.Status,
                        reviewedByUserId = request.ReviewedByUserId,
                        reviewedAt = request.ReviewedAt,
                        rejectionReason = request.RejectionReason
                    }
                });

                var rejectPush = PushNotificationCopy.MessageRejected(
                    request.RejectionReason,
                    request.TitlePreview);
                await _appNotifier.NotifyAsync(
                    request.UserId,
                    NotificationCategory.Suggestions,
                    rejectPush.Title,
                    rejectPush.Body,
                    InAppNotificationTypes.MessageRejected,
                    relatedEntityId: request.Id,
                    relatedEntityType: AuditEntityTypes.SmsApprovalRequest,
                    actionUrl: "/sms/reports",
                    metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        decision = "Rejected",
                        rejectionReason = request.RejectionReason,
                        titlePreview = request.TitlePreview,
                        requestType = request.RequestType
                    }));

                return ApiResponse<bool>.CreateSuccess(true, "درخواست رد شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting SMS request {RequestId}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task NotifyMessageDecisionAsync(
            SmsApprovalRequest request,
            bool approved,
            bool scheduled)
        {
            if (approved)
            {
                var copy = PushNotificationCopy.MessageApproved(request.TitlePreview, scheduled);
                await _appNotifier.NotifyAsync(
                    request.UserId,
                    NotificationCategory.Suggestions,
                    copy.Title,
                    copy.Body,
                    InAppNotificationTypes.MessageApproved,
                    relatedEntityId: request.Id,
                    relatedEntityType: AuditEntityTypes.SmsApprovalRequest,
                    actionUrl: "/sms/reports",
                    metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        decision = "Approved",
                        scheduled,
                        titlePreview = request.TitlePreview,
                        requestType = request.RequestType
                    }));
                return;
            }

            var rejectCopy = PushNotificationCopy.MessageRejected(
                request.RejectionReason,
                request.TitlePreview);
            await _appNotifier.NotifyAsync(
                request.UserId,
                NotificationCategory.Suggestions,
                rejectCopy.Title,
                rejectCopy.Body,
                InAppNotificationTypes.MessageRejected,
                relatedEntityId: request.Id,
                relatedEntityType: AuditEntityTypes.SmsApprovalRequest,
                actionUrl: "/sms/reports",
                metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    decision = "Rejected",
                    rejectionReason = request.RejectionReason,
                    titlePreview = request.TitlePreview,
                    requestType = request.RequestType
                }));
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
                    // ErrorMessage / FailedCount را پاک نکن — برای تشخیص علت fail قبلی لازم‌اند
                    campaign.UpdatedAt = DateTime.UtcNow;

                    // گیرندگان Failed را برای تلاش مجدد آماده کن (فقط وقتی هیچ ارسالی موفق نبوده)
                    var failedRecipients = await _context.MessageRecipients
                        .Where(r => r.CampaignId == campaign.Id && r.Status == "Failed")
                        .ToListAsync();
                    foreach (var recipient in failedRecipients)
                    {
                        recipient.Status = "Pending";
                        recipient.SmsServiceId = null;
                        recipient.SentAt = null;
                        // ErrorMessage قبلی برای دیباگ نگه داشته می‌شود تا ارسال بعدی پاکش کند
                    }
                }
            }

            request.Status = AdminApprovalStatuses.Pending;
            request.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// پس از fail کامل ارسال: کمپین Failed می‌ماند (قابل مشاهده)، درخواست تأیید Pending برای retry.
        /// </summary>
        private async Task MarkApprovalSendFailedAsync(
            SmsApprovalRequest request,
            MessageCampaign campaign,
            int adminUserId)
        {
            campaign.Status = "Failed";
            campaign.AdminApprovalStatus = AdminApprovalStatuses.Pending;
            campaign.AdminApprovedAt = null;
            campaign.AdminApprovedByUserId = null;
            campaign.UpdatedAt = DateTime.UtcNow;

            var failedRecipients = await _context.MessageRecipients
                .Where(r => r.CampaignId == campaign.Id && r.Status == "Failed")
                .ToListAsync();
            foreach (var recipient in failedRecipients)
            {
                recipient.Status = "Pending";
                recipient.SmsServiceId = null;
                recipient.SentAt = null;
            }

            if (campaign.AutomatedMessageId.HasValue)
            {
                await SyncAutomationExecutionsAfterSendAsync(
                    campaign.AutomatedMessageId.Value,
                    campaign.Id);
            }

            request.Status = AdminApprovalStatuses.Pending;
            request.UpdatedAt = DateTime.UtcNow;
            request.ReviewedByUserId = adminUserId;
            await _context.SaveChangesAsync();

            _logger.LogWarning(
                "Campaign {CampaignId} send failed after approval — kept Failed status, approval reset to Pending for retry",
                campaign.Id);
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

            // اگر کمپین Failed است، وضعیت Failed را حفظ کن
            if (request.MessageCampaignId.HasValue)
            {
                var campaign = await _context.MessageCampaigns
                    .FirstOrDefaultAsync(c => c.Id == request.MessageCampaignId.Value && !c.IsDeleted);
                if (campaign != null && campaign.Status == "Failed" && campaign.SentCount == 0)
                {
                    await MarkApprovalSendFailedAsync(request, campaign, request.ReviewedByUserId ?? 0);
                    return;
                }
            }

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

        private static DateTime? NormalizeToUtc(DateTime? value)
        {
            if (!value.HasValue)
                return null;

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }
    }

    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly Api_Context _context;
        private readonly ISmsPricingService _smsPricing;
        private readonly IAdminQuickSendApprovalService _quickSendApproval;
        private readonly ILogger<AdminDashboardService> _logger;

        public AdminDashboardService(
            Api_Context context,
            ISmsPricingService smsPricing,
            IAdminQuickSendApprovalService quickSendApproval,
            ILogger<AdminDashboardService> logger)
        {
            _context = context;
            _smsPricing = smsPricing;
            _quickSendApproval = quickSendApproval;
            _logger = logger;
        }

        public async Task<ApiResponse<AdminDashboardStatsDto>> GetStatsAsync()
        {
            try
            {
                _logger.LogInformation("شروع بارگذاری آمار داشبورد ادمین");

                var utcNow = DateTime.UtcNow;
                var todayStart = DateTime.SpecifyKind(utcNow.Date, DateTimeKind.Utc);
                var tomorrow = todayStart.AddDays(1);
                var weekStart = todayStart.AddDays(-6);
                var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var smsStatsFrom = weekStart < monthStart ? weekStart : monthStart;

                var pricing = await _smsPricing.GetRuntimeAsync();
                var smsDaily = await AggregateSmsUsageByDayAsync(smsStatsFrom, tomorrow, pricing.Rules);
                var chargedByDay = await AggregateSmsWalletChargedByDayAsync(smsStatsFrom, tomorrow);

                var pagesToday = SumIntInRange(smsDaily, todayStart, tomorrow, x => x.Pages);
                var pagesWeek = SumIntInRange(smsDaily, weekStart, tomorrow, x => x.Pages);
                var pagesMonth = SumIntInRange(smsDaily, monthStart, tomorrow, x => x.Pages);

                var sentToday = SumIntInRange(smsDaily, todayStart, tomorrow, x => x.SentCount);
                var sentWeek = SumIntInRange(smsDaily, weekStart, tomorrow, x => x.SentCount);
                var sentMonth = SumIntInRange(smsDaily, monthStart, tomorrow, x => x.SentCount);

                var chargedToday = SumDecimalInRange(chargedByDay, todayStart, tomorrow);
                var chargedWeek = SumDecimalInRange(chargedByDay, weekStart, tomorrow);
                var chargedMonth = SumDecimalInRange(chargedByDay, monthStart, tomorrow);

                var stats = new AdminDashboardStatsDto
                {
                    PendingSmsApprovals = await _context.SmsApprovalRequests.CountAsync(r => r.Status == AdminApprovalStatuses.Pending && !r.IsDeleted),
                    PendingTemplateApprovals = await _context.MessageTemplates.CountAsync(t => t.ApprovalStatus == AdminApprovalStatuses.Pending && !t.IsDeleted),
                    PendingQuickSendApprovals = await _quickSendApproval.CountPendingAsync(),
                    OpenTickets = await _context.SupportTickets.CountAsync(t => (t.Status == TicketStatuses.Open || t.Status == TicketStatuses.InProgress) && !t.IsDeleted),
                    TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted),
                    ActiveSubscriptions = await _context.UserSubscriptions.CountAsync(us => us.Status == "Active" && us.ExpiresAt > DateTime.UtcNow && !us.IsDeleted),
                    SmsSentToday = sentToday,
                    SmsSentThisWeek = sentWeek,
                    SmsSentThisMonth = sentMonth,
                    SmsPagesToday = pagesToday,
                    SmsPagesThisWeek = pagesWeek,
                    SmsPagesThisMonth = pagesMonth,
                    CostPerPart = pricing.CostPerPart,
                    SmsEstimatedCostToday = RoundMoney(pagesToday * pricing.CostPerPart),
                    SmsEstimatedCostThisWeek = RoundMoney(pagesWeek * pricing.CostPerPart),
                    SmsEstimatedCostThisMonth = RoundMoney(pagesMonth * pricing.CostPerPart),
                    SmsChargedCostToday = chargedToday,
                    SmsChargedCostThisWeek = chargedWeek,
                    SmsChargedCostThisMonth = chargedMonth,
                    IsSmsBillingEnabled = pricing.IsBillingEffectivelyEnabled
                };

                _logger.LogInformation(
                    "پایان بارگذاری آمار داشبورد — PagesToday: {Pages}, ChargedToday: {Charged}, CostPerPart: {Cost}",
                    stats.SmsPagesToday, stats.SmsChargedCostToday, stats.CostPerPart);

                return ApiResponse<AdminDashboardStatsDto>.CreateSuccess(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری آمار داشبورد ادمین");
                return ApiResponse<AdminDashboardStatsDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AdminDashboardChartsDto>> GetChartsAsync()
        {
            try
            {
                _logger.LogInformation("شروع بارگذاری نمودارهای داشبورد ادمین");

                var utcNow = DateTime.UtcNow;
                var todayStart = DateTime.SpecifyKind(utcNow.Date, DateTimeKind.Utc);
                var tomorrow = todayStart.AddDays(1);
                var lineStart = todayStart.AddDays(-6);

                var userDailyRaw = await _context.Users
                    .AsNoTracking()
                    .Where(u => !u.IsDeleted && u.CreatedAt >= lineStart)
                    .GroupBy(u => u.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync();

                var userGrowth = new List<AdminDashboardChartPointDto>();
                for (var i = 0; i < 7; i++)
                {
                    var day = lineStart.AddDays(i);
                    var count = userDailyRaw.FirstOrDefault(x => x.Date == day.Date)?.Count ?? 0;
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

                var daysFromSaturday = ((int)todayStart.DayOfWeek + 1) % 7;
                var thisWeekStart = todayStart.AddDays(-daysFromSaturday);
                var eightWeeksStart = thisWeekStart.AddDays(-7 * 7);
                var twelveMonthsStart = monthStart.AddMonths(-11);
                var chartRangeStart = eightWeeksStart < twelveMonthsStart ? eightWeeksStart : twelveMonthsStart;

                var pricing = await _smsPricing.GetRuntimeAsync();
                var smsDaily = await AggregateSmsUsageByDayAsync(chartRangeStart, tomorrow, pricing.Rules);
                var chargedByDay = await AggregateSmsWalletChargedByDayAsync(chartRangeStart, tomorrow);

                var smsPagesDaily = BuildDailySmsPoints(smsDaily, chargedByDay, lineStart, 7, pricing.CostPerPart);
                var smsPagesWeekly = BuildWeeklySmsPoints(smsDaily, chargedByDay, todayStart, 8, pricing.CostPerPart);
                var smsPagesMonthly = BuildMonthlySmsPoints(smsDaily, chargedByDay, utcNow, 12, pricing.CostPerPart);

                _logger.LogInformation("پایان بارگذاری نمودارهای داشبورد ادمین");

                return ApiResponse<AdminDashboardChartsDto>.CreateSuccess(new AdminDashboardChartsDto
                {
                    UserGrowthLast7Days = userGrowth,
                    MonthlyActivity = monthlyActivity,
                    SmsPagesDaily = smsPagesDaily,
                    SmsPagesWeekly = smsPagesWeekly,
                    SmsPagesMonthly = smsPagesMonthly
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری نمودارهای داشبورد ادمین");
                return ApiResponse<AdminDashboardChartsDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        /// <summary>
        /// تجمیع تعداد ارسال و صفحات پیامک به‌ازای هر روز.
        /// اولویت: MessageText + قواعد تعرفه؛ در غیر این صورت PartsCount کمپین/پیام؛ در نهایت ۱.
        /// </summary>
        private async Task<Dictionary<DateTime, SmsDayUsage>> AggregateSmsUsageByDayAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            SmsPartsRules rules)
        {
            var rows = await _context.SmsDeliveryRecords
                .AsNoTracking()
                .Where(r => !r.IsDeleted
                    && r.SendStatus == SmsSendStatuses.Sent
                    && r.SentAt >= fromUtc
                    && r.SentAt < toUtcExclusive)
                .Select(r => new
                {
                    Day = r.SentAt.Date,
                    r.SourceModule,
                    r.SourceEntityId,
                    r.MessageText
                })
                .ToListAsync();

            if (rows.Count == 0)
                return new Dictionary<DateTime, SmsDayUsage>();

            var campaignIds = rows
                .Where(r => r.SourceModule == SmsSourceModules.MessageCampaign && r.SourceEntityId.HasValue)
                .Select(r => r.SourceEntityId!.Value)
                .Distinct()
                .ToList();

            var messageIds = rows
                .Where(r => (r.SourceModule == SmsSourceModules.MessageDirect
                             || r.SourceModule == SmsSourceModules.AutomatedMessage)
                            && r.SourceEntityId.HasValue)
                .Select(r => r.SourceEntityId!.Value)
                .Distinct()
                .ToList();

            var campaignParts = campaignIds.Count == 0
                ? new Dictionary<int, int>()
                : await _context.MessageCampaigns
                    .AsNoTracking()
                    .Where(c => campaignIds.Contains(c.Id) && !c.IsDeleted)
                    .Select(c => new { c.Id, c.PartsCount })
                    .ToDictionaryAsync(c => c.Id, c => c.PartsCount > 0 ? c.PartsCount : 1);

            var messageParts = messageIds.Count == 0
                ? new Dictionary<int, int>()
                : await _context.Messages
                    .AsNoTracking()
                    .Where(m => messageIds.Contains(m.Id) && !m.IsDeleted)
                    .Select(m => new { m.Id, m.PartsCount })
                    .ToDictionaryAsync(m => m.Id, m => m.PartsCount > 0 ? m.PartsCount : 1);

            var byDay = new Dictionary<DateTime, SmsDayUsage>();
            foreach (var row in rows)
            {
                var fallback = ResolveFallbackParts(row.SourceModule, row.SourceEntityId, campaignParts, messageParts);
                var pages = ResolveAccurateParts(row.MessageText, rules, fallback);
                var day = DateTime.SpecifyKind(row.Day, DateTimeKind.Utc);

                if (!byDay.TryGetValue(day, out var usage))
                {
                    usage = new SmsDayUsage();
                    byDay[day] = usage;
                }

                usage.SentCount += 1;
                usage.Pages += pages;
            }

            return byDay;
        }

        /// <summary>
        /// مبلغ واقعی کسرشده از کیف پول بابت ارسال پیامک (Purchase منفی با عنوان مرتبط).
        /// </summary>
        private async Task<Dictionary<DateTime, decimal>> AggregateSmsWalletChargedByDayAsync(
            DateTime fromUtc,
            DateTime toUtcExclusive)
        {
            var groups = await _context.WalletTransactions
                .AsNoTracking()
                .Where(wt => wt.Status == TransactionStatuses.Completed
                    && wt.TransactionType == WalletTransactionTypes.Purchase
                    && wt.Amount < 0
                    && wt.CreatedAt >= fromUtc
                    && wt.CreatedAt < toUtcExclusive
                    && (wt.Title.Contains("پیامک")
                        || wt.Title.Contains("ارسال پیام")
                        || wt.Title.Contains("ارسال کمپین")
                        || (wt.Description != null && wt.Description.Contains("پیامک"))))
                .GroupBy(wt => wt.CreatedAt.Date)
                .Select(g => new { Day = g.Key, Amount = g.Sum(x => -x.Amount) })
                .ToListAsync();

            var byDay = new Dictionary<DateTime, decimal>(groups.Count);
            foreach (var g in groups)
            {
                byDay[DateTime.SpecifyKind(g.Day, DateTimeKind.Utc)] = RoundMoney(g.Amount);
            }

            return byDay;
        }

        private static int ResolveFallbackParts(
            string sourceModule,
            int? sourceEntityId,
            IReadOnlyDictionary<int, int> campaignParts,
            IReadOnlyDictionary<int, int> messageParts)
        {
            if (sourceEntityId is null)
                return 1;

            if (sourceModule == SmsSourceModules.MessageCampaign
                && campaignParts.TryGetValue(sourceEntityId.Value, out var campaignPageCount))
            {
                return campaignPageCount;
            }

            if ((sourceModule == SmsSourceModules.MessageDirect
                 || sourceModule == SmsSourceModules.AutomatedMessage)
                && messageParts.TryGetValue(sourceEntityId.Value, out var messagePageCount))
            {
                return messagePageCount;
            }

            return 1;
        }

        private static int ResolveAccurateParts(string? messageText, SmsPartsRules rules, int fallback)
        {
            if (string.IsNullOrWhiteSpace(messageText))
                return Math.Max(1, fallback);

            try
            {
                var analysis = SmsPartsCalculator.Analyze(messageText, rules, throwOnMaxPages: false);
                return Math.Max(1, analysis.PartsCount);
            }
            catch (Exception)
            {
                return Math.Max(1, fallback);
            }
        }

        private static int SumIntInRange(
            IReadOnlyDictionary<DateTime, SmsDayUsage> byDay,
            DateTime fromInclusive,
            DateTime toExclusive,
            Func<SmsDayUsage, int> selector)
        {
            var total = 0;
            foreach (var (day, usage) in byDay)
            {
                if (day >= fromInclusive && day < toExclusive)
                    total += selector(usage);
            }

            return total;
        }

        private static decimal SumDecimalInRange(
            IReadOnlyDictionary<DateTime, decimal> byDay,
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            decimal total = 0;
            foreach (var (day, amount) in byDay)
            {
                if (day >= fromInclusive && day < toExclusive)
                    total += amount;
            }

            return RoundMoney(total);
        }

        private static List<AdminDashboardChartPointDto> BuildDailySmsPoints(
            IReadOnlyDictionary<DateTime, SmsDayUsage> smsDaily,
            IReadOnlyDictionary<DateTime, decimal> chargedByDay,
            DateTime startDay,
            int dayCount,
            decimal costPerPart)
        {
            var points = new List<AdminDashboardChartPointDto>(dayCount);
            for (var i = 0; i < dayCount; i++)
            {
                var day = startDay.AddDays(i);
                var pages = smsDaily.TryGetValue(day, out var usage) ? usage.Pages : 0;
                points.Add(new AdminDashboardChartPointDto
                {
                    Label = day.ToString("yyyy-MM-dd"),
                    Value = pages,
                    EstimatedCost = RoundMoney(pages * costPerPart),
                    ChargedCost = chargedByDay.GetValueOrDefault(day)
                });
            }

            return points;
        }

        private static List<AdminDashboardChartPointDto> BuildWeeklySmsPoints(
            IReadOnlyDictionary<DateTime, SmsDayUsage> smsDaily,
            IReadOnlyDictionary<DateTime, decimal> chargedByDay,
            DateTime todayUtc,
            int weekCount,
            decimal costPerPart)
        {
            var daysFromSaturday = ((int)todayUtc.DayOfWeek + 1) % 7;
            var thisWeekStart = todayUtc.AddDays(-daysFromSaturday);
            var firstWeekStart = thisWeekStart.AddDays(-7 * (weekCount - 1));

            var points = new List<AdminDashboardChartPointDto>(weekCount);
            for (var i = 0; i < weekCount; i++)
            {
                var weekStart = firstWeekStart.AddDays(7 * i);
                var weekEnd = weekStart.AddDays(7);
                var pages = SumIntInRange(smsDaily, weekStart, weekEnd, x => x.Pages);
                points.Add(new AdminDashboardChartPointDto
                {
                    Label = weekStart.ToString("yyyy-MM-dd"),
                    Value = pages,
                    EstimatedCost = RoundMoney(pages * costPerPart),
                    ChargedCost = SumDecimalInRange(chargedByDay, weekStart, weekEnd)
                });
            }

            return points;
        }

        private static List<AdminDashboardChartPointDto> BuildMonthlySmsPoints(
            IReadOnlyDictionary<DateTime, SmsDayUsage> smsDaily,
            IReadOnlyDictionary<DateTime, decimal> chargedByDay,
            DateTime utcNow,
            int monthCount,
            decimal costPerPart)
        {
            var currentMonthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var firstMonthStart = currentMonthStart.AddMonths(-(monthCount - 1));

            var points = new List<AdminDashboardChartPointDto>(monthCount);
            for (var i = 0; i < monthCount; i++)
            {
                var monthStart = firstMonthStart.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1);
                var pages = SumIntInRange(smsDaily, monthStart, monthEnd, x => x.Pages);
                points.Add(new AdminDashboardChartPointDto
                {
                    Label = monthStart.ToString("yyyy-MM"),
                    Value = pages,
                    EstimatedCost = RoundMoney(pages * costPerPart),
                    ChargedCost = SumDecimalInRange(chargedByDay, monthStart, monthEnd)
                });
            }

            return points;
        }

        private static decimal RoundMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed class SmsDayUsage
        {
            public int SentCount { get; set; }
            public int Pages { get; set; }
        }
    }
}
