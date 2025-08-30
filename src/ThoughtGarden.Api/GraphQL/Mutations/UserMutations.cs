using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Payloads;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class UserMutations
    {
        // Update user email/username
        public async Task<User?> UpdateUser(int id,string? userName,string? email, [Service] ThoughtGardenDbContext db)
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return null;

            if (!string.IsNullOrWhiteSpace(userName)) user.UserName = userName;
            if (!string.IsNullOrWhiteSpace(email)) user.Email = email;

            await db.SaveChangesAsync();
            return user;
        }

        // Soft delete user
        public async Task<bool> DeleteUser(int id, [Service] ThoughtGardenDbContext db)
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return false;

            db.Users.Remove(user);  // hard delete for now (MVP)
            await db.SaveChangesAsync();
            return true;
        }

        // Login
        public async Task<AuthPayload> LoginUser(
        string email,
        string password,
        ThoughtGardenDbContext db,
        [Service] JwtHelper jwtHelper)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) throw new GraphQLException("User not found");

            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
                throw new GraphQLException("Invalid password");

            return new AuthPayload
            {
                AccessToken = jwtHelper.GenerateAccessToken(user),
                RefreshToken = await jwtHelper.GenerateRefreshToken(user)
            };
        }

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

        public async Task<AuthPayload> RefreshToken(string refreshToken, ThoughtGardenDbContext db, [Service] JwtHelper jwtHelper)
        {
            var stored = await db.RefreshTokens.Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (stored == null || !stored.IsActive)
                throw new GraphQLException("Invalid or expired refresh token");

            // revoke old
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var newAccessToken = jwtHelper.GenerateAccessToken(stored.User);
            var newRefreshToken = await jwtHelper.GenerateRefreshToken(stored.User);

            return new AuthPayload
            {
                AccessToken = jwtHelper.GenerateAccessToken(stored.User),
                RefreshToken = await jwtHelper.GenerateRefreshToken(stored.User)
            };
        }

        // Logout
        public async Task<bool> LogoutUser(string refreshToken, ThoughtGardenDbContext db)
        {
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
            if (stored != null && stored.IsActive)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return true;
        }
    }
}