using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GardenPlantQueries
    {
        [UseProjection]
        public IQueryable<GardenPlant> GetStoredPlants(int userId, [Service] ThoughtGardenDbContext db) =>
            db.GardenPlants
               .Where(p => p.GardenState.UserId == userId && p.IsStored);

        [UseProjection]
        public IQueryable<GardenPlant> GetActivePlants(int gardenStateId, [Service] ThoughtGardenDbContext db) =>
            db.GardenPlants
               .Where(p => p.GardenStateId == gardenStateId && !p.IsStored)
               .OrderBy(p => p.Order);

    }
}
