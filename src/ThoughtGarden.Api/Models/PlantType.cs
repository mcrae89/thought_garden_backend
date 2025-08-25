using ThoughtGarden.Models;

public class PlantType
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int EmotionTagId { get; set; }

    // Navigation
    public EmotionTag EmotionTag { get; set; } = null!;
    public ICollection<GardenPlant> GardenPlants { get; set; } = new List<GardenPlant>();
}
