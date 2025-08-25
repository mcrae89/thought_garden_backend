namespace ThoughtGarden.Models
{
    public class EntryEmotion
    {
        // Composite key: EntryId + EmotionId
        public int EntryId { get; set; }
        public JournalEntry Entry { get; set; } = null!;

        public int EmotionId { get; set; }
        public EmotionTag Emotion { get; set; } = null!;

        public int? Intensity { get; set; }  // Optional 1–10 scale
    }
}
