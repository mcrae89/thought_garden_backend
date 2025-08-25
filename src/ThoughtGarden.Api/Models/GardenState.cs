namespace ThoughtGarden.Models
{
    public class GardenState
    {
        public int Id { get; set; }  // Primary Key

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;  // When this snapshot was created

        // Navigation
        public ICollection<GardenPlant> Plants { get; set; } = new List<GardenPlant>();
    }
}
