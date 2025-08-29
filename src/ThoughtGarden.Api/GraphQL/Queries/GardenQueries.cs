using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GardenQueries
    {
        [UseProjection]
        public IQueryable<GardenState> GetGardens(int userId, [Service] ThoughtGardenDbContext db) =>
            db.GardenStates.Where(gs => gs.UserId == userId);

        [UseProjection]
        public IQueryable<GardenState> GetGardenById(int id, [Service] ThoughtGardenDbContext db) =>
            db.GardenStates.Where(gs => gs.Id == id);
    }
}
