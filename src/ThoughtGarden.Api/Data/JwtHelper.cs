using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Data
{
    public class JwtHelper
    {
        private readonly IConfiguration _config;
        public JwtHelper(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("username", user.UserName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var keyBytes = GetJwtKeyBytes(_config);
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var ttl = GetAccessTokenTtl(_config);
            var now = DateTime.UtcNow;

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer missing"),
                audience: _config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience missing"),
                claims: claims,
                notBefore: now,
                expires: now.Add(ttl),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public static (string token, DateTime expiresUtc, string hash) GenerateRefreshToken(IConfiguration cfg)
        {
            // 64 random bytes -> Base64Url string (no '+'/'/'/'=' to simplify storage)
            Span<byte> buffer = stackalloc byte[64];
            RandomNumberGenerator.Fill(buffer);
            var token = Base64UrlEncode(buffer);
            var expires = DateTime.UtcNow.Add(GetRefreshTokenTtl(cfg));
            var hash = Sha256Hex(token);
            return (token, expires, hash);
        }
        public static byte[] GetJwtKeyBytes(IConfiguration cfg)
        {
            var keyB64 = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
            try
            {
                var bytes = Convert.FromBase64String(keyB64);
                if (bytes.Length < 32) throw new InvalidOperationException("Jwt:Key must decode to at least 32 bytes (256-bit).");
                return bytes;
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Jwt:Key is not valid Base64. Provide a Base64-encoded 256-bit key.");
            }
        }

        public static TimeSpan GetAccessTokenTtl(IConfiguration cfg)
        {
            var raw = cfg["Jwt:AccessTokenMinutes"] ?? throw new InvalidOperationException("Jwt:AccessTokenMinutes missing");
            if (!int.TryParse(raw, out var minutes) || minutes <= 0 || minutes > 1440)
                throw new InvalidOperationException("Jwt:AccessTokenMinutes must be an integer between 1 and 1440.");
            return TimeSpan.FromMinutes(minutes);
        }

        public static TimeSpan GetRefreshTokenTtl(IConfiguration cfg)
        {
            var raw = cfg["Jwt:RefreshTokenDays"] ?? throw new InvalidOperationException("Jwt:RefreshTokenDays missing");
            if (!int.TryParse(raw, out var days) || days <= 0 || days > 365)
                throw new InvalidOperationException("Jwt:RefreshTokenDays must be an integer between 1 and 365.");
            return TimeSpan.FromDays(days);
        }

        public static string Sha256Hex(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes); // store hex in DB
        }

        private static string Base64UrlEncode(ReadOnlySpan<byte> data)
            => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
