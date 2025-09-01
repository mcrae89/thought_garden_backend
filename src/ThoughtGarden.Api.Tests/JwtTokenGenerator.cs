using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace ThoughtGarden.Api.Tests.Utils
{
    public static class JwtTokenGenerator
    {
        public static string GenerateToken(
            string key,
            string issuer,
            string audience,
            int userId,
            string username,
            string email,
            string role = "User")
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),   // ✅ needed for GetProfile
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("username", username),
                new Claim(ClaimTypes.Role, role),                          // ✅ role-based auth
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer,
                audience,
                claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
