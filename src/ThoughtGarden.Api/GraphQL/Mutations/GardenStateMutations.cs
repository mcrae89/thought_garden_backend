using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using System.Security.Claims;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GardenStateMutations
    {
        [Authorize]
        public async Task<GardenState> CreateGardenState(ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var state = new GardenState { UserId = userId, SnapshotAt = DateTime.UtcNow };
            db.GardenStates.Add(state);
            await db.SaveChangesAsync();
            return state;
        }
    }
}
