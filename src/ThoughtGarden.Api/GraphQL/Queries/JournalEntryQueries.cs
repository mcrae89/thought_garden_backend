using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Mappers;
using ThoughtGarden.Api.GraphQL.Types;
using ThoughtGarden.Models;

[ExtendObjectType("Query")]
public class JournalEntryQueries
{
    [Authorize]
    public IEnumerable<JournalEntryType> GetJournalEntries(ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db, [Service] EncryptionHelper encryption)
    {
        var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = claims.FindFirstValue(ClaimTypes.Role);
        var isAdmin = role == UserRole.Admin.ToString();

        var query = db.JournalEntries
            .Include(e => e.Mood)
            .Include(e => e.SecondaryEmotions)
                .ThenInclude(se => se.Emotion)
            .Where(j => !j.IsDeleted);

        var entries = isAdmin ? query.ToList() : query.Where(j => j.UserId == callerId).ToList();

        return entries.Select(e =>
        {
            try
            {
                return e.ToGraphType(encryption);
            }
            catch (DecryptionFailedException)
            {
                throw new GraphQLException("Unable to decrypt journal entry.");
            }
        });
    }

    [Authorize]
    public JournalEntryType GetJournalEntryById(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db, [Service] EncryptionHelper encryption)
    {
        var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = claims.FindFirstValue(ClaimTypes.Role);
        var isAdmin = role == UserRole.Admin.ToString();

        var entry = db.JournalEntries
            .Include(e => e.Mood)
            .Include(e => e.SecondaryEmotions)
                .ThenInclude(se => se.Emotion)
            .FirstOrDefault(j => j.Id == id && !j.IsDeleted);

        if (entry == null) throw new GraphQLException("Entry not found");
        if (!isAdmin && entry.UserId != callerId) throw new GraphQLException("Not authorized");

        try
        {
            return entry.ToGraphType(encryption);
        }
        catch (DecryptionFailedException)
        {
            throw new GraphQLException("Unable to decrypt journal entry.");
        }
    }
}