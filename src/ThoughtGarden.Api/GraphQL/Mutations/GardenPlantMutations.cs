using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GardenPlantMutations
    {
        private readonly ThoughtGardenDbContext _db;

        public GardenPlantMutations(ThoughtGardenDbContext db)
        {
            _db = db;
        }

        public async Task<GardenPlant> AddGardenPlant(int gardenStateId,int plantTypeId,double growthProgress = 0.0,GardenPlant.GrowthStage? stage = null)
        {
            var plant = new GardenPlant
            {
                GardenStateId = gardenStateId,
                PlantTypeId = plantTypeId,
                GrowthProgress = growthProgress,
                Stage = stage ?? GardenPlant.GrowthStage.Seed, // fallback
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                // New plants default to storage
                Order = null,
                IsStored = true
            };

            _db.GardenPlants.Add(plant);
            await _db.SaveChangesAsync();
            return plant;
        }



        public async Task<GardenPlant?> MoveGardenPlant(int plantId, int newOrder)
        {
            var plant = await _db.GardenPlants.FindAsync(plantId);
            if (plant == null) return null;

            plant.Order = newOrder;
            plant.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return plant;
        }

        // ✅ Store = pot the plant (not deleted, just stored)
        public async Task<GardenPlant?> StoreGardenPlant(int plantId)
        {
            var plant = await _db.GardenPlants.FindAsync(plantId);
            if (plant == null) return null;

            plant.IsStored = true;
            plant.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return plant;
        }

        // ✅ Restore = move plant back from storage into garden
        public async Task<GardenPlant?> RestoreGardenPlant(int plantId, int newOrder)
        {
            var plant = await _db.GardenPlants.FindAsync(plantId);
            if (plant == null) return null;

            plant.IsStored = false;
            plant.Order = newOrder;
            plant.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return plant;
        }

        public async Task<bool> DeleteGardenPlant(int id)
        {
            var plant = await _db.GardenPlants.FindAsync(id);
            if (plant == null) return false;

            _db.GardenPlants.Remove(plant);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}