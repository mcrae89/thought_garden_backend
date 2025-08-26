using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using Microsoft.EntityFrameworkCore;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class UserQueries
    {
        private readonly ThoughtGardenDbContext _db;

        public UserQueries(ThoughtGardenDbContext db) => _db = db;

        public async Task<List<User>> GetUsers() =>
            await _db.Users.ToListAsync();

        public async Task<User?> GetUserById(int id) =>
            await _db.Users
                .Include(u => u.GardenStates)
                .Include(u => u.JournalEntries)
                .FirstOrDefaultAsync(u => u.Id == id);
    }
}
