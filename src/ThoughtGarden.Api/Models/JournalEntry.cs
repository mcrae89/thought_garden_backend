namespace ThoughtGarden.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }  // Primary Key
        public string Text { get; set; } = null!;

        public string? DataNonce { get; set; }     // base64(12)
        public string? DataTag { get; set; }       // base64(16)
        public string? WrappedKeys { get; set; }   // JSON map: keyId -> base64(nonce||tag||wrappedDEK)
        public string? AlgVersion { get; set; }    // e.g., "gcm.v1"


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        // Foreign key to User
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Primary mood
        public int? MoodId { get; set; }
        public EmotionTag? Mood { get; set; }

        // Secondary emotions (with intensities)
        public ICollection<EntryEmotion> SecondaryEmotions { get; set; } = new List<EntryEmotion>();
    }
}

