using Api_Vapp.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Api_Vapp.Services.Admin
{
    /// <summary>
    /// Rate limit در حافظه برای پیش‌نمایش ادمین — سبک، بدون Redis.
    /// </summary>
    public class QuickSendPreviewRateLimiter : IQuickSendPreviewRateLimiter
    {
        private const int MaxPreviewReadsPerMinute = 120;
        private const int MaxTokenIssuesPerMinute = 40;

        private readonly IMemoryCache _cache;

        public QuickSendPreviewRateLimiter(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<(bool Allowed, int? RetryAfterSeconds)> CheckPreviewReadAsync(string clientKey)
            => CheckAsync(BuildPreviewReadKey(clientKey), MaxPreviewReadsPerMinute, TimeSpan.FromMinutes(1));

        public Task RecordPreviewReadAsync(string clientKey)
        {
            Record(BuildPreviewReadKey(clientKey), MaxPreviewReadsPerMinute, TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }

        public Task<(bool Allowed, int? RetryAfterSeconds)> CheckTokenIssueAsync(int adminUserId)
            => CheckAsync(BuildTokenIssueKey(adminUserId), MaxTokenIssuesPerMinute, TimeSpan.FromMinutes(1));

        public Task RecordTokenIssueAsync(int adminUserId)
        {
            Record(BuildTokenIssueKey(adminUserId), MaxTokenIssuesPerMinute, TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }

        private static string BuildPreviewReadKey(string clientKey) =>
            $"qs_preview_read_{clientKey}";

        private static string BuildTokenIssueKey(int adminUserId) =>
            $"qs_preview_issue_{adminUserId}";

        private Task<(bool Allowed, int? RetryAfterSeconds)> CheckAsync(
            string key,
            int maxPerWindow,
            TimeSpan window)
        {
            if (maxPerWindow <= 0)
            {
                return Task.FromResult((true, (int?)null));
            }

            if (!_cache.TryGetValue(key, out RateBucket? bucket) || bucket == null)
            {
                return Task.FromResult((true, (int?)null));
            }

            if (bucket.Count < maxPerWindow)
            {
                return Task.FromResult((true, (int?)null));
            }

            var retryAfter = (int)Math.Max(1, (bucket.WindowEndsAt - DateTimeOffset.UtcNow).TotalSeconds);
            return Task.FromResult((false, (int?)retryAfter));
        }

        private void Record(string key, int maxPerWindow, TimeSpan window)
        {
            if (maxPerWindow <= 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var bucket = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = window;
                entry.Size = 1;
                return new RateBucket { WindowEndsAt = now.Add(window) };
            })!;

            if (bucket.WindowEndsAt <= now)
            {
                bucket.Count = 0;
                bucket.WindowEndsAt = now.Add(window);
            }

            bucket.Count++;
            _cache.Set(key, bucket, new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = bucket.WindowEndsAt,
                Size = 1
            });
        }

        private sealed class RateBucket
        {
            public int Count { get; set; }

            public DateTimeOffset WindowEndsAt { get; set; }
        }
    }
}
