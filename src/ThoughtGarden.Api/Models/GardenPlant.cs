using System;

namespace ThoughtGarden.Models
{
    public class GardenPlant
    {
        public int Id { get; set; }
        public int GardenStateId { get; set; }
        public int PlantTypeId { get; set; }

        // Per-user growth
        public double GrowthProgress { get; set; }
        public GrowthStage Stage { get; set; }  // Enum stays inside GardenPlant
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public GardenState GardenState { get; set; } = null!;
        public PlantType PlantType { get; set; } = null!;

        // Nested enum so it's self-contained
        public enum GrowthStage
        {
            Seed = 0,
            Sprout = 1,
            Bloom = 2,
            Mature = 3,
            Withered = 4
        }
    }
}
