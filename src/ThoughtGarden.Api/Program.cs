using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ThoughtGarden.Api.Data;
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

builder.Services.AddScoped<JwtHelper>();
builder.Services.AddSingleton<EnvelopeCrypto>();


// GraphQL (Hot Chocolate)
var gql = builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .AddQueryType(d => d.Name("Query"))
        .AddTypeExtension<UserQueries>()
        .AddTypeExtension<JournalEntryQueries>()
        .AddTypeExtension<GardenQueries>()
        .AddTypeExtension<GardenPlantQueries>()
        .AddTypeExtension<EmotionQueries>()
        .AddTypeExtension<PlantTypeQueries>()
        .AddTypeExtension<ServerInfoQueries>()
    .AddMutationType(d => d.Name("Mutation"))
        .AddTypeExtension<UserMutations>()
        .AddTypeExtension<JournalEntryMutations>()
        .AddTypeExtension<EmotionTagMutations>()
        .AddTypeExtension<GardenStateMutations>()
        .AddTypeExtension<GardenPlantMutations>()
        .AddTypeExtension<PlantTypeMutations>();

if (builder.Environment.IsDevelopment())
{
    gql.AddTypeExtension<MaintenanceMutations>(); // dev-only
}

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

// Authentication + Authorization
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Convert.FromBase64String(builder.Configuration["Jwt:Key"]!)
            )
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHostedService<ThoughtGarden.Api.Infrastructure.DevSeedHostedService>();


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

// Authentication + Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();        // REST
app.MapGraphQL("/graphql");  // GraphQL

app.Run();

public partial class Program { }