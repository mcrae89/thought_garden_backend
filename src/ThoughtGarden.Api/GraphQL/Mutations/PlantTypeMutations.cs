using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class PlantTypeMutations
    {
        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        public async Task<PlantType> AddPlantType(string name, int emotionTagId, [Service] ThoughtGardenDbContext db)
        {
            var type = new PlantType { Name = name, EmotionTagId = emotionTagId };
            db.PlantTypes.Add(type);
            await db.SaveChangesAsync();
            return type;
        }

        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        public async Task<PlantType?> UpdatePlantType(int id, string? name, int? emotionTagId, [Service] ThoughtGardenDbContext db)
        {
            var type = await db.PlantTypes.FindAsync(id);
            if (type == null) throw new GraphQLException("Plant type not found");

            if (!string.IsNullOrWhiteSpace(name)) type.Name = name;
            if (emotionTagId.HasValue) type.EmotionTagId = emotionTagId.Value;

            await db.SaveChangesAsync();
            return type;
        }

        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        public async Task<bool> DeletePlantType(int id, [Service] ThoughtGardenDbContext db)
        {
            var type = await db.PlantTypes.FindAsync(id);
            if (type == null) throw new GraphQLException("Plant type not found");

            db.PlantTypes.Remove(type);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
