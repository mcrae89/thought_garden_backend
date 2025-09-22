using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThoughtGarden.Api.Data;
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
                return new JournalEntryType
                {
                    Id = e.Id,
                    Text = encryption.Decrypt(e.Text, e.IV),
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt,
                    IsDeleted = e.IsDeleted,
                    MoodId = e.MoodId,
                    Mood = e.Mood,
                    SecondaryEmotions = e.SecondaryEmotions
                };
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
            return new JournalEntryType
            {
                Id = entry.Id,
                Text = encryption.Decrypt(entry.Text, entry.IV),
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
                IsDeleted = entry.IsDeleted,
                MoodId = entry.MoodId,
                Mood = entry.Mood,
                SecondaryEmotions = entry.SecondaryEmotions
            };
        }
        catch (DecryptionFailedException)
        {
            throw new GraphQLException("Unable to decrypt journal entry.");
        }
    }
}