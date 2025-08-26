using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GardenQueries
    {
        private readonly ThoughtGardenDbContext _db;

        public GardenQueries(ThoughtGardenDbContext db) => _db = db;

        public async Task<List<GardenState>> GetGardens(int userId) =>
            await _db.GardenStates
                .Where(g => g.UserId == userId)
                .Include(g => g.Plants)
                    .ThenInclude(p => p.PlantType)
                        .ThenInclude(pt => pt.EmotionTag)
                .ToListAsync();

        public async Task<GardenState?> GetGardenById(int id) =>
            await _db.GardenStates
                .Include(g => g.Plants)
                    .ThenInclude(p => p.PlantType)
                        .ThenInclude(pt => pt.EmotionTag)
                .FirstOrDefaultAsync(g => g.Id == id);
    }
}
