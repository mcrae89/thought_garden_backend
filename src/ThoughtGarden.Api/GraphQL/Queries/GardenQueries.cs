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
            if (callerId != userId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");
            return db.GardenStates.Where(gs => gs.UserId == userId);
        }

        [Authorize]
        [UseProjection]
        public IQueryable<GardenState> GetGardenById(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == UserRole.Admin.ToString();
            return db.GardenStates.Where(gs => gs.Id == id && (isAdmin || gs.UserId == callerId));
        }
    }
}
