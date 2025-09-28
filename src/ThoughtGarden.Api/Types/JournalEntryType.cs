using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Types
{
    public class JournalEntryType
    {
        public int Id { get; set; }
        public string Text { get; set; } = null!;   // decrypted text only
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public int? MoodId { get; set; }
        public EmotionTag? Mood { get; set; }
        public ICollection<EntryEmotion> SecondaryEmotions { get; set; } = new List<EntryEmotion>();
    }
}
