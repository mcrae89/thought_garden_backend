using ThoughtGarden.Api.Data;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class PlantTypeMutations
    {
        private readonly ThoughtGardenDbContext _db;

        public PlantTypeMutations(ThoughtGardenDbContext db)
        {
            _db = db;
        }

        public async Task<PlantType> AddPlantType(string name,int emotionTagId)
        {
            var type = new PlantType { Name = name, EmotionTagId = emotionTagId };
            _db.PlantTypes.Add(type);
            await _db.SaveChangesAsync();
            return type;
        }

        public async Task<PlantType?> UpdatePlantType(int id,string? name,int? emotionTagId)
        {
            var type = await _db.PlantTypes.FindAsync(id);
            if (type == null) return null;

            if (!string.IsNullOrWhiteSpace(name)) type.Name = name;
            if (emotionTagId.HasValue) type.EmotionTagId = emotionTagId.Value;

            await _db.SaveChangesAsync();
            return type;
        }

        public async Task<bool> DeletePlantType(int id)
        {
            var type = await _db.PlantTypes.FindAsync(id);
            if (type == null) return false;

            _db.PlantTypes.Remove(type);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
