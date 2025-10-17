using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class EmotionQueries
    {
        [UseProjection]
        public IQueryable<EmotionTag> GetEmotions([Service] ThoughtGardenDbContext db) => db.EmotionTags;

        [UseProjection]
        public async Task<EmotionTag?> GetEmotionById(int id, [Service] ThoughtGardenDbContext db)
            => await db.EmotionTags.FirstOrDefaultAsync(e => e.Id == id);
    }
}
