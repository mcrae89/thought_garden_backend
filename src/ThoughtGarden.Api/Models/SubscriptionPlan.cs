namespace ThoughtGarden.Models
{
    public class SubscriptionPlan
    {
        public int Id { get; set; }  // Primary Key
        public string Name { get; set; } = null!;   // e.g., Free, Pro
        public int MaxJournalEntriesPerDay { get; set; }
        public int MaxGardenCustomizationsPerDay { get; set; }
        public decimal Price { get; set; }
        public string BillingPeriod { get; set; } = "Monthly";

        // Navigation
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
