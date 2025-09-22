namespace ThoughtGarden.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }  // Primary Key
        public string Text { get; set; } = null!;

        // IV used for encryption/decryption (Base64-encoded 16-byte string)
        public string IV { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        // Foreign key to User
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // ✅ Primary mood
        public int? MoodId { get; set; }
        public EmotionTag? Mood { get; set; }

        // ✅ Secondary emotions (with intensities)
        public ICollection<EntryEmotion> SecondaryEmotions { get; set; } = new List<EntryEmotion>();
    }
}

