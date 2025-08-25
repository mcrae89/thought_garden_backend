using System.Collections.Generic;

namespace ThoughtGarden.Models
{
    public class EmotionTag
    {
        public int Id { get; set; }  // Primary Key
        public string Name { get; set; } = null!;
        public string Color { get; set; } = null!;
        public string? Icon { get; set; }

        // ✅ Many-to-many via EntryEmotion (secondary emotions)
        public ICollection<EntryEmotion> EntryLinks { get; set; } = new List<EntryEmotion>();
    }
}
