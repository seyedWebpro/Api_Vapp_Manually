using Api_Vapp.Configuration;
using Api_Vapp.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    /// <summary>
    /// Rate limit در حافظه per-user — سبک و سریع برای اسکرپ/import.
    /// </summary>
    public class NumberSeekerRateLimiter : INumberSeekerRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly NumberSeekerOptions _options;

        public NumberSeekerRateLimiter(IMemoryCache cache, IOptions<NumberSeekerOptions> options)
        {
            _cache = cache;
            _options = options.Value;
        }

        public Task<(bool Allowed, int? RetryAfterSeconds)> CheckScrapeAsync(int userId)
            => CheckAsync($"ns_scrape_{userId}", _options.MaxScrapesPerHour);

        public Task<(bool Allowed, int? RetryAfterSeconds)> CheckImportAsync(int userId)
            => CheckAsync($"ns_import_{userId}", _options.MaxImportsPerHour);

        public Task RecordScrapeAsync(int userId)
        {
            Record($"ns_scrape_{userId}", _options.MaxScrapesPerHour);
            return Task.CompletedTask;
        }

        public Task RecordImportAsync(int userId)
        {
            Record($"ns_import_{userId}", _options.MaxImportsPerHour);
            return Task.CompletedTask;
        }

        private Task<(bool Allowed, int? RetryAfterSeconds)> CheckAsync(string key, int maxPerHour)
        {
            if (maxPerHour <= 0)
                return Task.FromResult((true, (int?)null));

            if (!_cache.TryGetValue(key, out RateBucket? bucket) || bucket == null)
                return Task.FromResult((true, (int?)null));

            if (bucket.Count < maxPerHour)
                return Task.FromResult((true, (int?)null));

            var retryAfter = (int)Math.Max(1, (bucket.WindowEndsAt - DateTimeOffset.UtcNow).TotalSeconds);
            return Task.FromResult((false, (int?)retryAfter));
        }

        private void Record(string key, int maxPerHour)
        {
            if (maxPerHour <= 0)
                return;

            var now = DateTimeOffset.UtcNow;
            var bucket = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                entry.SetSize(1);
                return new RateBucket { WindowEndsAt = now.AddHours(1) };
            })!;

            if (bucket.WindowEndsAt <= now)
            {
                bucket.Count = 0;
                bucket.WindowEndsAt = now.AddHours(1);
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
