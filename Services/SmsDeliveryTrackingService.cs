using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class SmsDeliveryTrackingService : ISmsDeliveryTrackingService
    {
        private readonly ISmsDeliveryRecordRepository _repository;
        private readonly ISmsService _smsService;
        private readonly IWalletService _walletService;
        private readonly IAuditService _audit;
        private readonly ILogger<SmsDeliveryTrackingService> _logger;
        private readonly int _maxCheckAttempts;
        private readonly int _minAgeBeforeFirstCheckMinutes;
        private readonly int _maxSidsPerSyncBatch;
        private readonly bool _refundOnUndelivered;

        public SmsDeliveryTrackingService(
            ISmsDeliveryRecordRepository repository,
            ISmsService smsService,
            IWalletService walletService,
            IAuditService audit,
            IConfiguration configuration,
            ILogger<SmsDeliveryTrackingService> logger)
        {
            _repository = repository;
            _smsService = smsService;
            _walletService = walletService;
            _audit = audit;
            _logger = logger;
            _maxCheckAttempts = configuration.GetValue("Sms:DeliverySync:MaxCheckAttempts", 48);
            _minAgeBeforeFirstCheckMinutes = configuration.GetValue("Sms:DeliverySync:MinAgeBeforeFirstCheckMinutes", 15);
            _maxSidsPerSyncBatch = configuration.GetValue("Sms:DeliverySync:MaxSidsPerBatch", 50);
            _refundOnUndelivered = configuration.GetValue("Sms:DeliverySync:RefundOnUndelivered", true);
        }

        public async Task TrackSuccessfulSendAsync(SmsDeliveryTrackRequestDto request)
        {
            try
            {
                if (request.UserId <= 0 || request.Sid <= 0 || string.IsNullOrWhiteSpace(request.Mobile))
                {
                    _logger.LogWarning(
                        "SMS delivery track skipped — invalid input. UserId: {UserId}, Sid: {Sid}, Mobile: {Mobile}, Module: {Module}",
                        request.UserId, request.Sid, request.Mobile, request.SourceModule);
                    return;
                }

                var sentAtUtc = request.SentAt.HasValue
                    ? DateTime.SpecifyKind(request.SentAt.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                var chargedAmount = request.ChargedAmount < 0 ? 0 : request.ChargedAmount;
                var partsCount = request.PartsCount < 0 ? 0 : request.PartsCount;

                var record = new SmsDeliveryRecord
                {
                    UserId = request.UserId,
                    SourceModule = request.SourceModule,
                    SourceEntityId = request.SourceEntityId,
                    SourceEntityLabel = request.SourceEntityLabel,
                    Mobile = request.Mobile.Trim(),
                    Sid = request.Sid,
                    MessageText = string.IsNullOrWhiteSpace(request.MessageText) ? null : request.MessageText.Trim(),
                    ChargedAmount = chargedAmount,
                    PartsCount = partsCount,
                    SendStatus = SmsSendStatuses.Sent,
                    DeliveryCategory = SmsDeliveryCategories.PendingSync,
                    SentAt = sentAtUtc,
                    CreatedAt = DateTime.UtcNow
                };

                await _repository.AddAsync(record);
                await _repository.SaveChangesAsync();

                _logger.LogInformation(
                    "SMS delivery record created — RecordId: {RecordId}, UserId: {UserId}, Module: {Module}, EntityId: {EntityId}, Mobile: {Mobile}, Sid: {Sid}, ChargedAmount: {ChargedAmount}, Parts: {Parts}, SentAtUtc: {SentAtUtc:yyyy-MM-dd HH:mm:ss}, Label: {Label}",
                    record.Id,
                    record.UserId,
                    record.SourceModule,
                    record.SourceEntityId,
                    record.Mobile,
                    record.Sid,
                    record.ChargedAmount,
                    record.PartsCount,
                    record.SentAt,
                    record.SourceEntityLabel ?? "-");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SMS delivery track failed — UserId: {UserId}, Sid: {Sid}, Mobile: {Mobile}, Module: {Module}",
                    request.UserId, request.Sid, request.Mobile, request.SourceModule);
            }
        }

        public async Task<ApiResponse<SmsDeliveryRecordDto>> GetByIdAsync(int userId, int id)
        {
            var record = await _repository.GetByIdAsync(id, userId);
            if (record == null)
                return ApiResponse<SmsDeliveryRecordDto>.NotFound("رکورد گزارش پیامک یافت نشد");

            return ApiResponse<SmsDeliveryRecordDto>.CreateSuccess(MapToDto(record));
        }

        public async Task<ApiResponse<SmsDeliveryReportListDto>> GetReportAsync(int userId, SmsDeliveryReportFilterDto filter)
        {
            if (filter.PageNumber < 1) filter.PageNumber = 1;
            if (filter.PageSize < 1 || filter.PageSize > 100) filter.PageSize = 20;

            var (items, totalCount) = await _repository.GetByUserAsync(userId, filter);

            _logger.LogDebug(
                "SMS delivery report queried — UserId: {UserId}, Module: {Module}, EntityId: {EntityId}, Category: {Category}, Total: {Total}, Page: {Page}",
                userId, filter.SourceModule ?? "-", filter.SourceEntityId, filter.DeliveryCategory ?? "-", totalCount, filter.PageNumber);

            return ApiResponse<SmsDeliveryReportListDto>.CreateSuccess(new SmsDeliveryReportListDto
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize)
            });
        }

        public async Task<ApiResponse<SmsDeliverySummaryDto>> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter)
        {
            var summary = await _repository.GetSummaryAsync(userId, filter);

            _logger.LogDebug(
                "SMS delivery summary queried — UserId: {UserId}, Total: {Total}, Delivered: {Delivered}, Operator: {Operator}, NotDelivered: {NotDelivered}, PendingApproval: {PendingApproval}, Rejected: {Rejected}, PendingSync: {PendingSync}",
                userId,
                summary.Total,
                summary.DeliveredToPhone,
                summary.SentToOperator,
                summary.NotDelivered,
                summary.PendingApproval,
                summary.Rejected,
                summary.PendingSync);

            return ApiResponse<SmsDeliverySummaryDto>.CreateSuccess(summary);
        }

        public async Task<ApiResponse<SmsDeliveryRecordDto>> RefreshRecordAsync(int userId, int id)
        {
            var record = await _repository.GetByIdAsync(id, userId);
            if (record == null)
                return ApiResponse<SmsDeliveryRecordDto>.NotFound("رکورد گزارش پیامک یافت نشد");

            if (record.SendStatus != SmsSendStatuses.Sent || record.Sid <= 0)
                return ApiResponse<SmsDeliveryRecordDto>.BadRequest("این رکورد قابل بروزرسانی وضعیت دلیوری نیست");

            _logger.LogInformation(
                "SMS delivery manual refresh — RecordId: {RecordId}, UserId: {UserId}, Sid: {Sid}, Mobile: {Mobile}, CurrentCategory: {Category}, CheckAttempts: {Attempts}",
                record.Id, userId, record.Sid, record.Mobile, record.DeliveryCategory, record.CheckAttempts);

            await SyncSidGroupAsync(record.Sid, isManualRefresh: true);

            var updated = await _repository.GetByIdAsync(id, userId);
            var dto = MapToDto(updated!);

            _logger.LogInformation(
                "SMS delivery manual refresh completed — RecordId: {RecordId}, Sid: {Sid}, StatusCode: {StatusCode}, StatusMessage: {StatusMessage}, Category: {Category}, IsFinal: {IsFinal}, WalletRefunded: {Refunded}",
                id, updated!.Sid, updated.ProviderStatusCode, updated.ProviderStatusMessage ?? "-", updated.DeliveryCategory, updated.IsDeliveryFinal, updated.WalletRefundedAt.HasValue);

            return ApiResponse<SmsDeliveryRecordDto>.CreateSuccess(dto);
        }

        public async Task<ApiResponse<SmsDeliverySummaryDto>> RefreshBySidAsync(int userId, long sid)
        {
            if (sid <= 0)
                return ApiResponse<SmsDeliverySummaryDto>.BadRequest("کد ارسال نامعتبر است");

            if (!await _repository.UserOwnsSidAsync(userId, sid))
                return ApiResponse<SmsDeliverySummaryDto>.NotFound("ارسال مورد نظر یافت نشد");

            _logger.LogInformation(
                "SMS delivery refresh by Sid — UserId: {UserId}, Sid: {Sid}",
                userId, sid);

            await SyncSidGroupForUserAsync(userId, sid);

            var summary = await _repository.GetSummaryBySidAsync(userId, sid);
            return ApiResponse<SmsDeliverySummaryDto>.CreateSuccess(summary, "وضعیت دلیوری بروزرسانی شد");
        }

        /// <summary>
        /// همگام‌سازی وضعیت دلیوری — SentAt و now هر دو UTC (مطابق بقیه backend)
        /// </summary>
        public async Task SyncPendingDeliveriesAsync(CancellationToken cancellationToken = default)
        {
            var batchStartedUtc = DateTime.UtcNow;
            var sentBeforeUtc = batchStartedUtc.AddMinutes(-_minAgeBeforeFirstCheckMinutes);

            var pendingSids = await _repository.GetDistinctPendingSidsAsync(
                sentBeforeUtc,
                _maxCheckAttempts,
                _maxSidsPerSyncBatch);

            if (pendingSids.Count == 0)
            {
                _logger.LogDebug(
                    "SMS delivery sync — no pending Sids. NowUtc: {NowUtc:yyyy-MM-dd HH:mm:ss}, MinAgeMinutes: {MinAge}, CutoffUtc: {CutoffUtc:yyyy-MM-dd HH:mm:ss}",
                    batchStartedUtc, _minAgeBeforeFirstCheckMinutes, sentBeforeUtc);
                return;
            }

            _logger.LogInformation(
                "=== SMS delivery sync batch started === NowUtc: {NowUtc:yyyy-MM-dd HH:mm:ss}, MinAgeMinutes: {MinAge}, CutoffUtc: {CutoffUtc:yyyy-MM-dd HH:mm:ss}, PendingSids: {Count}, Sids: [{Sids}]",
                batchStartedUtc,
                _minAgeBeforeFirstCheckMinutes,
                sentBeforeUtc,
                pendingSids.Count,
                string.Join(", ", pendingSids));

            var syncedCount = 0;
            foreach (var sid in pendingSids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SyncSidGroupAsync(sid);
                syncedCount++;
            }

            var durationMs = (DateTime.UtcNow - batchStartedUtc).TotalMilliseconds;
            _logger.LogInformation(
                "=== SMS delivery sync batch completed === SyncedSids: {Synced}/{Total}, DurationMs: {DurationMs:F0}, FinishedUtc: {FinishedUtc:yyyy-MM-dd HH:mm:ss}",
                syncedCount, pendingSids.Count, durationMs, DateTime.UtcNow);
        }

        private async Task SyncSidGroupAsync(long sid, bool isManualRefresh = false)
        {
            var activeRecords = await _repository.GetActivePendingBySidAsync(sid, _maxCheckAttempts);
            if (activeRecords.Count == 0)
            {
                _logger.LogDebug("SMS delivery sync skipped for Sid {Sid} — no active pending records", sid);
                return;
            }

            await ApplyDeliveryStatusAsync(sid, activeRecords, isManualRefresh ? "ManualRefresh" : "BackgroundJob");
        }

        private async Task SyncSidGroupForUserAsync(int userId, long sid)
        {
            var records = await _repository.GetSentRecordsBySidForUserAsync(userId, sid);
            if (records.Count == 0)
            {
                _logger.LogDebug("SMS delivery user refresh skipped for Sid {Sid} — no records", sid);
                return;
            }

            await ApplyDeliveryStatusAsync(sid, records, "ManualRefreshBySid");
        }

        private async Task ApplyDeliveryStatusAsync(long sid, List<SmsDeliveryRecord> activeRecords, string trigger)
        {
            var recordIds = string.Join(", ", activeRecords.Select(r => r.Id));
            var mobiles = string.Join(", ", activeRecords.Select(r => r.Mobile));

            _logger.LogInformation(
                "SMS delivery sync for Sid {Sid} — Trigger: {Trigger}, ActiveRecords: {Count}, RecordIds: [{RecordIds}], Mobiles: [{Mobiles}]",
                sid, trigger, activeRecords.Count, recordIds, mobiles);

            // اگر همه رکوردها فقط منتظر refund هستند و وضعیت نهایی دارند، API را دوباره نزن
            var needsProviderSync = activeRecords.Any(r => !r.IsDeliveryFinal);
            if (needsProviderSync)
            {
                await SyncFromProviderAsync(sid, activeRecords, trigger, recordIds);
            }
            else
            {
                _logger.LogDebug(
                    "SMS delivery skip provider call — Sid: {Sid}, Trigger: {Trigger}, all records final; processing refunds only",
                    sid, trigger);
            }

            await ProcessUndeliveredRefundsAsync(activeRecords, trigger);
        }

        private async Task SyncFromProviderAsync(
            long sid,
            List<SmsDeliveryRecord> activeRecords,
            string trigger,
            string recordIds)
        {
            var deliveryResult = await _smsService.GetDeliveryStatusAsync(sid);
            var nowUtc = DateTime.UtcNow;

            foreach (var record in activeRecords)
            {
                if (!record.IsDeliveryFinal)
                    record.CheckAttempts++;

                record.LastCheckedAt = nowUtc;
                record.UpdatedAt = nowUtc;
            }

            if (!deliveryResult.Success || deliveryResult.Data == null)
            {
                await _repository.SaveChangesAsync();
                _logger.LogWarning(
                    "SMS delivery API call failed — Sid: {Sid}, Trigger: {Trigger}, ApiSuccess: {Success}, Message: {Message}",
                    sid, trigger, deliveryResult.Success, deliveryResult.Message ?? "-");
                return;
            }

            if (deliveryResult.Data.Status < 0)
            {
                await _repository.SaveChangesAsync();
                _logger.LogWarning(
                    "SMS delivery API returned error — Sid: {Sid}, Trigger: {Trigger}, ApiStatus: {ApiStatus}, ApiMessage: {ApiMessage}",
                    sid, trigger, deliveryResult.Data.Status, deliveryResult.Data.Messege ?? "-");
                return;
            }

            if (deliveryResult.Data.Deliveries == null || deliveryResult.Data.Deliveries.Count == 0)
            {
                await _repository.SaveChangesAsync();
                _logger.LogInformation(
                    "SMS delivery API empty Deliveries — Sid: {Sid}, Trigger: {Trigger}, CheckAttempts incremented, will retry. Records: [{RecordIds}]",
                    sid, trigger, recordIds);
                return;
            }

            var deliveryLookup = deliveryResult.Data.Deliveries
                .GroupBy(d => SmsDeliveryStatusMapper.NormalizeMobile(d.Mobile))
                .ToDictionary(g => g.Key, g => g.First());

            _logger.LogDebug(
                "SMS delivery API response — Sid: {Sid}, DeliveriesCount: {Count}, Items: [{Items}]",
                sid,
                deliveryResult.Data.Deliveries.Count,
                string.Join(" | ", deliveryResult.Data.Deliveries.Select(d => $"{d.Mobile}:{d.Status}:{d.StatusMessage}")));

            var updatedCount = 0;
            var unmatchedRecords = new List<SmsDeliveryRecord>();

            foreach (var record in activeRecords)
            {
                var key = SmsDeliveryStatusMapper.NormalizeMobile(record.Mobile);
                if (string.IsNullOrEmpty(key) || !deliveryLookup.TryGetValue(key, out var deliveryItem))
                {
                    unmatchedRecords.Add(record);
                    continue;
                }

                var previousCategory = record.DeliveryCategory;
                record.ProviderStatusCode = deliveryItem.Status;
                record.ProviderStatusMessage = deliveryItem.StatusMessage;
                record.DeliveryCategory = SmsDeliveryStatusMapper.MapToCategory(deliveryItem.Status);
                record.IsDeliveryFinal = SmsDeliveryStatusMapper.IsFinalStatus(deliveryItem.Status);
                record.UpdatedAt = nowUtc;
                updatedCount++;

                _logger.LogInformation(
                    "SMS delivery status updated — RecordId: {RecordId}, Sid: {Sid}, Mobile: {Mobile}, ProviderStatus: {Status} ({StatusMessage}), Category: {PreviousCategory} → {NewCategory}, IsFinal: {IsFinal}, Attempt: {Attempt}",
                    record.Id,
                    sid,
                    record.Mobile,
                    deliveryItem.Status,
                    deliveryItem.StatusMessage ?? "-",
                    previousCategory,
                    record.DeliveryCategory,
                    record.IsDeliveryFinal,
                    record.CheckAttempts);
            }

            if (unmatchedRecords.Count > 0)
            {
                _logger.LogWarning(
                    "SMS delivery mobile not matched in API response — Sid: {Sid}, UnmatchedCount: {Count}, RecordIds: [{RecordIds}], StoredMobiles: [{Mobiles}], ApiMobiles: [{ApiMobiles}]",
                    sid,
                    unmatchedRecords.Count,
                    string.Join(", ", unmatchedRecords.Select(r => r.Id)),
                    string.Join(", ", unmatchedRecords.Select(r => r.Mobile)),
                    string.Join(", ", deliveryResult.Data.Deliveries.Select(d => d.Mobile)));
            }

            await _repository.SaveChangesAsync();

            _logger.LogInformation(
                "SMS delivery sync completed for Sid {Sid} — Trigger: {Trigger}, Updated: {Updated}/{Total}, Unmatched: {Unmatched}, IsFinalCount: {FinalCount}",
                sid,
                trigger,
                updatedCount,
                activeRecords.Count,
                unmatchedRecords.Count,
                activeRecords.Count(r => r.IsDeliveryFinal));
        }

        private async Task ProcessUndeliveredRefundsAsync(List<SmsDeliveryRecord> records, string trigger)
        {
            if (!_refundOnUndelivered)
                return;

            foreach (var record in records)
            {
                if (!record.IsDeliveryFinal)
                    continue;

                if (!SmsDeliveryStatusMapper.IsRefundEligibleCategory(record.DeliveryCategory))
                    continue;

                if (record.ChargedAmount <= 0)
                    continue;

                if (record.WalletRefundTransactionId.HasValue)
                    continue;

                var claimedAt = DateTime.UtcNow;
                var claimed = await _repository.TryClaimWalletRefundAsync(record.Id, claimedAt);
                if (!claimed)
                {
                    _logger.LogDebug(
                        "SMS delivery refund skip — already claimed. RecordId: {RecordId}, Sid: {Sid}",
                        record.Id, record.Sid);
                    continue;
                }

                // مهم: قبل از AddBalance روی همان DbContext، claim را روی entity هم set کن
                // تا SaveChanges کیف پول، WalletRefundedAt را با null بازنویسی نکند
                record.WalletRefundedAt = claimedAt;

                try
                {
                    var reference = $"SMS-DLR-REFUND-{record.Id}";
                    var refund = await _walletService.AddBalanceAsync(
                        record.UserId,
                        record.ChargedAmount,
                        WalletTransactionTypes.Refund,
                        SmsDeliveryRefundCopy.WalletTitle,
                        SmsDeliveryRefundCopy.BuildWalletDescription(record),
                        referenceNumber: reference,
                        sendPushNotification: false);

                    if (!refund.Success || refund.Data == null)
                    {
                        await _repository.ClearWalletRefundClaimAsync(record.Id);
                        record.WalletRefundedAt = null;

                        _logger.LogError(
                            "CRITICAL: SMS delivery wallet refund failed — RecordId: {RecordId}, UserId: {UserId}, Amount: {Amount}, Sid: {Sid}, Category: {Category}, Trigger: {Trigger}, Error: {Error}",
                            record.Id, record.UserId, record.ChargedAmount, record.Sid, record.DeliveryCategory, trigger, refund.Message ?? "-");

                        await _audit.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Sms,
                            Action = AuditActions.SmsDeliveryRefundFailed,
                            EntityType = AuditEntityTypes.SmsSend,
                            EntityId = record.Id.ToString(),
                            ActorUserId = record.UserId,
                            TargetUserId = record.UserId,
                            Succeeded = false,
                            ErrorMessage = "WalletRefundFailed",
                            Metadata = new
                            {
                                record.Sid,
                                record.SourceModule,
                                record.SourceEntityId,
                                record.ChargedAmount,
                                record.DeliveryCategory,
                                record.ProviderStatusCode,
                                trigger
                            }
                        });
                        continue;
                    }

                    await _repository.SetWalletRefundTransactionAsync(record.Id, refund.Data.Id, claimedAt);
                    record.WalletRefundTransactionId = refund.Data.Id;
                    record.WalletRefundedAt = claimedAt;

                    _logger.LogInformation(
                        "SMS delivery wallet refunded — RecordId: {RecordId}, UserId: {UserId}, Amount: {Amount}, Sid: {Sid}, Category: {Category}, WalletTxId: {WalletTxId}, Trigger: {Trigger}",
                        record.Id, record.UserId, record.ChargedAmount, record.Sid, record.DeliveryCategory, refund.Data.Id, trigger);

                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Sms,
                        Action = AuditActions.SmsDeliveryRefunded,
                        EntityType = AuditEntityTypes.SmsSend,
                        EntityId = record.Id.ToString(),
                        ActorUserId = record.UserId,
                        TargetUserId = record.UserId,
                        Succeeded = true,
                        Metadata = new
                        {
                            record.Sid,
                            record.SourceModule,
                            record.SourceEntityId,
                            record.ChargedAmount,
                            record.PartsCount,
                            record.DeliveryCategory,
                            record.ProviderStatusCode,
                            walletTransactionId = refund.Data.Id,
                            trigger
                        }
                    });
                }
                catch (Exception ex)
                {
                    await _repository.ClearWalletRefundClaimAsync(record.Id);
                    record.WalletRefundedAt = null;

                    _logger.LogError(ex,
                        "CRITICAL: SMS delivery wallet refund exception — RecordId: {RecordId}, UserId: {UserId}, Amount: {Amount}, Sid: {Sid}, Trigger: {Trigger}",
                        record.Id, record.UserId, record.ChargedAmount, record.Sid, trigger);

                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Sms,
                        Action = AuditActions.SmsDeliveryRefundFailed,
                        EntityType = AuditEntityTypes.SmsSend,
                        EntityId = record.Id.ToString(),
                        ActorUserId = record.UserId,
                        TargetUserId = record.UserId,
                        Succeeded = false,
                        ErrorMessage = "WalletRefundException",
                        Metadata = new
                        {
                            record.Sid,
                            record.SourceModule,
                            record.ChargedAmount,
                            record.DeliveryCategory,
                            trigger
                        }
                    });
                }
            }
        }

        private static SmsDeliveryRecordDto MapToDto(SmsDeliveryRecord record)
        {
            var categoryLabel = !string.IsNullOrWhiteSpace(record.ProviderStatusMessage)
                ? record.ProviderStatusMessage
                : SmsDeliveryCategories.GetPersianLabel(record.DeliveryCategory);

            return new SmsDeliveryRecordDto
            {
                Id = record.Id,
                SourceModule = record.SourceModule,
                SourceModuleLabel = SmsSourceModules.GetPersianLabel(record.SourceModule),
                SourceEntityId = record.SourceEntityId,
                SourceEntityLabel = record.SourceEntityLabel,
                Mobile = record.Mobile,
                Sid = record.Sid,
                MessageText = record.MessageText,
                SendStatus = record.SendStatus,
                DeliveryCategory = record.DeliveryCategory,
                DeliveryCategoryLabel = categoryLabel,
                ProviderStatusCode = record.ProviderStatusCode,
                ProviderStatusMessage = record.ProviderStatusMessage,
                IsDeliveryFinal = record.IsDeliveryFinal,
                ChargedAmount = record.ChargedAmount,
                PartsCount = record.PartsCount,
                WalletRefundedAt = record.WalletRefundedAt,
                IsWalletRefunded = record.WalletRefundTransactionId.HasValue,
                SentAt = record.SentAt,
                LastCheckedAt = record.LastCheckedAt
            };
        }
    }
}
