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
        public IQueryable<EmotionTag> GetEmotionById(int id, [Service] ThoughtGardenDbContext db) =>
            db.EmotionTags.Where(e => e.Id == id);
    }
}
