using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Types;
using ThoughtGarden.Models;
using ThoughtGarden.Models.Inputs;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class JournalEntryMutations
    {
        [Authorize]
        public async Task<JournalEntryType> AddJournalEntry(string text, int moodId, List<SecondaryEmotionInput>? secondaryEmotions, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db, [Service] EnvelopeCrypto crypto)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var enc = crypto.Encrypt(text);

            var entry = new JournalEntry
            {
                UserId = userId,
                MoodId = moodId,

                // encryption fields (preserve your property names)
                Text = enc.cipher,
                IV = enc.nonce,           // mirror nonce for compatibility
                DataNonce = enc.nonce,
                DataTag = enc.tag,
                WrappedKeys = enc.wrappedKeysJson,
                AlgVersion = enc.algVersion,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();

            if (secondaryEmotions != null && secondaryEmotions.Count > 0)
            {
                foreach (var se in secondaryEmotions)
                {
                    db.EntryEmotions.Add(new EntryEmotion { EntryId = entry.Id, EmotionId = se.EmotionId, Intensity = se.Intensity });
                }
                await db.SaveChangesAsync();
            }
            await db.Entry(entry).Reference(e => e.Mood).LoadAsync();
            await db.Entry(entry).Collection(e => e.SecondaryEmotions).Query().Include(se => se.Emotion).LoadAsync();

            return entry.ToGraphType(crypto);
        }

        [Authorize]
        public async Task<JournalEntryType> UpdateJournalEntry(int id, string? text, int? moodId, List<SecondaryEmotionInput>? secondaryEmotions, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db, [Service] EnvelopeCrypto crypto)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == UserRole.Admin.ToString();

            var entry = await db.JournalEntries
                .Include(e => e.SecondaryEmotions)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

            if (entry == null) throw new GraphQLException("Entry not found");
            if (!isAdmin && entry.UserId != userId) throw new GraphQLException("Not authorized");

            if (!string.IsNullOrWhiteSpace(text))
            {
                var enc = crypto.Encrypt(text);
                entry.Text = enc.cipher;        // base64 ciphertext
                entry.IV = enc.nonce;         // keep mirroring for compatibility
                entry.DataNonce = enc.nonce;         // base64(12)
                entry.DataTag = enc.tag;           // base64(16)
                entry.WrappedKeys = enc.wrappedKeysJson;
                entry.AlgVersion = enc.algVersion;    // "gcm.v1"
            }

            if (moodId.HasValue) entry.MoodId = moodId.Value;

            if (secondaryEmotions != null)
            {
                db.EntryEmotions.RemoveRange(entry.SecondaryEmotions);
                foreach (var se in secondaryEmotions)
                {
                    db.EntryEmotions.Add(new EntryEmotion { EntryId = entry.Id, EmotionId = se.EmotionId, Intensity = se.Intensity });
                }
            }

            entry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await db.Entry(entry).Reference(e => e.Mood).LoadAsync();
            await db.Entry(entry).Collection(e => e.SecondaryEmotions).Query().Include(se => se.Emotion).LoadAsync();

            return entry.ToGraphType(crypto);
        }

        [Authorize]
        public async Task<bool> DeleteJournalEntry(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isAdmin = role == UserRole.Admin.ToString();

            var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
            if (entry == null) throw new GraphQLException("Entry not found");
            if (!isAdmin && entry.UserId != userId) throw new GraphQLException("Not authorized");

            entry.IsDeleted = true;
            entry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return true;
        }
    }
}