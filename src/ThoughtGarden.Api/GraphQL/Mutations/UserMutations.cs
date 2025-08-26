using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

[ExtendObjectType("Mutation")]
public class UserMutations
{
    // ✅ Create a new user
    public async Task<User> AddUser(
        string userName,
        string email,
        string passwordHash,
        UserRole role,
        int subscriptionPlanId,
        [Service] ThoughtGardenDbContext db)
    {
        var user = new User
        {
            UserName = userName,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            SubscriptionPlanId = subscriptionPlanId
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // ✅ Update user email/username
    public async Task<User?> UpdateUser(
        int id,
        string? userName,
        string? email,
        [Service] ThoughtGardenDbContext db)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(userName)) user.UserName = userName;
        if (!string.IsNullOrWhiteSpace(email)) user.Email = email;

        await db.SaveChangesAsync();
        return user;
    }

    // ✅ Soft delete user
    public async Task<bool> DeleteUser(
        int id,
        [Service] ThoughtGardenDbContext db)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return false;

        db.Users.Remove(user);  // hard delete for now (MVP)
        await db.SaveChangesAsync();
        return true;
    }
}
