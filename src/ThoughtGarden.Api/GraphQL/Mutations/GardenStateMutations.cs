using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GardenStateMutations
    {
        public async Task<GardenState> CreateGardenState(
            int userId, [Service] ThoughtGardenDbContext db)
        {
            var state = new GardenState { UserId = userId, SnapshotAt = DateTime.UtcNow };
            db.GardenStates.Add(state);
            await db.SaveChangesAsync();
            return state;
        }
    }
}