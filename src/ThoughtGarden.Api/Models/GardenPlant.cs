using System;

namespace ThoughtGarden.Models
{
    public class GardenPlant
    {
        public int Id { get; set; }
        public int GardenStateId { get; set; }
        public int PlantTypeId { get; set; }

        // Per-user growth
        public double GrowthProgress { get; set; } = 0.0;
        public GrowthStage Stage { get; set; } = GrowthStage.Seed;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Placement
        public int? Order { get; set; }  // null = not assigned to garden layout
        public bool IsStored { get; set; } = true; // default new plants go into storage


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
