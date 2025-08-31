using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using System.Security.Claims;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class JournalEntryQueries
    {
        [Authorize]
        [UseProjection]
        public IQueryable<JournalEntry> GetJournalEntries(ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == UserRole.Admin.ToString();

            return isAdmin
                ? db.JournalEntries.Where(j => !j.IsDeleted)
                : db.JournalEntries.Where(j => j.UserId == callerId && !j.IsDeleted);
        }

        [Authorize]
        [UseProjection]
        public IQueryable<JournalEntry> GetJournalEntryById(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == UserRole.Admin.ToString();

            return db.JournalEntries.Where(j => j.Id == id && (isAdmin || j.UserId == callerId));
        }
    }
}
