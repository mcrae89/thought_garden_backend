using ThoughtGarden.Api.Data;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class PlantTypeMutations
    {
        public async Task<PlantType> AddPlantType(string name,int emotionTagId, [Service] ThoughtGardenDbContext db)
        {
            var type = new PlantType { Name = name, EmotionTagId = emotionTagId };
            db.PlantTypes.Add(type);
            await db.SaveChangesAsync();
            return type;
        }

        public async Task<PlantType?> UpdatePlantType(int id,string? name,int? emotionTagId, [Service] ThoughtGardenDbContext db)
        {
            var type = await db.PlantTypes.FindAsync(id);
            if (type == null) return null;

            if (!string.IsNullOrWhiteSpace(name)) type.Name = name;
            if (emotionTagId.HasValue) type.EmotionTagId = emotionTagId.Value;

            await db.SaveChangesAsync();
            return type;
        }

        public async Task<bool> DeletePlantType(int id, [Service] ThoughtGardenDbContext db)
        {
            var type = await db.PlantTypes.FindAsync(id);
            if (type == null) return false;

            db.PlantTypes.Remove(type);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
