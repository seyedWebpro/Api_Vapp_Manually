using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace Api_Vapp.Services.Admin
{
    public class AppNewsTickerService : IAppNewsTickerService
    {
        private readonly IAppNewsTickerRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _audit;
        private readonly ILogger<AppNewsTickerService> _logger;

        public AppNewsTickerService(
            IAppNewsTickerRepository repository,
            IMemoryCache cache,
            IAuditService audit,
            ILogger<AppNewsTickerService> logger)
        {
            _repository = repository;
            _cache = cache;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<List<AppNewsTickerMessageResponseDto>>> GetAllAsync(bool includeInactive = true)
        {
            try
            {
                var messages = (await _repository.GetAllAsync(includeInactive)).Select(Map).ToList();

                return ApiResponse<List<AppNewsTickerMessageResponseDto>>.CreateSuccess(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت زیرنویس‌های خبری اپ");
                return ApiResponse<List<AppNewsTickerMessageResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<List<AppNewsTickerMessageResponseDto>>> GetActiveAsync()
        {
            try
            {
                if (_cache.TryGetValue(AppNewsTickerCacheKeys.ActiveList, out List<AppNewsTickerMessageResponseDto>? cached)
                    && cached != null)
                {
                    return ApiResponse<List<AppNewsTickerMessageResponseDto>>.CreateSuccess(cached);
                }

                var messages = (await _repository.GetActiveAsync()).Select(Map).ToList();

                _cache.Set(
                    AppNewsTickerCacheKeys.ActiveList,
                    messages,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        Size = 1
                    });

                return ApiResponse<List<AppNewsTickerMessageResponseDto>>.CreateSuccess(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت زیرنویس‌های خبری فعال اپ");
                return ApiResponse<List<AppNewsTickerMessageResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AppNewsTickerMessageResponseDto>> CreateAsync(CreateAppNewsTickerMessageDto dto)
        {
            try
            {
                var text = NormalizeText(dto.Text);
                if (text == null)
                    return ValidationError<AppNewsTickerMessageResponseDto>();

                var entity = new AppNewsTickerMessage
                {
                    Text = text,
                    SortOrder = dto.SortOrder,
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.UtcNow
                };

                _repository.Add(entity);
                await _repository.SaveChangesAsync();
                InvalidateCache();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AppNewsTickerCreated,
                    EntityType = AuditEntityTypes.AppNewsTicker,
                    EntityId = entity.Id.ToString(),
                    After = Snapshot(entity)
                });

                return ApiResponse<AppNewsTickerMessageResponseDto>.CreateSuccess(Map(entity), "متن زیرنویس ایجاد شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد زیرنویس خبری اپ");
                return ApiResponse<AppNewsTickerMessageResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AppNewsTickerMessageResponseDto>> UpdateAsync(int id, UpdateAppNewsTickerMessageDto dto)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<AppNewsTickerMessageResponseDto>.NotFound("متن زیرنویس یافت نشد");

                var text = NormalizeText(dto.Text);
                if (text == null)
                    return ValidationError<AppNewsTickerMessageResponseDto>();

                var before = Snapshot(entity);
                entity.Text = text;
                entity.SortOrder = dto.SortOrder;
                entity.IsActive = dto.IsActive;
                entity.UpdatedAt = DateTime.UtcNow;

                await _repository.SaveChangesAsync();
                InvalidateCache();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AppNewsTickerUpdated,
                    EntityType = AuditEntityTypes.AppNewsTicker,
                    EntityId = entity.Id.ToString(),
                    Before = before,
                    After = Snapshot(entity)
                });

                return ApiResponse<AppNewsTickerMessageResponseDto>.CreateSuccess(Map(entity), "متن زیرنویس به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی زیرنویس خبری اپ — Id: {Id}", id);
                return ApiResponse<AppNewsTickerMessageResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<bool>.NotFound("متن زیرنویس یافت نشد");

                var before = Snapshot(entity);
                entity.IsDeleted = true;
                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                await _repository.SaveChangesAsync();
                InvalidateCache();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.AppNewsTickerDeleted,
                    EntityType = AuditEntityTypes.AppNewsTicker,
                    EntityId = entity.Id.ToString(),
                    Before = before,
                    After = Snapshot(entity)
                });

                return ApiResponse<bool>.CreateSuccess(true, "متن زیرنویس حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف زیرنویس خبری اپ — Id: {Id}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static string? NormalizeText(string? text)
        {
            var normalized = text?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static ApiResponse<T> ValidationError<T>() =>
            ApiResponse<T>.BadRequest(
                "متن زیرنویس الزامی است",
                errorCode: ErrorCodes.ValidationFailed);

        private void InvalidateCache() => _cache.Remove(AppNewsTickerCacheKeys.ActiveList);

        private static AppNewsTickerMessageResponseDto Map(AppNewsTickerMessage entity) => new()
        {
            Id = entity.Id,
            Text = entity.Text,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        private static object Snapshot(AppNewsTickerMessage entity) => new
        {
            entity.Id,
            entity.Text,
            entity.SortOrder,
            entity.IsActive,
            entity.IsDeleted
        };
    }
}
