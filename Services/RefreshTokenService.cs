using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Api_Vapp.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly Api_Context _context;
        private readonly IConfiguration _configuration;
        private readonly IJwtService _jwtService;
        private readonly int _refreshTokenExpirationDays;

        public RefreshTokenService(
            Api_Context context, 
            IConfiguration configuration,
            IJwtService jwtService)
        {
            _context = context;
            _configuration = configuration;
            _jwtService = jwtService;
            _refreshTokenExpirationDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
        }

        public async Task<Models.RefreshToken> CreateRefreshTokenAsync(int userId, DateTime? originalExpiresAt = null)
        {
            // اگر originalExpiresAt مشخص شده باشد (برای Refresh Token جدید)، از همان استفاده می‌کنیم
            // در غیر این صورت، از زمان فعلی + Expiration Days استفاده می‌کنیم
            var expiresAt = originalExpiresAt ?? DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);
            
            var refreshToken = new Models.RefreshToken
            {
                UserId = userId,
                Token = _jwtService.GenerateRefreshToken(), // استفاده از JwtService برای تولید امن
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

            if (refreshToken != null)
            {
                refreshToken.IsRevoked = true;
                refreshToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task RevokeAllUserTokensAsync(int userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
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

            // بررسی وضعیت کاربر
            if (refreshToken.User != null)
            {
                if (!refreshToken.User.IsActive || refreshToken.User.IsDeleted)
                    return false;
            }

            return true;
        }
    }
}

