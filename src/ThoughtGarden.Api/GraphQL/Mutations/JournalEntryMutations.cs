using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using ThoughtGarden.Models.Inputs;
using System.Security.Claims;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class JournalEntryMutations
    {
        [Authorize]
        public async Task<JournalEntry> AddJournalEntry(string text, int moodId, List<SecondaryEmotionInput>? secondaryEmotions, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var entry = new JournalEntry { UserId = userId, Text = text, MoodId = moodId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, IsDeleted = false };
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();

            if (secondaryEmotions != null && secondaryEmotions.Count > 0)
            {
                foreach (var se in secondaryEmotions)
                    db.EntryEmotions.Add(new EntryEmotion { EntryId = entry.Id, EmotionId = se.EmotionId, Intensity = se.Intensity });

                await db.SaveChangesAsync();
            }

            return entry;
        }

        [Authorize]
        public async Task<JournalEntry?> UpdateJournalEntry(int id, string? text, int? moodId, List<SecondaryEmotionInput>? secondaryEmotions, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var entry = await db.JournalEntries.Include(e => e.SecondaryEmotions).FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId && !e.IsDeleted);
            if (entry == null) throw new GraphQLException("Not authorized or entry not found");

            if (!string.IsNullOrWhiteSpace(text)) entry.Text = text;
            if (moodId.HasValue) entry.MoodId = moodId.Value;

            if (secondaryEmotions != null)
            {
                db.EntryEmotions.RemoveRange(entry.SecondaryEmotions);
                foreach (var se in secondaryEmotions)
                    db.EntryEmotions.Add(new EntryEmotion { EntryId = entry.Id, EmotionId = se.EmotionId, Intensity = se.Intensity });
            }

            entry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return entry;
        }

        [Authorize]
        public async Task<bool> DeleteJournalEntry(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var entry = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (entry == null) throw new GraphQLException("Not authorized or entry not found");

            entry.IsDeleted = true;
            entry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return true;
        }
    }
}
