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
        public async Task<AuthPayload> LoginUser(string email, string password, [Service] ThoughtGardenDbContext db, [Service] IConfiguration config, [Service] JwtHelper jwtHelper)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) throw new GraphQLException("User not found");
            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash)) throw new GraphQLException("Invalid password");

            var now = DateTime.UtcNow;
            const int MaxActiveRefreshTokens = 4; // keep the newest 4

            await using var tx = await db.Database.BeginTransactionAsync();

            await db.RefreshTokens
                .Where(r => r.UserId == user.Id && r.ExpiresAt <= now)
                .ExecuteDeleteAsync();

            // Prune extras (keep newest 4 active; revoke the rest) — single SQL UPDATE
            var extras = db.RefreshTokens
                .Where(r => r.UserId == user.Id && r.RevokedAt == null && r.ExpiresAt > now)
                .OrderByDescending(r => r.CreatedAt)
                .Skip(MaxActiveRefreshTokens);

            await extras.ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, now));

            // Issue tokens
            var access = jwtHelper.GenerateAccessToken(user);

            var (rt, expiresUtc, hashHex) = JwtHelper.GenerateRefreshToken(config);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hashHex,
                ExpiresAt = expiresUtc,
                CreatedAt = now
            });

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return new AuthPayload { AccessToken = access, RefreshToken = rt };
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

        public async Task<AuthPayload> RefreshToken(string refreshToken, [Service] ThoughtGardenDbContext db, [Service] IConfiguration config, [Service] JwtHelper jwtHelper)
        {
            var incomingHash = JwtHelper.Sha256Hex(refreshToken);
            var stored = await db.RefreshTokens
                .Include(r => r.User)
                .Where(r => r.ExpiresAt > DateTime.UtcNow && r.RevokedAt == null)
                .FirstOrDefaultAsync(r => r.TokenHash == incomingHash);
            if (stored == null || !stored.IsActive) throw new GraphQLException("Invalid or expired refresh token");

            stored.RevokedAt = DateTime.UtcNow;
            var (newRt, newExpiresUtc, newHashHex) = JwtHelper.GenerateRefreshToken(config);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = stored.UserId,
                TokenHash = newHashHex,
                ExpiresAt = newExpiresUtc,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            var access = jwtHelper.GenerateAccessToken(stored.User);
            return new AuthPayload { AccessToken = access, RefreshToken = newRt };
        }


        // Logout
        public async Task<bool> LogoutUser(string refreshToken, [Service] ThoughtGardenDbContext db)
        {
            var incomingHash = JwtHelper.Sha256Hex(refreshToken);
            var stored = await db.RefreshTokens
                .Where(r => r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync(r => r.TokenHash == incomingHash);

            if (stored != null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return true;
        }
    }
}