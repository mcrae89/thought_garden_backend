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

        [UseProjection]
        public IQueryable<GardenState> GetGardens(int userId) =>
            _db.GardenStates.Where(gs => gs.UserId == userId);

        [UseProjection]
        public IQueryable<GardenState> GetGardenById(int id) =>
            _db.GardenStates.Where(gs => gs.Id == id);
    }
}
