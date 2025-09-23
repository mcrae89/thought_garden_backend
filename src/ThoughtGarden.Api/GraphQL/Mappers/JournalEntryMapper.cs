using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Types;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mappers
{
    public static class JournalEntryMapper
    {
        public static JournalEntryType ToGraphType(this JournalEntry entry, EncryptionHelper encryption)
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
    }
}
