using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GardenPlantQueries
    {
        private readonly ThoughtGardenDbContext _db;

        public GardenPlantQueries(ThoughtGardenDbContext db) => _db = db;

        [UseProjection]
        public IQueryable<GardenPlant> GetStoredPlants(int userId) =>
            _db.GardenPlants
               .Where(p => p.GardenState.UserId == userId && p.IsStored);

        [UseProjection]
        public IQueryable<GardenPlant> GetActivePlants(int gardenStateId) =>
            _db.GardenPlants
               .Where(p => p.GardenStateId == gardenStateId && !p.IsStored)
               .OrderBy(p => p.Order);

    }
}
