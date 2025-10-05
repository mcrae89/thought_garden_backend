using System.Collections.Generic;

namespace ThoughtGarden.Models
{
    public class User
    {
        public int Id { get; set; }

        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;

        // Role for authorization
        public UserRole Role { get; set; } = UserRole.User;

        public int SubscriptionPlanId { get; set; }
        public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

        // Navigation
        public ICollection<GardenState> GardenStates { get; set; } = new List<GardenState>();
        public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();


        public UserSettings? Settings { get; set; }
    }

    public enum UserRole
    {
        User,
        Admin,
        Moderator
    }
}
