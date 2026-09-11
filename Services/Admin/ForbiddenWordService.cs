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
    public class ForbiddenWordService : IForbiddenWordService
    {
        private readonly IForbiddenWordRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _audit;
        private readonly ILogger<ForbiddenWordService> _logger;

        public ForbiddenWordService(
            IForbiddenWordRepository repository,
            IMemoryCache cache,
            IAuditService audit,
            ILogger<ForbiddenWordService> logger)
        {
            _repository = repository;
            _cache = cache;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<List<ForbiddenWordResponseDto>>> GetAllAsync(
            bool includeInactive = true,
            string? search = null)
        {
            try
            {
                var items = (await _repository.GetAllAsync(includeInactive, search))
                    .Select(Map)
                    .ToList();

                return ApiResponse<List<ForbiddenWordResponseDto>>.CreateSuccess(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت کلمات فیلتر");
                return ApiResponse<List<ForbiddenWordResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<List<string>>> GetActiveWordsAsync()
        {
            try
            {
                var words = (await GetActivePairsCachedAsync()).Select(p => p.Display).ToList();
                return ApiResponse<List<string>>.CreateSuccess(words);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت کلمات فیلتر فعال");
                return ApiResponse<List<string>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ForbiddenWordBulkCreateResultDto>> CreateAsync(CreateForbiddenWordDto dto)
        {
            try
            {
                var rawParts = SplitWords(dto.Word);
                if (rawParts.Count == 0)
                {
                    return ApiResponse<ForbiddenWordBulkCreateResultDto>.BadRequest(
                        "کلمه فیلتر الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (rawParts.Count > ForbiddenWordLimits.MaxBulkCreate)
                {
                    return ApiResponse<ForbiddenWordBulkCreateResultDto>.BadRequest(
                        $"حداکثر {ForbiddenWordLimits.MaxBulkCreate} کلمه در هر ثبت مجاز است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var prepared = new List<(string Display, string Normalized)>();
                var invalid = new List<string>();

                foreach (var part in rawParts)
                {
                    var display = part.Trim();
                    if (display.Length > ForbiddenWordLimits.MaxWordLength)
                    {
                        invalid.Add(display);
                        continue;
                    }

                    var normalized = ForbiddenWordMatcher.Normalize(display);
                    if (string.IsNullOrEmpty(normalized))
                    {
                        invalid.Add(display);
                        continue;
                    }

                    if (prepared.All(p => p.Normalized != normalized))
                        prepared.Add((display, normalized));
                }

                if (invalid.Count > 0 && prepared.Count == 0)
                {
                    return ApiResponse<ForbiddenWordBulkCreateResultDto>.BadRequest(
                        "هیچ کلمه معتبری برای ثبت یافت نشد",
                        invalid,
                        ErrorCodes.ValidationFailed);
                }

                var existing = await _repository.GetExistingNormalizedAsync(prepared.Select(p => p.Normalized));
                var toCreate = new List<ForbiddenWord>();
                var skipped = new List<string>();

                foreach (var (display, normalized) in prepared)
                {
                    if (existing.Contains(normalized))
                    {
                        skipped.Add(display);
                        continue;
                    }

                    toCreate.Add(new ForbiddenWord
                    {
                        Word = display,
                        NormalizedWord = normalized,
                        IsActive = dto.IsActive,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (toCreate.Count > 0)
                {
                    _repository.AddRange(toCreate);
                    await _repository.SaveChangesAsync();
                    InvalidateCache();

                    foreach (var entity in toCreate)
                    {
                        await _audit.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Admin,
                            Action = AuditActions.ForbiddenWordCreated,
                            EntityType = AuditEntityTypes.ForbiddenWord,
                            EntityId = entity.Id.ToString(),
                            After = Snapshot(entity)
                        });
                    }
                }

                var result = new ForbiddenWordBulkCreateResultDto
                {
                    CreatedCount = toCreate.Count,
                    SkippedDuplicateCount = skipped.Count,
                    Created = toCreate.Select(Map).ToList(),
                    SkippedDuplicates = skipped
                };

                if (toCreate.Count == 0 && skipped.Count > 0)
                {
                    return ApiResponse<ForbiddenWordBulkCreateResultDto>.BadRequest(
                        "همه کلمات واردشده از قبل ثبت شده‌اند",
                        skipped,
                        ErrorCodes.ValidationFailed);
                }

                var message = toCreate.Count == 1
                    ? "کلمه فیلتر ثبت شد"
                    : $"{toCreate.Count} کلمه فیلتر ثبت شد";

                if (skipped.Count > 0)
                    message += $" ({skipped.Count} مورد تکراری نادیده گرفته شد)";

                return ApiResponse<ForbiddenWordBulkCreateResultDto>.CreateSuccess(result, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد کلمه فیلتر");
                return ApiResponse<ForbiddenWordBulkCreateResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ForbiddenWordResponseDto>> UpdateAsync(int id, UpdateForbiddenWordDto dto)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<ForbiddenWordResponseDto>.NotFound("کلمه فیلتر یافت نشد");

                var display = dto.Word?.Trim() ?? string.Empty;
                var normalized = ForbiddenWordMatcher.Normalize(display);
                if (string.IsNullOrEmpty(normalized))
                {
                    return ApiResponse<ForbiddenWordResponseDto>.BadRequest(
                        "کلمه فیلتر الزامی است",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                if (display.Length > ForbiddenWordLimits.MaxWordLength)
                {
                    return ApiResponse<ForbiddenWordResponseDto>.BadRequest(
                        $"کلمه فیلتر نمی‌تواند بیشتر از {ForbiddenWordLimits.MaxWordLength} کاراکتر باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var duplicate = await _repository.FindByNormalizedAsync(normalized, excludeId: id);
                if (duplicate != null)
                {
                    return ApiResponse<ForbiddenWordResponseDto>.BadRequest(
                        "این کلمه از قبل در فهرست فیلتر وجود دارد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var before = Snapshot(entity);
                entity.Word = display;
                entity.NormalizedWord = normalized;
                entity.IsActive = dto.IsActive;
                entity.UpdatedAt = DateTime.UtcNow;

                await _repository.SaveChangesAsync();
                InvalidateCache();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.ForbiddenWordUpdated,
                    EntityType = AuditEntityTypes.ForbiddenWord,
                    EntityId = entity.Id.ToString(),
                    Before = before,
                    After = Snapshot(entity)
                });

                return ApiResponse<ForbiddenWordResponseDto>.CreateSuccess(Map(entity), "کلمه فیلتر به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی کلمه فیلتر — Id: {Id}", id);
                return ApiResponse<ForbiddenWordResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    return ApiResponse<bool>.NotFound("کلمه فیلتر یافت نشد");

                var before = Snapshot(entity);
                entity.IsDeleted = true;
                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                await _repository.SaveChangesAsync();
                InvalidateCache();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Admin,
                    Action = AuditActions.ForbiddenWordDeleted,
                    EntityType = AuditEntityTypes.ForbiddenWord,
                    EntityId = entity.Id.ToString(),
                    Before = before,
                    After = Snapshot(entity)
                });

                return ApiResponse<bool>.CreateSuccess(true, "کلمه فیلتر حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف کلمه فیلتر — Id: {Id}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ForbiddenWordValidateResultDto>> ValidateTextAsync(ForbiddenWordValidateRequestDto dto)
        {
            try
            {
                var matches = await FindMatchesAsync(dto.Text);
                var result = new ForbiddenWordValidateResultDto
                {
                    IsClean = matches.Count == 0,
                    MatchedWords = matches,
                    Message = matches.Count == 0 ? null : ForbiddenWordMessages.BuildUserMessage(matches)
                };

                if (!result.IsClean)
                {
                    return ApiResponse<ForbiddenWordValidateResultDto>.BadRequest(
                        result.Message!,
                        matches,
                        ErrorCodes.FilteredWord);
                }

                return ApiResponse<ForbiddenWordValidateResultDto>.CreateSuccess(result, "متن بدون کلمه فیلتر است");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در اعتبارسنجی کلمه فیلتر");
                return ApiResponse<ForbiddenWordValidateResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<T>?> TryBlockIfContainsAsync<T>(params string?[] texts)
        {
            var matches = await FindMatchesInTextsAsync(texts);
            if (matches.Count == 0)
                return null;

            return ForbiddenWordMessages.BlockedResponse<T>(matches);
        }

        private async Task<List<string>> FindMatchesAsync(string? text)
        {
            var pairs = await GetActivePairsCachedAsync();
            return ForbiddenWordMatcher.FindMatches(text, pairs);
        }

        private async Task<List<string>> FindMatchesInTextsAsync(params string?[] texts)
        {
            var pairs = await GetActivePairsCachedAsync();
            return ForbiddenWordMatcher.FindMatchesInTexts(texts, pairs);
        }

        private async Task<IReadOnlyList<(string Display, string Normalized)>> GetActivePairsCachedAsync()
        {
            if (_cache.TryGetValue(ForbiddenWordCacheKeys.ActiveNormalizedList,
                    out List<(string Display, string Normalized)>? cached)
                && cached != null)
            {
                return cached;
            }

            var entities = await _repository.GetActiveAsync();
            var pairs = entities
                .Select(e => (e.Word, e.NormalizedWord))
                .ToList();

            _cache.Set(
                ForbiddenWordCacheKeys.ActiveNormalizedList,
                pairs,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    Size = 1
                });

            return pairs;
        }

        private void InvalidateCache() => _cache.Remove(ForbiddenWordCacheKeys.ActiveNormalizedList);

        private static List<string> SplitWords(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<string>();

            return input
                .Split(new[] { '\r', '\n', ',', '،', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        private static ForbiddenWordResponseDto Map(ForbiddenWord entity) => new()
        {
            Id = entity.Id,
            Word = entity.Word,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        private static object Snapshot(ForbiddenWord entity) => new
        {
            entity.Id,
            entity.Word,
            entity.NormalizedWord,
            entity.IsActive,
            entity.IsDeleted
        };
    }
}
