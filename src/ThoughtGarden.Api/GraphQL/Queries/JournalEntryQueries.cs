using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Types;

[ExtendObjectType("Query")]
public class JournalEntryQueries
{
    [Authorize]
    public IEnumerable<JournalEntryType> GetJournalEntries(
        ClaimsPrincipal claims,
        [Service] ThoughtGardenDbContext db,
        [Service] EnvelopeCrypto crypto)
    {
        var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var entries = db.JournalEntries
            .AsNoTracking()
            .Include(e => e.Mood)
            .Include(e => e.SecondaryEmotions).ThenInclude(se => se.Emotion)
            .Where(j => !j.IsDeleted && j.UserId == callerId)
            .ToList();

        return entries.Select(e =>
        {
            try
            {
                return e.ToGraphType(crypto);
            }
            catch (DecryptionFailedException)
            {
                throw new GraphQLException("Unable to decrypt journal entry.");
            }
        });
    }

    [Authorize]
    public JournalEntryType GetJournalEntryById(
        int id,
        ClaimsPrincipal claims,
        [Service] ThoughtGardenDbContext db,
        [Service] EnvelopeCrypto crypto)
    {
        var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var entry = db.JournalEntries
            .AsNoTracking()
            .Include(e => e.Mood)
            .Include(e => e.SecondaryEmotions).ThenInclude(se => se.Emotion)
            .FirstOrDefault(j => j.Id == id && !j.IsDeleted);

        if (entry == null) throw new GraphQLException("Entry not found");
        if (entry.UserId != callerId) throw new GraphQLException("Not authorized");

        try
        {
            return entry.ToGraphType(crypto);
        }
        catch (DecryptionFailedException)
        {
            throw new GraphQLException("Unable to decrypt journal entry.");
        }
    }
}
