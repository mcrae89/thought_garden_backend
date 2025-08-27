using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class EmotionQueries
    {
        private readonly ThoughtGardenDbContext _db;

        public EmotionQueries(ThoughtGardenDbContext db) => _db = db;

        [UseProjection]
        public IQueryable<EmotionTag> GetEmotions() => _db.EmotionTags;

        [UseProjection]
        public IQueryable<EmotionTag> GetEmotionById(int id) =>
            _db.EmotionTags.Where(e => e.Id == id);
    }
}
