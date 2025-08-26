using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class PlantQueries
    {
        private readonly ThoughtGardenDbContext _db;

        public PlantQueries(ThoughtGardenDbContext db) => _db = db;

        public async Task<List<GardenPlant>> GetStoredPlants(int userId) =>
            await _db.GardenPlants
                .Where(p => p.GardenState.UserId == userId && p.IsStored)
                .Include(p => p.PlantType)
                .ToListAsync();

        public async Task<List<GardenPlant>> GetActivePlants(int gardenStateId) =>
            await _db.GardenPlants
                .Where(p => p.GardenStateId == gardenStateId && !p.IsStored)
                .OrderBy(p => p.Order)
                .Include(p => p.PlantType)
                .ToListAsync();
    }
}
