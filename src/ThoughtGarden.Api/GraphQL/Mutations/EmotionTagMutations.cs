using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using HotChocolate.Authorization;

namespace ThoughtGarden.Api.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class EmotionTagMutations
    {
        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        public async Task<EmotionTag> AddEmotionTag(string name, string color, string? icon, [Service] ThoughtGardenDbContext db)
        {
            var tag = new EmotionTag { Name = name, Color = color, Icon = icon };
            db.EmotionTags.Add(tag);
            await db.SaveChangesAsync();
            return tag;
        }

        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        public async Task<EmotionTag?> UpdateEmotionTag(int id, string? name, string? color, string? icon, [Service] ThoughtGardenDbContext db)
        {
            var tag = await db.EmotionTags.FindAsync(id);
            if (tag == null) return null;

            if (!string.IsNullOrWhiteSpace(name)) tag.Name = name;
            if (!string.IsNullOrWhiteSpace(color)) tag.Color = color;
            if (!string.IsNullOrWhiteSpace(icon)) tag.Icon = icon;

            await db.SaveChangesAsync();
            return tag;
        }

        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        public async Task<bool> DeleteEmotionTag(int id, [Service] ThoughtGardenDbContext db)
        {
            var tag = await db.EmotionTags.FindAsync(id);
            if (tag == null) return false;

            db.EmotionTags.Remove(tag);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
