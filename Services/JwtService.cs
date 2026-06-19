using Api_Vapp.DTOs.Auth;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Api_Vapp.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _accessTokenExpirationMinutes;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
            _secretKey = _configuration["Jwt:Secret"] ?? throw new ArgumentNullException("Jwt:Secret");
            
            if (string.IsNullOrWhiteSpace(_secretKey) || _secretKey.Length < 32)
            {
                throw new ArgumentException("Jwt:Secret must be at least 32 characters long for HMAC-SHA256 algorithm.", nameof(configuration));
            }
            
            _issuer = _configuration["Jwt:Issuer"] ?? throw new ArgumentNullException("Jwt:Issuer");
            _audience = _configuration["Jwt:Audience"] ?? throw new ArgumentNullException("Jwt:Audience");
            _accessTokenExpirationMinutes = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60");
        }

        public string GenerateAccessToken(User user, IEnumerable<string>? roleNames = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.MobilePhone, user.PhoneNumber),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (roleNames != null)
            {
                foreach (var roleName in roleNames.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    claims.Add(new Claim(ClaimTypes.Role, roleName));
                }
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }

        /// <summary>
        /// استخراج اطلاعات کاربر از ClaimsPrincipal (JWT Token)
        /// </summary>
        /// <param name="principal">ClaimsPrincipal که از HttpContext.User دریافت می‌شود</param>
        /// <returns>اطلاعات توکن شامل UserId، PhoneNumber و TokenId</returns>
        public TokenInfoDto GetTokenInfo(ClaimsPrincipal? principal)
        {
            if (principal == null)
            {
                return new TokenInfoDto();
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var phoneNumberClaim = principal.FindFirst(ClaimTypes.MobilePhone)?.Value;
            var tokenIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            int? userId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int parsedUserId))
            {
                userId = parsedUserId;
            }

            return new TokenInfoDto
            {
                UserId = userId,
                PhoneNumber = phoneNumberClaim,
                TokenId = tokenIdClaim
            };
        }

        /// <summary>
        /// دریافت زمان انقضای Access Token به دقیقه
        /// </summary>
        /// <returns>زمان انقضای Access Token به دقیقه</returns>
        public int GetAccessTokenExpirationMinutes()
        {
            return _accessTokenExpirationMinutes;
        }
    }
}



