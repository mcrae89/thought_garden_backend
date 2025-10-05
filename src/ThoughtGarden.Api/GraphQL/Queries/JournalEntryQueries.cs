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
    public IEnumerable<JournalEntryType> GetJournalEntries(
        ClaimsPrincipal claims,
        [Service] ThoughtGardenDbContext db,
        [Service] EnvelopeCrypto crypto)
    {
        var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = claims.FindFirstValue(ClaimTypes.Role);
        var isAdmin = string.Equals(role, UserRole.Admin.ToString(), StringComparison.Ordinal);

        var query = db.JournalEntries
            .AsNoTracking()
            .Include(e => e.Mood)
            .Include(e => e.SecondaryEmotions).ThenInclude(se => se.Emotion)
            .Where(j => !j.IsDeleted);

        var entries = isAdmin
            ? query.ToList()                       // admin sees all, but no decryption
            : query.Where(j => j.UserId == callerId).ToList();

        foreach (var e in entries)
        {
            if (!isAdmin && e.UserId == callerId)
            {
                // Owner path: decrypt
                string plain;
                try { plain = crypto.Decrypt(e.Text, e.DataNonce!, e.DataTag!, e.WrappedKeys!); }
                catch (DecryptionFailedException) { throw new GraphQLException("Unable to decrypt journal entry."); }

                yield return new JournalEntryType
                {
                    Id = e.Id,
                    Text = plain,
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt,
                    IsDeleted = e.IsDeleted,
                    MoodId = e.MoodId,
                    Mood = e.Mood,
                    SecondaryEmotions = e.SecondaryEmotions
                };
            }
            else
            {
                // Admin (or any non-owner fallback) path: do NOT decrypt text
                yield return new JournalEntryType
                {
                    Id = e.Id,
                    Text = "[encrypted]", // placeholder; do not expose ciphertext nor plaintext
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt,
                    IsDeleted = e.IsDeleted,
                    MoodId = e.MoodId,
                    Mood = e.Mood,
                    SecondaryEmotions = e.SecondaryEmotions
                };
            }
        }
    }

    [Authorize]
    public JournalEntryType GetJournalEntryById(
        int id,
        ClaimsPrincipal claims,
        [Service] ThoughtGardenDbContext db,
        [Service] EnvelopeCrypto crypto)
    {
        var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = claims.FindFirstValue(ClaimTypes.Role);
        var isAdmin = string.Equals(role, UserRole.Admin.ToString(), StringComparison.Ordinal);

        var entry = db.JournalEntries
            .AsNoTracking()
            .Include(e => e.Mood)
            .Include(e => e.SecondaryEmotions).ThenInclude(se => se.Emotion)
            .FirstOrDefault(j => j.Id == id && !j.IsDeleted);

        if (entry == null) throw new GraphQLException("Entry not found");

        // Owner gets decrypted; Admin can view metadata for any entry without decrypting
        if (entry.UserId != callerId && !isAdmin) throw new GraphQLException("not authorized");

        if (isAdmin && entry.UserId != callerId)
        {
            // Admin non-owner view: do NOT decrypt
            return new JournalEntryType
            {
                Id = entry.Id,
                Text = "[encrypted]",
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
                IsDeleted = entry.IsDeleted,
                MoodId = entry.MoodId,
                Mood = entry.Mood,
                SecondaryEmotions = entry.SecondaryEmotions
            };
        }

        // Owner (or admin viewing own entry) — decrypt
        try
        {
            var plain = crypto.Decrypt(entry.Text, entry.DataNonce!, entry.DataTag!, entry.WrappedKeys!);
            return new JournalEntryType
            {
                Id = entry.Id,
                Text = plain,
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
