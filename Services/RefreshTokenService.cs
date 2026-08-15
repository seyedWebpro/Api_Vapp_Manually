using System.Collections.Concurrent;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Auth;
using Api_Vapp.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly Api_Context _context;
        private readonly IConfiguration _configuration;
        private readonly IJwtService _jwtService;
        private readonly ILogger<RefreshTokenService> _logger;
        private readonly int _refreshTokenExpirationDays;
        private readonly int _graceSeconds;

        /// <summary>
        /// فقط برای providerهایی مثل InMemory که ExecuteUpdate ندارند — claim را سریالایز می‌کند.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ClaimLocks = new(StringComparer.Ordinal);

        public RefreshTokenService(
            Api_Context context,
            IConfiguration configuration,
            IJwtService jwtService,
            ILogger<RefreshTokenService> logger)
        {
            _context = context;
            _configuration = configuration;
            _jwtService = jwtService;
            _logger = logger;
            _refreshTokenExpirationDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
            _graceSeconds = Math.Clamp(
                int.Parse(_configuration["Jwt:RefreshTokenGraceSeconds"] ?? "30"),
                5,
                300);
        }

        public async Task<Models.RefreshToken> CreateRefreshTokenAsync(int userId, DateTime? originalExpiresAt = null)
        {
            var expiresAt = originalExpiresAt ?? DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

            var refreshToken = new Models.RefreshToken
            {
                UserId = userId,
                Token = _jwtService.GenerateRefreshToken(),
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<Models.RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token && !rt.IsRevoked);
        }

        public async Task RevokeRefreshTokenAsync(string token)
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken != null && !refreshToken.IsRevoked)
            {
                refreshToken.IsRevoked = true;
                refreshToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task RevokeAllUserTokensAsync(int userId)
        {
            var now = DateTime.UtcNow;
            try
            {
                await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(rt => rt.IsRevoked, true)
                        .SetProperty(rt => rt.RevokedAt, now)
                        .SetProperty(rt => rt.ReplacementToken, (string?)null));
            }
            catch (InvalidOperationException)
            {
                var tokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                    .ToListAsync();
                foreach (var token in tokens)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = now;
                    token.ReplacementToken = null;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsRefreshTokenValidAsync(string token)
        {
            var refreshToken = await GetRefreshTokenAsync(token);

            if (refreshToken == null)
                return false;

            if (refreshToken.IsRevoked)
                return false;

            if (refreshToken.ExpiresAt < DateTime.UtcNow)
                return false;

            if (refreshToken.User != null)
            {
                if (!refreshToken.User.IsActive || refreshToken.User.IsDeleted)
                    return false;
            }

            return true;
        }

        public async Task<RefreshTokenRotationResult> RotateOrReuseAsync(string presentedToken)
        {
            if (string.IsNullOrWhiteSpace(presentedToken))
                return RefreshTokenRotationResult.Invalid();

            var now = DateTime.UtcNow;
            var replacementValue = _jwtService.GenerateRefreshToken();
            var replacementExpiresAt = now.AddDays(_refreshTokenExpirationDays);

            var claimed = await TryClaimAsync(presentedToken, now, replacementValue);

            if (claimed == 1)
            {
                var old = await _context.RefreshTokens
                    .Include(rt => rt.User)
                    .AsTracking()
                    .FirstAsync(rt => rt.Token == presentedToken);

                if (old.User == null || !old.User.IsActive || old.User.IsDeleted)
                {
                    old.ReplacementToken = null;
                    old.ReplacedByTokenId = null;
                    await _context.SaveChangesAsync();
                    _logger.LogWarning(
                        "Refresh rotation claimed for inactive/deleted user {UserId}",
                        old.UserId);
                    return RefreshTokenRotationResult.InactiveUser();
                }

                var neu = new Models.RefreshToken
                {
                    UserId = old.UserId,
                    Token = replacementValue,
                    ExpiresAt = replacementExpiresAt,
                    CreatedAt = now,
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(neu);
                await _context.SaveChangesAsync();

                old.ReplacedByTokenId = neu.Id;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Refresh token rotated for user {UserId} (tokenId {OldId} → {NewId})",
                    old.UserId, old.Id, neu.Id);

                return RefreshTokenRotationResult.Rotated(neu, old.User);
            }

            return await TryGraceReuseAsync(presentedToken, now);
        }

        /// <summary>
        /// Atomic claim روی SQL Server با ExecuteUpdate؛ روی InMemory با قفل process-local.
        /// </summary>
        private async Task<int> TryClaimAsync(string presentedToken, DateTime now, string replacementValue)
        {
            try
            {
                return await _context.RefreshTokens
                    .Where(rt =>
                        rt.Token == presentedToken &&
                        !rt.IsRevoked &&
                        rt.ExpiresAt > now)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(rt => rt.IsRevoked, true)
                        .SetProperty(rt => rt.RevokedAt, now)
                        .SetProperty(rt => rt.ReplacementToken, replacementValue));
            }
            catch (InvalidOperationException)
            {
                var gate = ClaimLocks.GetOrAdd(presentedToken, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync();
                try
                {
                    var old = await _context.RefreshTokens
                        .FirstOrDefaultAsync(rt =>
                            rt.Token == presentedToken &&
                            !rt.IsRevoked &&
                            rt.ExpiresAt > now);

                    if (old == null)
                        return 0;

                    old.IsRevoked = true;
                    old.RevokedAt = now;
                    old.ReplacementToken = replacementValue;
                    await _context.SaveChangesAsync();
                    return 1;
                }
                finally
                {
                    gate.Release();
                }
            }
        }

        private async Task<RefreshTokenRotationResult> TryGraceReuseAsync(string presentedToken, DateTime now)
        {
            var grace = TimeSpan.FromSeconds(_graceSeconds);

            for (var attempt = 0; attempt < 15; attempt++)
            {
                var existing = await _context.RefreshTokens
                    .Include(rt => rt.User)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(rt => rt.Token == presentedToken);

                if (existing == null)
                    return RefreshTokenRotationResult.Invalid();

                if (existing.User == null || !existing.User.IsActive || existing.User.IsDeleted)
                    return RefreshTokenRotationResult.InactiveUser();

                if (!existing.IsRevoked)
                {
                    if (existing.ExpiresAt <= now)
                        return RefreshTokenRotationResult.Invalid();

                    await Task.Delay(20);
                    continue;
                }

                var revokedAt = existing.RevokedAt ?? existing.CreatedAt;
                if (now - revokedAt > grace)
                {
                    _logger.LogWarning(
                        "Refresh token reuse outside grace window for user {UserId}",
                        existing.UserId);
                    return RefreshTokenRotationResult.Invalid();
                }

                if (string.IsNullOrWhiteSpace(existing.ReplacementToken))
                {
                    await Task.Delay(20);
                    continue;
                }

                var replacement = await _context.RefreshTokens
                    .Include(rt => rt.User)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(rt => rt.Token == existing.ReplacementToken);

                if (replacement == null)
                {
                    await Task.Delay(20);
                    replacement = await _context.RefreshTokens
                        .Include(rt => rt.User)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(rt => rt.Token == existing.ReplacementToken);

                    if (replacement == null)
                    {
                        var synthetic = new Models.RefreshToken
                        {
                            Id = existing.ReplacedByTokenId ?? 0,
                            UserId = existing.UserId,
                            Token = existing.ReplacementToken,
                            ExpiresAt = now.AddDays(_refreshTokenExpirationDays),
                            CreatedAt = now,
                            IsRevoked = false,
                            User = existing.User
                        };

                        _logger.LogInformation(
                            "Refresh grace reuse (synthetic) for user {UserId}",
                            existing.UserId);
                        return RefreshTokenRotationResult.GraceReuse(synthetic, existing.User);
                    }
                }

                if (replacement.IsRevoked)
                {
                    if (!string.IsNullOrWhiteSpace(replacement.ReplacementToken)
                        && replacement.RevokedAt.HasValue
                        && now - replacement.RevokedAt.Value <= grace)
                    {
                        var nested = await _context.RefreshTokens
                            .Include(rt => rt.User)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(rt =>
                                rt.Token == replacement.ReplacementToken && !rt.IsRevoked);

                        if (nested != null && nested.ExpiresAt > now && nested.User != null)
                        {
                            _logger.LogInformation(
                                "Refresh grace reuse (nested) for user {UserId}",
                                nested.UserId);
                            return RefreshTokenRotationResult.GraceReuse(nested, nested.User);
                        }
                    }

                    await Task.Delay(20);
                    continue;
                }

                if (replacement.ExpiresAt <= now || replacement.User == null)
                    return RefreshTokenRotationResult.Invalid();

                _logger.LogInformation(
                    "Refresh grace reuse for user {UserId} (presented token already rotated)",
                    existing.UserId);
                return RefreshTokenRotationResult.GraceReuse(replacement, replacement.User);
            }

            return RefreshTokenRotationResult.Invalid();
        }
    }
}
