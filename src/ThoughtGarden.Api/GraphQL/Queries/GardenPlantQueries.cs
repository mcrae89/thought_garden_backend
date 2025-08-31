using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using System.Security.Claims;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GardenPlantQueries
    {
        [Authorize]
        [UseProjection]
        public IQueryable<GardenPlant> GetStoredPlants(int userId, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            if (callerId != userId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");
            return db.GardenPlants.Where(p => p.GardenState.UserId == userId && p.IsStored);
        }

        [Authorize]
        [UseProjection]
        public IQueryable<GardenPlant> GetActivePlants(int gardenStateId, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == UserRole.Admin.ToString();
            return db.GardenPlants.Where(p => p.GardenStateId == gardenStateId && !p.IsStored && (isAdmin || p.GardenState.UserId == callerId)).OrderBy(p => p.Order);
        }
    }
}
