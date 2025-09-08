using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using HotChocolate.Authorization;
using HotChocolate;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class PlantTypeQueries
    {
        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        [UseProjection]
        public IQueryable<PlantType> GetPlantTypes([Service] ThoughtGardenDbContext db)
        {
            return db.PlantTypes;
        }

        [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
        [UseProjection]
        public IQueryable<PlantType> GetPlantTypeById(int id, [Service] ThoughtGardenDbContext db)
        {
            return db.PlantTypes.Where(p => p.Id == id);
        }
    }
}
