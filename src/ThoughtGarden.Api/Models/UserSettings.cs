namespace ThoughtGarden.Models
{
    public class UserSettings
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Theme { get; set; } = "light";
        public string EncryptionLevel { get; set; } = "standard";
        public bool Reminders { get; set; } = false;
    }
}
