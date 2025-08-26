using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GardenStateMutations
    {
        private readonly ThoughtGardenDbContext _db;

        public GardenStateMutations(ThoughtGardenDbContext db)
        {
            _db = db;
        }

        public async Task<GardenState> CreateGardenState(
            int userId)
        {
            var state = new GardenState { UserId = userId, SnapshotAt = DateTime.UtcNow };
            _db.GardenStates.Add(state);
            await _db.SaveChangesAsync();
            return state;
        }
    }
}