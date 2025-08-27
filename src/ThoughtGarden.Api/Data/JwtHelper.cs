using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Data
{
    public static class JwtHelper
    {
        private static readonly string Key = "SuperSecretKey_ChangeThis"; // move to appsettings.json
        private static readonly string Issuer = "ThoughtGarden";
        private static readonly string Audience = "ThoughtGardenMobile";

        public static string GenerateToken(User user)
        {
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("username", user.UserName)
        };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(Issuer, Audience, claims,
                expires: DateTime.UtcNow.AddHours(2), signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}
