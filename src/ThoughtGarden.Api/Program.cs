using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL;

var builder = WebApplication.CreateBuilder(args);

// Add services to container
builder.Services.AddControllers();

// EF Core DbContext (placeholder connection string from appsettings.json)
builder.Services.AddDbContext<ThoughtGardenDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// GraphQL (Hot Chocolate)
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Queries>()
    .AddMutationType<Mutations>();

// Swagger (still handy for REST endpoints)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Development-only tools
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();        // Enable REST controllers
app.MapGraphQL("/graphql");  // GraphQL endpoint

app.Run();
