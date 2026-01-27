using Api_Vapp.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت Blacklist برای توکن‌های JWT لغو شده
    /// استفاده از Memory Cache برای ذخیره JTI های لغو شده
    /// </summary>
    public class TokenBlacklistService : ITokenBlacklistService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<TokenBlacklistService> _logger;
        private const string BlacklistPrefix = "Blacklist_JTI_";

        public TokenBlacklistService(IMemoryCache cache, ILogger<TokenBlacklistService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task AddToBlacklistAsync(string jti, int expirationMinutes)
        {
            if (string.IsNullOrWhiteSpace(jti))
            {
                _logger.LogWarning("Attempted to add empty JTI to blacklist");
                return Task.CompletedTask;
            }

            var cacheKey = $"{BlacklistPrefix}{jti}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expirationMinutes),
                Priority = CacheItemPriority.Normal,
                Size = 1
            };

            _cache.Set(cacheKey, true, cacheOptions);
            _logger.LogInformation("JTI {Jti} added to blacklist for {ExpirationMinutes} minutes", jti, expirationMinutes);

            return Task.CompletedTask;
        }

        public Task<bool> IsTokenBlacklistedAsync(string jti)
        {
            if (string.IsNullOrWhiteSpace(jti))
            {
                return Task.FromResult(false);
            }

            var cacheKey = $"{BlacklistPrefix}{jti}";
            var isBlacklisted = _cache.TryGetValue(cacheKey, out _);

            if (isBlacklisted)
            {
                _logger.LogWarning("Blacklisted JTI {Jti} attempted to be used", jti);
            }

            return Task.FromResult(isBlacklisted);
        }

        public Task RemoveFromBlacklistAsync(string jti)
        {
            if (string.IsNullOrWhiteSpace(jti))
            {
                return Task.CompletedTask;
            }

            var cacheKey = $"{BlacklistPrefix}{jti}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("JTI {Jti} removed from blacklist", jti);

            return Task.CompletedTask;
        }
    }
}






