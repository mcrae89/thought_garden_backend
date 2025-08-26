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

        public async Task<List<EmotionTag>> GetEmotions() =>
            await _db.EmotionTags.ToListAsync();

        public async Task<EmotionTag?> GetEmotionById(int id) =>
            await _db.EmotionTags.FirstOrDefaultAsync(e => e.Id == id);
    }
}
