using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Payloads;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class UserMutations
    {
        // Update user email/username
        [Authorize]
        public async Task<User?> UpdateUser(int id, string? userName, string? email, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);

            // only allow self-update unless Admin
            if (userId != id && role != UserRole.Admin.ToString())
                throw new GraphQLException("Not authorized to update this user");

            var user = await db.Users.FindAsync(id);
            if (user == null) throw new GraphQLException("User not found");

            if (!string.IsNullOrWhiteSpace(userName)) user.UserName = userName;
            if (!string.IsNullOrWhiteSpace(email)) user.Email = email;

            await db.SaveChangesAsync();
            return user;
        }

        // Delete user
        [Authorize]
        public async Task<bool> DeleteUser(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);

            // only allow self-delete unless Admin
            if (userId != id && role != UserRole.Admin.ToString())
                throw new GraphQLException("Not authorized to delete this user");

            var user = await db.Users.FindAsync(id);
            if (user == null) throw new GraphQLException("User not found");

            db.Users.Remove(user);  // hard delete for now (MVP)
            await db.SaveChangesAsync();
            return true;
        }

        // Update Password
        [Authorize]
        public async Task<bool> UpdatePassword(string currentPassword,string newPassword,ClaimsPrincipal claims,[Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new GraphQLException("User not found");

            // verify current password
            if (!PasswordHelper.VerifyPassword(currentPassword, user.PasswordHash))
                throw new GraphQLException("Current password is incorrect");

            // hash and update
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            await db.SaveChangesAsync();

            return true;
        }

        // Login
        public async Task<AuthPayload> LoginUser(string email,string password,[Service] ThoughtGardenDbContext db,[Service] JwtHelper jwtHelper)
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

        [Authorize]
        public async Task<AuthPayload> RefreshToken(string refreshToken, [Service] ThoughtGardenDbContext db, [Service] JwtHelper jwtHelper)
        {
            var stored = await db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == refreshToken);
            if (stored == null || !stored.IsActive) throw new GraphQLException("Invalid or expired refresh token");

            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var access = jwtHelper.GenerateAccessToken(stored.User);
            var refresh = await jwtHelper.GenerateRefreshToken(stored.User);

            return new AuthPayload { AccessToken = access, RefreshToken = refresh };
        }


        // Logout
        public async Task<bool> LogoutUser(string refreshToken,[Service] ThoughtGardenDbContext db)
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