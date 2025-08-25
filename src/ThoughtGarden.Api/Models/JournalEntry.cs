using System;
using System.Collections.Generic;

namespace ThoughtGarden.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }  // Primary Key
        public string Text { get; set; } = null!;
        public string? Mood { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        // Foreign key to User
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Many-to-many: JournalEntry <-> EmotionTag
        public ICollection<EntryEmotion> Emotions { get; set; } = new List<EntryEmotion>();
    }
}
