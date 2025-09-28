using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using System.Security.Claims;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GardenQueries
    {
        [Authorize]
        [UseProjection]
        public IQueryable<GardenState> GetGardens(int userId, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            if (callerId != userId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not Authorized");
            return db.GardenStates.Where(gs => gs.UserId == userId);
        }

        [Authorize]
        public GardenState? GetGardenById(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == UserRole.Admin.ToString();

            var garden = db.GardenStates.FirstOrDefault(g => g.Id == id);
            if (garden == null) return null;

            if (!isAdmin && garden.UserId != callerId)
                throw new GraphQLException("Not Authorized");

            return garden;
        }

    }
}
