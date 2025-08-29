using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using ThoughtGarden.Models.Inputs;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class JournalEntryMutations
    {
        // Add a new journal entry
        public async Task<JournalEntry> AddJournalEntry(string text, int userId, int moodId, List<SecondaryEmotionInput>? secondaryEmotions, [Service] ThoughtGardenDbContext db)
        {
            var entry = new JournalEntry
            {
                UserId = userId,
                Text = text,
                MoodId = moodId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();

            // ✅ Add secondary emotions
            if (secondaryEmotions != null && secondaryEmotions.Count > 0)
            {
                foreach (var se in secondaryEmotions)
                {
                    db.EntryEmotions.Add(new EntryEmotion
                    {
                        EntryId = entry.Id,
                        EmotionId = se.EmotionId,
                        Intensity = se.Intensity
                    });
                }
                await db.SaveChangesAsync();
            }

            return entry;
        }


        // Update an existing journal entry
        public async Task<JournalEntry?> UpdateJournalEntry(int id, string? text, int? moodId, List<SecondaryEmotionInput>? secondaryEmotions, [Service] ThoughtGardenDbContext db)
        {
            var entry = await db.JournalEntries
                .Include(e => e.SecondaryEmotions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null || entry.IsDeleted) return null;

            if (!string.IsNullOrWhiteSpace(text)) entry.Text = text;
            if (moodId.HasValue) entry.MoodId = moodId.Value;

            if (secondaryEmotions != null)
            {
                // Remove old ones
                db.EntryEmotions.RemoveRange(entry.SecondaryEmotions);

                // Add new ones
                foreach (var se in secondaryEmotions)
                {
                    db.EntryEmotions.Add(new EntryEmotion
                    {
                        EntryId = entry.Id,
                        EmotionId = se.EmotionId,
                        Intensity = se.Intensity
                    });
                }
            }

            entry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return entry;
        }

        // Soft delete a journal entry
        public async Task<bool> DeleteJournalEntry(int id, [Service] ThoughtGardenDbContext db)
        {
            var entry = await db.JournalEntries.FindAsync(id);
            if (entry == null) return false;

            entry.IsDeleted = true;
            entry.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return true;
        }
    }
}