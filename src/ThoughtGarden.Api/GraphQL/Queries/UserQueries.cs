using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class UserQueries
    {
        private readonly ThoughtGardenDbContext _db;

        public UserQueries(ThoughtGardenDbContext db) => _db = db;

        [UseProjection]
        public IQueryable<User> GetUsers() => _db.Users;

        [UseProjection]
        public IQueryable<User> GetUserById(int id) =>
            _db.Users.Where(u => u.Id == id);

        // REGISTER
        public async Task<User> RegisterUser(string username, string email, string password)
        {
            var hash = PasswordHelper.HashPassword(password);

            var user = new User
            {
                UserName = username,
                Email = email,
                PasswordHash = hash
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return user;
        }

        // LOGIN
        public async Task<string> LoginUser(string email, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) throw new GraphQLException("User not found");

            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
                throw new GraphQLException("Invalid password");

            // Issue JWT here (we’ll wire this up in Step 5)
            return JwtHelper.GenerateToken(user);
        }

        [Authorize]
        [UseProjection]
        public IQueryable<User> GetProfile(ClaimsPrincipal claims)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return _db.Users.Where(u => u.Id == userId);
        }
    }
}
