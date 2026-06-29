using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class SmsDeliveryTrackingService : ISmsDeliveryTrackingService
    {
        private readonly ISmsDeliveryRecordRepository _repository;
        private readonly ISmsService _smsService;
        private readonly ILogger<SmsDeliveryTrackingService> _logger;
        private readonly int _maxCheckAttempts;
        private readonly int _minAgeBeforeFirstCheckMinutes;
        private readonly int _maxSidsPerSyncBatch;

        public SmsDeliveryTrackingService(
            ISmsDeliveryRecordRepository repository,
            ISmsService smsService,
            IConfiguration configuration,
            ILogger<SmsDeliveryTrackingService> logger)
        {
            _repository = repository;
            _smsService = smsService;
            _logger = logger;
            _maxCheckAttempts = configuration.GetValue("Sms:DeliverySync:MaxCheckAttempts", 48);
            _minAgeBeforeFirstCheckMinutes = configuration.GetValue("Sms:DeliverySync:MinAgeBeforeFirstCheckMinutes", 60);
            _maxSidsPerSyncBatch = configuration.GetValue("Sms:DeliverySync:MaxSidsPerBatch", 50);
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

                var record = new SmsDeliveryRecord
                {
                    UserId = request.UserId,
                    SourceModule = request.SourceModule,
                    SourceEntityId = request.SourceEntityId,
                    SourceEntityLabel = request.SourceEntityLabel,
                    Mobile = request.Mobile.Trim(),
                    Sid = request.Sid,
                    SendStatus = SmsSendStatuses.Sent,
                    DeliveryCategory = SmsDeliveryCategories.PendingSync,
                    SentAt = sentAtUtc,
                    CreatedAt = DateTime.UtcNow
                };

                await _repository.AddAsync(record);
                await _repository.SaveChangesAsync();

                _logger.LogInformation(
                    "SMS delivery record created — RecordId: {RecordId}, UserId: {UserId}, Module: {Module}, EntityId: {EntityId}, Mobile: {Mobile}, Sid: {Sid}, SentAtUtc: {SentAtUtc:yyyy-MM-dd HH:mm:ss}, Label: {Label}",
                    record.Id,
                    record.UserId,
                    record.SourceModule,
                    record.SourceEntityId,
                    record.Mobile,
                    record.Sid,
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
                PageSize = filter.PageSize
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
                "SMS delivery manual refresh completed — RecordId: {RecordId}, Sid: {Sid}, StatusCode: {StatusCode}, StatusMessage: {StatusMessage}, Category: {Category}, IsFinal: {IsFinal}",
                id, updated!.Sid, updated.ProviderStatusCode, updated.ProviderStatusMessage ?? "-", updated.DeliveryCategory, updated.IsDeliveryFinal);

            return ApiResponse<SmsDeliveryRecordDto>.CreateSuccess(dto);
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

            var trigger = isManualRefresh ? "ManualRefresh" : "BackgroundJob";
            var recordIds = string.Join(", ", activeRecords.Select(r => r.Id));
            var mobiles = string.Join(", ", activeRecords.Select(r => r.Mobile));

            _logger.LogInformation(
                "SMS delivery sync for Sid {Sid} — Trigger: {Trigger}, ActiveRecords: {Count}, RecordIds: [{RecordIds}], Mobiles: [{Mobiles}]",
                sid, trigger, activeRecords.Count, recordIds, mobiles);

            var deliveryResult = await _smsService.GetDeliveryStatusAsync(sid);
            var nowUtc = DateTime.UtcNow;

            foreach (var record in activeRecords)
            {
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
                SendStatus = record.SendStatus,
                DeliveryCategory = record.DeliveryCategory,
                DeliveryCategoryLabel = categoryLabel,
                ProviderStatusCode = record.ProviderStatusCode,
                ProviderStatusMessage = record.ProviderStatusMessage,
                IsDeliveryFinal = record.IsDeliveryFinal,
                SentAt = record.SentAt,
                LastCheckedAt = record.LastCheckedAt
            };
        }
    }
}
