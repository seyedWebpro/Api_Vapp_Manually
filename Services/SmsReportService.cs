using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class SmsReportService : ISmsReportService
    {
        private const int MaxExcelExportRows = 20_000;

        private readonly ISmsDeliveryRecordRepository _repository;
        private readonly ISmsDeliveryTrackingService _deliveryTracking;
        private readonly ISmsPricingService _smsPricing;
        private readonly ILogger<SmsReportService> _logger;
        private readonly INumberSeekerPhoneAccessService _phoneAccess;
        private readonly string _senderNumber;

        public SmsReportService(
            ISmsDeliveryRecordRepository repository,
            ISmsDeliveryTrackingService deliveryTracking,
            ISmsPricingService smsPricing,
            IConfiguration configuration,
            ILogger<SmsReportService> logger,
            INumberSeekerPhoneAccessService phoneAccess)
        {
            _repository = repository;
            _deliveryTracking = deliveryTracking;
            _smsPricing = smsPricing;
            _logger = logger;
            _phoneAccess = phoneAccess;
            _senderNumber = configuration["Sms:SenderNumber"] ?? string.Empty;
        }

        public Task<ApiResponse<SmsReportFilterOptionsDto>> GetFilterOptionsAsync()
        {
            var options = new SmsReportFilterOptionsDto
            {
                SendTypes = SmsSendTypeFilters.PersianLabels
                    .Select(kv => new SmsReportFilterOptionDto { Value = kv.Key, Label = kv.Value })
                    .ToList(),
                DateRangePresets = SmsReportDateRangePresets.PersianLabels
                    .Where(kv => kv.Key != SmsReportDateRangePresets.Custom)
                    .Select(kv => new SmsReportFilterOptionDto { Value = kv.Key, Label = kv.Value })
                    .ToList(),
                DeliveryCategories = SmsDeliveryCategories.PersianLabels
                    .Select(kv => new SmsReportFilterOptionDto { Value = kv.Key, Label = kv.Value })
                    .ToList()
            };

            return Task.FromResult(ApiResponse<SmsReportFilterOptionsDto>.CreateSuccess(options));
        }

        public async Task<ApiResponse<SmsSendBatchListDto>> GetSendBatchesAsync(int userId, SmsSendListFilterDto filter)
        {
            try
            {
                var validationError = ValidateAndNormalizeListFilter(filter);
                if (validationError != null)
                    return ApiResponse<SmsSendBatchListDto>.BadRequest(validationError, errorCode: ErrorCodes.InvalidInput);

                var (items, totalCount) = await _repository.GetSendBatchesAsync(userId, filter);
                var partsMap = await LoadPartsMapAsync(items);
                var sidMessageTexts = await _repository.GetSampleMessageTextsBySidsAsync(
                    userId, items.Where(i => !i.IsCampaignBatch).Select(i => i.Sid));
                var campaignMessageTexts = await _repository.GetSampleMessageTextsByCampaignIdsAsync(
                    userId, items.Where(i => i.IsCampaignBatch && i.SourceEntityId.HasValue)
                        .Select(i => i.SourceEntityId!.Value));
                var pricingRules = (await _smsPricing.GetRuntimeAsync()).Rules;

                var dtoItems = items.Select(item =>
                {
                    string? sampleMessage = null;
                    if (item.IsCampaignBatch && item.SourceEntityId.HasValue)
                        campaignMessageTexts.TryGetValue(item.SourceEntityId.Value, out sampleMessage);
                    else
                        sidMessageTexts.TryGetValue(item.Sid, out sampleMessage);

                    var (sendType, sendTypeLabel) = MapSendType(item.SourceModule);
                    return new SmsSendBatchListItemDto
                    {
                        Sid = item.Sid,
                        SendId = item.SendId,
                        IsCampaignBatch = item.IsCampaignBatch,
                        Title = item.Title ?? (item.IsCampaignBatch
                            ? $"کمپین #{item.SendId}"
                            : $"ارسال #{item.Sid}"),
                        SourceModule = item.SourceModule,
                        SourceModuleLabel = SmsSourceModules.GetPersianLabel(item.SourceModule),
                        SendType = sendType,
                        SendTypeLabel = sendTypeLabel,
                        SourceEntityId = item.SourceEntityId,
                        SendCount = item.SendCount,
                        PartsCount = ResolvePartsCount(item, partsMap, sampleMessage, pricingRules),
                        SentAt = item.SentAt
                    };
                }).ToList();

                var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize);

                _logger.LogInformation(
                    "SMS send batches listed — UserId: {UserId}, Total: {Total}, Page: {Page}/{PageSize}, SendType: {SendType}, Search: {Search}",
                    userId, totalCount, filter.PageNumber, filter.PageSize, filter.SendType ?? "-", filter.Search ?? "-");

                return ApiResponse<SmsSendBatchListDto>.CreateSuccess(new SmsSendBatchListDto
                {
                    Items = dtoItems,
                    TotalCount = totalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = totalPages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS send batches list failed — UserId: {UserId}", userId);
                return ApiResponse<SmsSendBatchListDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsSendBatchDetailDto>> GetSendBatchDetailAsync(int userId, long sid)
        {
            try
            {
                if (sid <= 0)
                    return ApiResponse<SmsSendBatchDetailDto>.BadRequest("کد ارسال نامعتبر است");

                var batch = await _repository.GetSendBatchBySidAsync(userId, sid);
                if (batch == null)
                    return ApiResponse<SmsSendBatchDetailDto>.NotFound("ارسال مورد نظر یافت نشد");

                return await BuildBatchDetailAsync(userId, batch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS send batch detail failed — UserId: {UserId}, Sid: {Sid}", userId, sid);
                return ApiResponse<SmsSendBatchDetailDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsSendBatchDetailDto>> GetSendBatchDetailByCampaignAsync(int userId, int campaignId)
        {
            try
            {
                if (campaignId <= 0)
                    return ApiResponse<SmsSendBatchDetailDto>.BadRequest("شناسه کمپین نامعتبر است");

                var batch = await _repository.GetSendBatchByCampaignAsync(userId, campaignId);
                if (batch == null)
                    return ApiResponse<SmsSendBatchDetailDto>.NotFound("ارسال مورد نظر یافت نشد");

                return await BuildBatchDetailAsync(userId, batch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS campaign batch detail failed — UserId: {UserId}, CampaignId: {CampaignId}", userId, campaignId);
                return ApiResponse<SmsSendBatchDetailDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<SmsSendBatchDetailDto>> BuildBatchDetailAsync(int userId, SmsSendBatchProjection batch)
        {
            SmsDeliverySummaryDto summary;
            if (batch.IsCampaignBatch && batch.SourceEntityId.HasValue)
                summary = await _repository.GetSummaryByCampaignAsync(userId, batch.SourceEntityId.Value);
            else
                summary = await _repository.GetSummaryBySidAsync(userId, batch.Sid);

            var partsMap = await LoadPartsMapAsync(new[] { batch });
            var messageText = await ResolveBatchMessageTextAsync(userId, batch);
            var (sendType, sendTypeLabel) = MapSendType(batch.SourceModule);
            var pricingRules = (await _smsPricing.GetRuntimeAsync()).Rules;

            var dto = new SmsSendBatchDetailDto
            {
                Sid = batch.Sid,
                SendId = batch.SendId,
                IsCampaignBatch = batch.IsCampaignBatch,
                Title = batch.Title ?? (batch.IsCampaignBatch
                    ? $"کمپین #{batch.SendId}"
                    : $"ارسال #{batch.Sid}"),
                SourceModule = batch.SourceModule,
                SourceModuleLabel = SmsSourceModules.GetPersianLabel(batch.SourceModule),
                SendType = sendType,
                SendTypeLabel = sendTypeLabel,
                SourceEntityId = batch.SourceEntityId,
                SenderNumber = _senderNumber,
                SendCount = batch.SendCount,
                PartsCount = ResolvePartsCount(batch, partsMap, messageText, pricingRules),
                SentAt = batch.SentAt,
                MessageText = messageText,
                Summary = summary
            };

            _logger.LogInformation(
                "SMS send batch detail — UserId: {UserId}, Sid: {Sid}, SendId: {SendId}, Campaign: {IsCampaign}, Count: {Count}",
                userId, batch.Sid, batch.SendId, batch.IsCampaignBatch, batch.SendCount);

            return ApiResponse<SmsSendBatchDetailDto>.CreateSuccess(dto);
        }

        public async Task<ApiResponse<SmsSendRecipientListDto>> GetRecipientsAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter)
        {
            try
            {
                if (sid <= 0)
                    return ApiResponse<SmsSendRecipientListDto>.BadRequest("کد ارسال نامعتبر است");

                NormalizeRecipientFilter(filter);

                if (!await _repository.UserOwnsSidAsync(userId, sid))
                    return ApiResponse<SmsSendRecipientListDto>.NotFound("ارسال مورد نظر یافت نشد");

                var batch = await _repository.GetSendBatchBySidAsync(userId, sid);
                var (items, totalCount) = await _repository.GetRecipientsBySidAsync(userId, sid, filter);
                return await BuildRecipientListResponseAsync(userId, batch, sid, items, totalCount, filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS recipients list failed — UserId: {UserId}, Sid: {Sid}", userId, sid);
                return ApiResponse<SmsSendRecipientListDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsSendRecipientListDto>> GetRecipientsByCampaignAsync(
            int userId, int campaignId, SmsSendRecipientFilterDto filter)
        {
            try
            {
                if (campaignId <= 0)
                    return ApiResponse<SmsSendRecipientListDto>.BadRequest("شناسه کمپین نامعتبر است");

                NormalizeRecipientFilter(filter);

                if (!await _repository.UserOwnsCampaignAsync(userId, campaignId))
                    return ApiResponse<SmsSendRecipientListDto>.NotFound("ارسال مورد نظر یافت نشد");

                var batch = await _repository.GetSendBatchByCampaignAsync(userId, campaignId);
                var (items, totalCount) = await _repository.GetRecipientsByCampaignAsync(userId, campaignId, filter);
                return await BuildRecipientListResponseAsync(userId, batch, batch?.Sid ?? 0, items, totalCount, filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS campaign recipients list failed — UserId: {UserId}, CampaignId: {CampaignId}", userId, campaignId);
                return ApiResponse<SmsSendRecipientListDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<SmsSendRecipientListDto>> BuildRecipientListResponseAsync(
            int userId,
            SmsSendBatchProjection? batch,
            long sid,
            List<SmsDeliveryRecord> items,
            int totalCount,
            SmsSendRecipientFilterDto filter)
        {
            var hiddenMobiles = await _phoneAccess.GetHiddenMobileNumbersAsync(userId);
            var rowOffset = (filter.PageNumber - 1) * filter.PageSize;
            var dtoItems = items
                .Select((record, index) => MapRecipient(record, rowOffset + index + 1, hiddenMobiles))
                .ToList();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize);

            return ApiResponse<SmsSendRecipientListDto>.CreateSuccess(new SmsSendRecipientListDto
            {
                Sid = batch?.Sid ?? sid,
                SendId = batch?.SendId ?? sid,
                IsCampaignBatch = batch?.IsCampaignBatch ?? false,
                Items = dtoItems,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = totalPages
            });
        }

        public async Task<ApiResponse<SmsMessageDetailDto>> GetMessageDetailAsync(int userId, int recordId)
        {
            try
            {
                var record = await _repository.GetByIdAsync(recordId, userId);
                if (record == null)
                    return ApiResponse<SmsMessageDetailDto>.NotFound("پیامک مورد نظر یافت نشد");

                var messageText = record.MessageText;
                if (string.IsNullOrWhiteSpace(messageText))
                    messageText = await ResolveRecordMessageTextAsync(record);

                var categoryLabel = ResolveCategoryLabel(record);
                var hiddenMobiles = await _phoneAccess.GetHiddenMobileNumbersAsync(userId);
                var dto = new SmsMessageDetailDto
                {
                    Id = record.Id,
                    Sid = record.Sid,
                    Mobile = MaskIfHidden(record.Mobile, hiddenMobiles),
                    SenderNumber = _senderNumber,
                    Title = record.SourceEntityLabel ?? $"ارسال #{record.Sid}",
                    SourceModule = record.SourceModule,
                    SourceModuleLabel = SmsSourceModules.GetPersianLabel(record.SourceModule),
                    DeliveryCategory = record.DeliveryCategory,
                    DeliveryCategoryLabel = categoryLabel,
                    StatusHint = BuildStatusHint(record.DeliveryCategory, categoryLabel),
                    SentAt = record.SentAt,
                    MessageText = messageText
                };

                return ApiResponse<SmsMessageDetailDto>.CreateSuccess(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS message detail failed — UserId: {UserId}, RecordId: {RecordId}", userId, recordId);
                return ApiResponse<SmsMessageDetailDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsDeliverySummaryDto>> RefreshSendBatchAsync(int userId, long sid)
        {
            try
            {
                if (sid <= 0)
                    return ApiResponse<SmsDeliverySummaryDto>.BadRequest("کد ارسال نامعتبر است");

                var grouped = await _repository.TryResolveGroupedBatchBySidAsync(userId, sid);
                if (grouped.HasValue)
                {
                    var sids = await _repository.GetDistinctSidsByModuleEntityAsync(
                        userId, grouped.Value.SourceModule, grouped.Value.EntityId);
                    foreach (var batchSid in sids)
                    {
                        await _deliveryTracking.RefreshBySidAsync(userId, batchSid);
                    }

                    var summary = await _repository.GetSummaryBySidAsync(userId, sid);
                    return ApiResponse<SmsDeliverySummaryDto>.CreateSuccess(summary, "وضعیت دلیوری بروزرسانی شد");
                }

                return await _deliveryTracking.RefreshBySidAsync(userId, sid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS send batch refresh failed — UserId: {UserId}, Sid: {Sid}", userId, sid);
                return ApiResponse<SmsDeliverySummaryDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<SmsDeliverySummaryDto>> RefreshSendBatchByCampaignAsync(int userId, int campaignId)
        {
            try
            {
                if (campaignId <= 0)
                    return ApiResponse<SmsDeliverySummaryDto>.BadRequest("شناسه کمپین نامعتبر است");

                if (!await _repository.UserOwnsCampaignAsync(userId, campaignId))
                    return ApiResponse<SmsDeliverySummaryDto>.NotFound("ارسال مورد نظر یافت نشد");

                var sids = await _repository.GetDistinctSidsByCampaignAsync(userId, campaignId);
                foreach (var sid in sids)
                {
                    await _deliveryTracking.RefreshBySidAsync(userId, sid);
                }

                var summary = await _repository.GetSummaryByCampaignAsync(userId, campaignId);
                return ApiResponse<SmsDeliverySummaryDto>.CreateSuccess(summary, "وضعیت دلیوری بروزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS campaign batch refresh failed — UserId: {UserId}, CampaignId: {CampaignId}", userId, campaignId);
                return ApiResponse<SmsDeliverySummaryDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ExportExcelResultDto>> ExportRecipientsToExcelAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter)
        {
            try
            {
                if (sid <= 0)
                    return ApiResponse<ExportExcelResultDto>.BadRequest("کد ارسال نامعتبر است");

                if (!await _repository.UserOwnsSidAsync(userId, sid))
                    return ApiResponse<ExportExcelResultDto>.NotFound("ارسال مورد نظر یافت نشد");

                NormalizeRecipientFilter(filter);

                var batch = await _repository.GetSendBatchBySidAsync(userId, sid);
                var totalMatching = (await _repository.GetRecipientsBySidAsync(userId, sid, new SmsSendRecipientFilterDto
                {
                    Search = filter.Search,
                    DeliveryCategory = filter.DeliveryCategory,
                    PageNumber = 1,
                    PageSize = 1
                })).TotalCount;

                var records = await _repository.GetAllRecipientsBySidForExportAsync(
                    userId, sid, filter, MaxExcelExportRows);

                return await BuildExcelExportAsync(userId, batch, sid, records, totalMatching);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS recipients export failed — UserId: {UserId}, Sid: {Sid}", userId, sid);
                return ApiResponse<ExportExcelResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ExportExcelResultDto>> ExportRecipientsByCampaignToExcelAsync(
            int userId, int campaignId, SmsSendRecipientFilterDto filter)
        {
            try
            {
                if (campaignId <= 0)
                    return ApiResponse<ExportExcelResultDto>.BadRequest("شناسه کمپین نامعتبر است");

                if (!await _repository.UserOwnsCampaignAsync(userId, campaignId))
                    return ApiResponse<ExportExcelResultDto>.NotFound("ارسال مورد نظر یافت نشد");

                NormalizeRecipientFilter(filter);

                var batch = await _repository.GetSendBatchByCampaignAsync(userId, campaignId);
                var totalMatching = (await _repository.GetRecipientsByCampaignAsync(userId, campaignId, new SmsSendRecipientFilterDto
                {
                    Search = filter.Search,
                    DeliveryCategory = filter.DeliveryCategory,
                    PageNumber = 1,
                    PageSize = 1
                })).TotalCount;

                var records = await _repository.GetAllRecipientsByCampaignForExportAsync(
                    userId, campaignId, filter, MaxExcelExportRows);

                return await BuildExcelExportAsync(userId, batch, batch?.Sid ?? 0, records, totalMatching);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS campaign recipients export failed — UserId: {UserId}, CampaignId: {CampaignId}", userId, campaignId);
                return ApiResponse<ExportExcelResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<ExportExcelResultDto>> BuildExcelExportAsync(
            int userId,
            SmsSendBatchProjection? batch,
            long sid,
            List<SmsDeliveryRecord> records,
            int totalMatching)
        {
            var isTruncated = totalMatching > records.Count;
            var sendId = batch?.SendId ?? sid;

            var batchTitle = !string.IsNullOrWhiteSpace(batch?.Title)
                ? batch!.Title!
                : (batch?.IsCampaignBatch == true ? $"کمپین #{sendId}" : $"ارسال #{sid}");
            var batchMessageText = batch != null
                ? await ResolveBatchMessageTextAsync(userId, batch)
                : null;
            var (_, sendTypeLabel) = MapSendType(batch?.SourceModule ?? records.FirstOrDefault()?.SourceModule ?? string.Empty);
            var hiddenMobiles = await _phoneAccess.GetHiddenMobileNumbersAsync(userId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("گیرندگان");

            worksheet.Cell(1, 1).Value = "ردیف";
            worksheet.Cell(1, 2).Value = "شماره موبایل";
            worksheet.Cell(1, 3).Value = "فرستنده";
            worksheet.Cell(1, 4).Value = "وضعیت";
            worksheet.Cell(1, 5).Value = "کد وضعیت";
            worksheet.Cell(1, 6).Value = "پیام وضعیت";
            worksheet.Cell(1, 7).Value = "تاریخ ارسال";
            worksheet.Cell(1, 8).Value = "کد ارسال";
            worksheet.Cell(1, 9).Value = "عنوان";
            worksheet.Cell(1, 10).Value = "نوع ارسال";
            worksheet.Cell(1, 11).Value = "متن پیام";
            worksheet.Cell(1, 12).Value = "وضعیت نهایی";
            worksheet.Row(1).Style.Font.Bold = true;

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var row = i + 2;
                var categoryLabel = ResolveCategoryLabel(record);
                var statusMessage = !string.IsNullOrWhiteSpace(record.ProviderStatusMessage)
                    ? record.ProviderStatusMessage!
                    : categoryLabel;
                var title = !string.IsNullOrWhiteSpace(record.SourceEntityLabel)
                    ? record.SourceEntityLabel!
                    : batchTitle;
                var messageText = !string.IsNullOrWhiteSpace(record.MessageText)
                    ? record.MessageText!
                    : (batchMessageText ?? string.Empty);
                var rowSendTypeLabel = string.IsNullOrWhiteSpace(sendTypeLabel)
                    ? SmsSourceModules.GetPersianLabel(record.SourceModule)
                    : sendTypeLabel;

                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = MaskIfHidden(record.Mobile, hiddenMobiles);
                worksheet.Cell(row, 3).Value = string.IsNullOrWhiteSpace(_senderNumber) ? "-" : _senderNumber;
                worksheet.Cell(row, 4).Value = categoryLabel;
                worksheet.Cell(row, 5).Value = record.ProviderStatusCode?.ToString() ?? "-";
                worksheet.Cell(row, 6).Value = statusMessage;
                worksheet.Cell(row, 7).Value = record.SentAt.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(row, 8).Value = record.Sid.ToString();
                worksheet.Cell(row, 9).Value = title;
                worksheet.Cell(row, 10).Value = rowSendTypeLabel;
                worksheet.Cell(row, 11).Value = string.IsNullOrWhiteSpace(messageText) ? "-" : messageText;
                worksheet.Cell(row, 12).Value = record.IsDeliveryFinal ? "بله" : "خیر";
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileContent = stream.ToArray();

            var safeTitle = string.Join("_", (batch?.Title ?? $"send-{sendId}").Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeTitle))
                safeTitle = $"send-{sendId}";

            var result = new ExportExcelResultDto
            {
                FileContent = fileContent,
                FileName = $"SmsReport_{safeTitle}_{sendId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                TotalCount = totalMatching,
                ExportedCount = records.Count,
                PageNumber = 1,
                PageSize = records.Count,
                TotalPages = 1,
                IsTruncated = isTruncated
            };

            _logger.LogInformation(
                "SMS recipients exported — UserId: {UserId}, Sid: {Sid}, SendId: {SendId}, Exported: {Exported}, Total: {Total}, Truncated: {Truncated}",
                userId, sid, sendId, records.Count, totalMatching, isTruncated);

            var message = isTruncated
                ? $"فایل اکسل با {records.Count} ردیف از {totalMatching} آماده دانلود است (سقف خروجی اعمال شد)"
                : $"فایل اکسل با {records.Count} ردیف آماده دانلود است";

            return ApiResponse<ExportExcelResultDto>.CreateSuccess(result, message);
        }

        private async Task<Dictionary<int, int>> LoadPartsMapAsync(IEnumerable<SmsSendBatchProjection> items)
        {
            var campaignIds = items
                .Where(i => i.SourceModule == SmsSourceModules.MessageCampaign && i.SourceEntityId.HasValue)
                .Select(i => i.SourceEntityId!.Value);

            return await _repository.GetCampaignPartsCountsAsync(campaignIds);
        }

        private static int ResolvePartsCount(
            SmsSendBatchProjection item,
            IReadOnlyDictionary<int, int> partsMap,
            string? messageText = null,
            SmsPartsRules? rules = null)
        {
            if (item.SourceModule == SmsSourceModules.MessageCampaign &&
                item.SourceEntityId.HasValue &&
                partsMap.TryGetValue(item.SourceEntityId.Value, out var parts) &&
                parts > 0)
            {
                return parts;
            }

            if (!string.IsNullOrWhiteSpace(messageText))
            {
                try
                {
                    return SmsPartsCalculator.CalculateParts(messageText, rules ?? SmsPartsRules.Defaults);
                }
                catch (ArgumentException)
                {
                    return 1;
                }
            }

            return 1;
        }

        private async Task<string?> ResolveBatchMessageTextAsync(int userId, SmsSendBatchProjection batch)
        {
            string? stored;
            if (batch.IsCampaignBatch && batch.SourceEntityId.HasValue)
                stored = await _repository.GetSampleMessageTextByCampaignAsync(userId, batch.SourceEntityId.Value);
            else
                stored = await _repository.GetSampleMessageTextBySidAsync(userId, batch.Sid);

            if (!string.IsNullOrWhiteSpace(stored))
                return stored;

            if (batch.SourceModule == SmsSourceModules.MessageCampaign && batch.SourceEntityId.HasValue)
                return await _repository.ResolveCampaignMessageTextAsync(batch.SourceEntityId.Value, string.Empty);

            if (batch.SourceModule == SmsSourceModules.MessageDirect && batch.SourceEntityId.HasValue)
                return await _repository.ResolveDirectMessageTextAsync(batch.SourceEntityId.Value);

            return null;
        }

        private async Task<string?> ResolveRecordMessageTextAsync(SmsDeliveryRecord record)
        {
            if (record.SourceModule == SmsSourceModules.MessageCampaign && record.SourceEntityId.HasValue)
                return await _repository.ResolveCampaignMessageTextAsync(record.SourceEntityId.Value, record.Mobile);

            if (record.SourceModule == SmsSourceModules.MessageDirect && record.SourceEntityId.HasValue)
                return await _repository.ResolveDirectMessageTextAsync(record.SourceEntityId.Value);

            return null;
        }

        private SmsSendRecipientDto MapRecipient(
            SmsDeliveryRecord record,
            int rowNumber,
            IReadOnlySet<string> hiddenMobiles) =>
            new()
            {
                Id = record.Id,
                RowNumber = rowNumber,
                Mobile = MaskIfHidden(record.Mobile, hiddenMobiles),
                SenderNumber = _senderNumber,
                DeliveryCategory = record.DeliveryCategory,
                DeliveryCategoryLabel = ResolveCategoryLabel(record),
                ProviderStatusCode = record.ProviderStatusCode,
                ProviderStatusMessage = record.ProviderStatusMessage,
                IsDeliveryFinal = record.IsDeliveryFinal,
                SentAt = record.SentAt,
                LastCheckedAt = record.LastCheckedAt
            };

        private static string MaskIfHidden(string? mobile, IReadOnlySet<string> hiddenMobiles)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return mobile ?? string.Empty;
            return hiddenMobiles.Contains(mobile) ? PhoneNumberMasker.Mask(mobile) : mobile;
        }

        private static string ResolveCategoryLabel(SmsDeliveryRecord record) =>
            SmsDeliveryCategories.GetPersianLabel(record.DeliveryCategory);

        private static string BuildStatusHint(string category, string label) =>
            category switch
            {
                SmsDeliveryCategories.DeliveredToPhone => "پیامک با موفقیت به گیرنده تحویل شده است.",
                SmsDeliveryCategories.SentToOperator => "پیامک به اپراتور ارسال شده و در مسیر تحویل است.",
                SmsDeliveryCategories.NotDelivered => "پیامک به گوشی گیرنده نرسیده است.",
                SmsDeliveryCategories.PendingApproval => "پیامک در انتظار تایید است.",
                SmsDeliveryCategories.Rejected => "پیامک رد شده است.",
                SmsDeliveryCategories.SendFailed => "ارسال پیامک ناموفق بوده است.",
                _ => $"وضعیت فعلی: {label}"
            };

        private static (string SendType, string Label) MapSendType(string sourceModule)
        {
            if (sourceModule is SmsSourceModules.MessageCampaign
                or SmsSourceModules.MessageDirect
                or SmsSourceModules.AutomatedMessage)
            {
                return (SmsSendTypeFilters.Campaign, SmsSendTypeFilters.GetPersianLabel(SmsSendTypeFilters.Campaign));
            }

            if (sourceModule is SmsSourceModules.Cashback or SmsSourceModules.CashbackScheduled)
            {
                return (SmsSendTypeFilters.Cashback, SmsSendTypeFilters.GetPersianLabel(SmsSendTypeFilters.Cashback));
            }

            if (sourceModule == SmsSourceModules.ReferralProgram)
            {
                return (SmsSendTypeFilters.Reward, SmsSendTypeFilters.GetPersianLabel(SmsSendTypeFilters.Reward));
            }

            return (sourceModule, SmsSourceModules.GetPersianLabel(sourceModule));
        }

        private static string? ValidateAndNormalizeListFilter(SmsSendListFilterDto filter)
        {
            if (filter.PageNumber < 1) filter.PageNumber = 1;
            if (filter.PageSize < 1 || filter.PageSize > 100) filter.PageSize = 20;

            if (!SmsSendTypeFilters.IsValid(filter.SendType))
                return "نوع ارسال نامعتبر است";

            if (!SmsReportDateRangePresets.IsValid(filter.DateRangePreset))
                return "بازه زمانی نامعتبر است";

            if (string.IsNullOrWhiteSpace(filter.SendType))
                filter.SendType = SmsSendTypeFilters.All;

            if (string.IsNullOrWhiteSpace(filter.DateRangePreset))
            {
                filter.DateRangePreset = filter.FromDate.HasValue || filter.ToDate.HasValue
                    ? SmsReportDateRangePresets.Custom
                    : SmsReportDateRangePresets.Last7Days;
            }

            return null;
        }

        private static void NormalizeRecipientFilter(SmsSendRecipientFilterDto filter)
        {
            if (filter.PageNumber < 1) filter.PageNumber = 1;
            if (filter.PageSize < 1 || filter.PageSize > 100) filter.PageSize = 20;
        }
    }
}
