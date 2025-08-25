namespace ThoughtGarden.Models
{
    public class Plant
    {
        public int Id { get; set; }  // Primary Key

        public int GardenStateId { get; set; }
        public GardenState GardenState { get; set; } = null!;

        public int EmotionTagId { get; set; }   // FK → EmotionTag
        public EmotionTag EmotionTag { get; set; } = null!;

        public string? Name { get; set; }

        public GrowthStage GrowthStage { get; set; } = GrowthStage.Seed;
        public double GrowthProgress { get; set; } = 0.0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum GrowthStage
    {
        Seed,
        Sprout,
        Bloom,
        Mature,
        Withered
    }
}
