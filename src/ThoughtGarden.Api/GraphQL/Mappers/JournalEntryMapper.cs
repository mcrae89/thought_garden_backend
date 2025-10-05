using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Types;
using ThoughtGarden.Models;

public static class JournalEntryMapper
{
    public static JournalEntryType ToGraphType(this JournalEntry e, EnvelopeCrypto env)
    {
        // Use envelope fields; IV is still populated, but we don’t rely on it for GCM.
        var plain = env.Decrypt(e.Text, e.DataNonce!, e.DataTag!, e.WrappedKeys!);

        return new JournalEntryType
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
}
