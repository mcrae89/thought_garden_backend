using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class UserMutations
    {
        private readonly ThoughtGardenDbContext _db;

        public UserMutations(ThoughtGardenDbContext db)
        {
            _db = db;
        }

        // ✅ Create a new user
        public async Task<User> AddUser(string userName,string email,string passwordHash,UserRole role,int subscriptionPlanId)
        {
            var user = new User
            {
                UserName = userName,
                Email = email,
                PasswordHash = passwordHash,
                Role = role,
                SubscriptionPlanId = subscriptionPlanId
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        // ✅ Update user email/username
        public async Task<User?> UpdateUser(int id,string? userName,string? email)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return null;

            if (!string.IsNullOrWhiteSpace(userName)) user.UserName = userName;
            if (!string.IsNullOrWhiteSpace(email)) user.Email = email;

            await _db.SaveChangesAsync();
            return user;
        }

        // ✅ Soft delete user
        public async Task<bool> DeleteUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return false;

            _db.Users.Remove(user);  // hard delete for now (MVP)
            await _db.SaveChangesAsync();
            return true;
        }
    }
}