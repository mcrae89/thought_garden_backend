using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL;

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
    .AddQueryType<Queries>()
    .AddMutationType<Mutations>()
    .AddMutationType(d => d.Name("Mutation"))
    .AddTypeExtension<UserMutations>();

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
