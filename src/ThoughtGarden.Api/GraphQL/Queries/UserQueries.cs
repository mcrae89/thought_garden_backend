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
        [UseProjection]
        public IQueryable<User> GetUsers([Service] ThoughtGardenDbContext db) => db.Users;

        [UseProjection]
        public IQueryable<User> GetUserById(int id, [Service] ThoughtGardenDbContext db) =>
            db.Users.Where(u => u.Id == id);

        // REGISTER
        public async Task<User> RegisterUser(string username, string email, string password, [Service] ThoughtGardenDbContext db)
        {
            var hash = PasswordHelper.HashPassword(password);

            var user = new User
            {
                UserName = username,
                Email = email,
                PasswordHash = hash,
                Role = UserRole.User,
                SubscriptionPlanId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return user;
        }

        // LOGIN
        public async Task<string> LoginUser(string email,string password,ThoughtGardenDbContext db,[Service] JwtHelper jwtHelper)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) throw new GraphQLException("User not found");

            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
                throw new GraphQLException("Invalid password");

            return jwtHelper.GenerateToken(user);
        }

        [Authorize]
        [UseProjection]
        public IQueryable<User> GetProfile(ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return db.Users.Where(u => u.Id == userId);
        }
    }
}
