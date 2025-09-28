using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using static ThoughtGarden.Models.GardenPlant;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GardenPlantMutations
    {
        [Authorize]
        public async Task<GardenPlant> AddGardenPlant(int gardenStateId, int plantTypeId, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db, double growthProgress = 0.0, GardenPlant.GrowthStage? stage = null)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);
            var isOwner = await db.GardenStates.AnyAsync(gs => gs.Id == gardenStateId && gs.UserId == callerId);
            if (!isOwner && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");

            var plant = new GardenPlant { GardenStateId = gardenStateId, PlantTypeId = plantTypeId, GrowthProgress = growthProgress, Stage = stage ?? GardenPlant.GrowthStage.Seed, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Order = null, IsStored = true };
            db.GardenPlants.Add(plant);
            await db.SaveChangesAsync();
            return plant;
        }

        [Authorize]
        public async Task<GardenPlant> GrowGardenPlant(int plantId, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db, int growthMultiplier = 1)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);

            var plant = await db.GardenPlants.Include(p => p.GardenState).FirstOrDefaultAsync(p => p.Id == plantId);
            if (plant == null) return null;
            if (plant.GardenState.UserId != callerId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");

            plant.GrowthProgress = plant.GrowthProgress + (5 * growthMultiplier);

            if (plant.GrowthProgress >= 100)
            {
                plant.GrowthProgress = 0;

                if (plant.Stage < GrowthStage.Bloom)
                {
                    plant.Stage++;
                }
            }

            await db.SaveChangesAsync();
            return plant;
        }

        [Authorize]
        public async Task<GardenPlant?> MatureGardenPlant(
    int plantId,
    ClaimsPrincipal claims,
    [Service] ThoughtGardenDbContext db)
        {
            var userId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var plant = await db.GardenPlants
                .Include(p => p.GardenState)
                .FirstOrDefaultAsync(p => p.Id == plantId);

            if (plant == null)
                throw new GraphQLException("Plant not found");

            if (plant.GardenState.UserId != userId && !claims.IsInRole("Admin"))
                throw new GraphQLException("Not authorized");

            if (plant.Stage != GardenPlant.GrowthStage.Bloom)
                throw new GraphQLException("Only blooming plants can mature");

            plant.Stage = GardenPlant.GrowthStage.Mature;
            plant.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return plant;
        }



        [Authorize]
        public async Task<GardenPlant?> MoveGardenPlant(int plantId, int newOrder, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);

            var plant = await db.GardenPlants.Include(p => p.GardenState).FirstOrDefaultAsync(p => p.Id == plantId);
            if (plant == null) return null;
            if (plant.GardenState.UserId != callerId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");

            plant.Order = newOrder;
            plant.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return plant;
        }

        [Authorize]
        public async Task<GardenPlant?> StoreGardenPlant(int plantId, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);

            var plant = await db.GardenPlants.Include(p => p.GardenState).FirstOrDefaultAsync(p => p.Id == plantId);
            if (plant == null) return null;
            if (plant.GardenState.UserId != callerId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");

            plant.IsStored = true;
            plant.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return plant;
        }

        [Authorize]
        public async Task<GardenPlant?> RestoreGardenPlant(int plantId, int newOrder, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);

            var plant = await db.GardenPlants.Include(p => p.GardenState).FirstOrDefaultAsync(p => p.Id == plantId);
            if (plant == null) return null;
            if (plant.GardenState.UserId != callerId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");

            plant.IsStored = false;
            plant.Order = newOrder;
            plant.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return plant;
        }

        [Authorize]
        public async Task<bool> DeleteGardenPlant(int id, ClaimsPrincipal claims, [Service] ThoughtGardenDbContext db)
        {
            var callerId = int.Parse(claims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = claims.FindFirstValue(ClaimTypes.Role);

            var plant = await db.GardenPlants.Include(p => p.GardenState).FirstOrDefaultAsync(p => p.Id == id);
            if (plant == null) return false;
            if (plant.GardenState.UserId != callerId && role != UserRole.Admin.ToString()) throw new GraphQLException("Not authorized");

            db.GardenPlants.Remove(plant);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
