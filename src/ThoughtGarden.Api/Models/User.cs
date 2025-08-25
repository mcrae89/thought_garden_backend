using System;
using System.Collections.Generic;

namespace ThoughtGarden.Models
{
    public class User
    {
        public int Id { get; set; }  // Primary key
        public string Email { get; set; } = null!;  // Required
        public string Password { get; set; } = null!; // Hashed, not plain text
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<JournalEntry> Entries { get; set; } = new List<JournalEntry>();
        public UserSettings? Settings { get; set; }
    }
}
