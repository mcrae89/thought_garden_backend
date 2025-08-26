using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class EmotionTagMutations
    {
        private readonly ThoughtGardenDbContext _db;

        public EmotionTagMutations(ThoughtGardenDbContext db)
        {
            _db = db;
        }
        public async Task<EmotionTag> AddEmotionTag(string name,string color,string? icon)
        {
            var tag = new EmotionTag { Name = name, Color = color, Icon = icon };
            _db.EmotionTags.Add(tag);
            await _db.SaveChangesAsync();
            return tag;
        }

        public async Task<EmotionTag?> UpdateEmotionTag(int id,string? name,string? color,string? icon)
        {
            var tag = await _db.EmotionTags.FindAsync(id);
            if (tag == null) return null;

            if (!string.IsNullOrWhiteSpace(name)) tag.Name = name;
            if (!string.IsNullOrWhiteSpace(color)) tag.Color = color;
            if (!string.IsNullOrWhiteSpace(icon)) tag.Icon = icon;

            await _db.SaveChangesAsync();
            return tag;
        }

        public async Task<bool> DeleteEmotionTag(int id)
        {
            var tag = await _db.EmotionTags.FindAsync(id);
            if (tag == null) return false;

            _db.EmotionTags.Remove(tag);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}