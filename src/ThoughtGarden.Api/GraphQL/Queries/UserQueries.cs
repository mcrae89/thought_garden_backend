using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class UserQueries
    {
        [UseProjection]
        public IQueryable<User> GetUsers([Service] ThoughtGardenDbContext db) => db.Users;

        [UseProjection]
        public IQueryable<User> GetUserById(int id, [Service] ThoughtGardenDbContext db) =>
            db.Users.Where(u => u.Id == id);

        [Authorize]
        [UseProjection]
        public IQueryable<User> GetProfile(ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return db.Users.Where(u => u.Id == userId);
        }
    }
}
