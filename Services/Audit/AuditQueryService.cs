using Api_Vapp.Data;
using Api_Vapp.DTOs.Audit;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Audit
{
    public sealed class AuditQueryService : IAuditQueryService
    {
        private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();

        private readonly Api_Context _db;

        public AuditQueryService(Api_Context db)
        {
            _db = db;
        }

        public async Task<ApiResponse<PagedResponse<AuditLogDto>>> SearchAsync(
            AuditSearchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            request ??= new AuditSearchRequestDto();

            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 100);

            var query = _db.AdminAuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Category))
                query = query.Where(x => x.Category == request.Category.Trim());

            if (!string.IsNullOrWhiteSpace(request.Action))
                query = query.Where(x => x.Action == request.Action.Trim());

            if (!string.IsNullOrWhiteSpace(request.EntityType))
                query = query.Where(x => x.EntityType == request.EntityType.Trim());

            if (!string.IsNullOrWhiteSpace(request.EntityId))
                query = query.Where(x => x.EntityId == request.EntityId.Trim());

            if (request.ActorUserId.HasValue)
                query = query.Where(x => x.ActorUserId == request.ActorUserId);

            if (request.TargetUserId.HasValue)
                query = query.Where(x => x.TargetUserId == request.TargetUserId);

            if (!string.IsNullOrWhiteSpace(request.CorrelationId))
                query = query.Where(x => x.CorrelationId == request.CorrelationId.Trim());

            if (!string.IsNullOrWhiteSpace(request.Source))
                query = query.Where(x => x.Source == request.Source.Trim());

            if (request.Succeeded.HasValue)
                query = query.Where(x => x.Succeeded == request.Succeeded.Value);

            if (request.FromUtc.HasValue)
                query = query.Where(x => x.CreatedAt >= request.FromUtc.Value);

            if (request.ToUtc.HasValue)
                query = query.Where(x => x.CreatedAt <= request.ToUtc.Value);

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var q = request.Q.Trim();
                if (request.SearchInJson)
                {
                    // اختیاری و کندتر — برای جستجوی داخل JSON (مثل قیمت)
                    query = query.Where(x =>
                        x.Action.Contains(q)
                        || (x.EntityId != null && x.EntityId.Contains(q))
                        || (x.CorrelationId != null && x.CorrelationId.Contains(q))
                        || (x.ErrorMessage != null && x.ErrorMessage.Contains(q))
                        || (x.OldValue != null && x.OldValue.Contains(q))
                        || (x.NewValue != null && x.NewValue.Contains(q))
                        || (x.Metadata != null && x.Metadata.Contains(q)));
                }
                else
                {
                    query = query.Where(x =>
                        x.Action.Contains(q)
                        || (x.EntityId != null && x.EntityId.Contains(q))
                        || (x.CorrelationId != null && x.CorrelationId.Contains(q))
                        || (x.ErrorMessage != null && x.ErrorMessage.Contains(q)));
                }
            }

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AuditLogDto
                {
                    Id = x.Id,
                    Category = x.Category,
                    Action = x.Action,
                    EntityType = x.EntityType,
                    EntityId = x.EntityId,
                    ActorUserId = x.ActorUserId,
                    TargetUserId = x.TargetUserId,
                    OldValue = x.OldValue,
                    NewValue = x.NewValue,
                    Metadata = x.Metadata,
                    CorrelationId = x.CorrelationId,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                    RequestPath = x.RequestPath,
                    HttpMethod = x.HttpMethod,
                    Source = x.Source,
                    Succeeded = x.Succeeded,
                    ErrorMessage = x.ErrorMessage,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            foreach (var item in items)
                item.CreatedAtTehran = ToTehran(item.CreatedAt);

            return ApiResponse<PagedResponse<AuditLogDto>>.CreateSuccess(
                PagedResponse<AuditLogDto>.Create(items, total, page, pageSize),
                "لیست audit با موفقیت دریافت شد");
        }

        public async Task<ApiResponse<AuditLogDto>> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var row = await _db.AdminAuditLogs.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AuditLogDto
                {
                    Id = x.Id,
                    Category = x.Category,
                    Action = x.Action,
                    EntityType = x.EntityType,
                    EntityId = x.EntityId,
                    ActorUserId = x.ActorUserId,
                    TargetUserId = x.TargetUserId,
                    OldValue = x.OldValue,
                    NewValue = x.NewValue,
                    Metadata = x.Metadata,
                    CorrelationId = x.CorrelationId,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                    RequestPath = x.RequestPath,
                    HttpMethod = x.HttpMethod,
                    Source = x.Source,
                    Succeeded = x.Succeeded,
                    ErrorMessage = x.ErrorMessage,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row == null)
                return ApiResponse<AuditLogDto>.NotFound("رکورد audit یافت نشد");

            row.CreatedAtTehran = ToTehran(row.CreatedAt);
            return ApiResponse<AuditLogDto>.CreateSuccess(row);
        }

        private static DateTime ToTehran(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TehranTimeZone);

        private static TimeZoneInfo ResolveTehranTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
            }
        }
    }
}
