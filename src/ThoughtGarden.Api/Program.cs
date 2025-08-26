using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL;
using ThoughtGarden.Api.GraphQL.Mutations;
using ThoughtGarden.Api.GraphQL.Queries;

var builder = WebApplication.CreateBuilder(args);

// Add services to container
builder.Services.AddControllers();

// EF Core DbContext
builder.Services.AddDbContext<ThoughtGardenDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention()
        );

// GraphQL (Hot Chocolate)
builder.Services
    .AddGraphQLServer()
    .AddQueryType(d => d.Name("Query"))
        .AddTypeExtension<RootQueries>()
        .AddTypeExtension<UserQueries>()
        .AddTypeExtension<JournalEntryQueries>()
        .AddTypeExtension<GardenQueries>()
        .AddTypeExtension<PlantQueries>()
        .AddTypeExtension<EmotionQueries>()
        .AddTypeExtension<ServerInfoQueries>()
    .AddMutationType(d => d.Name("Mutation"))
        .AddTypeExtension<RootMutations>()
        .AddTypeExtension<UserMutations>()
        .AddTypeExtension<JournalEntryMutations>()
        .AddTypeExtension<EmotionTagMutations>()
        .AddTypeExtension<GardenStateMutations>()
        .AddTypeExtension<GardenPlantMutations>()
        .AddTypeExtension<PlantTypeMutations>();

// Swagger for REST endpoints
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS policy — allow all (safe for local dev)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Enable CORS before routing
app.UseCors();

// Development tools
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();        // REST
app.MapGraphQL("/graphql");  // GraphQL

app.Run();
