using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Mutations;
using ThoughtGarden.Api.GraphQL.Queries;
using ThoughtGarden.Api.Config;

static string? FindUp(string startDir, string file)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        var candidate = System.IO.Path.Combine(dir.FullName, file);
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}

var builder = WebApplication.CreateBuilder(args);

// .env for local dev only
var envPath = FindUp(builder.Environment.ContentRootPath, ".env");
if (envPath is not null && builder.Environment.IsDevelopment()) DotEnv.Load(envPath);

// Doppler if present (dev or prod; harmless if absent)
var dopplerToken = Environment.GetEnvironmentVariable("DOPPLER_TOKEN");
if (!string.IsNullOrWhiteSpace(dopplerToken)) builder.Configuration.AddDopplerSecrets();

// ---- Services ----
builder.Services.AddControllers();

// DbContext (pooling) + fail-fast for conn string
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection missing");
builder.Services.AddDbContextPool<ThoughtGardenDbContext>(opt =>
    opt.UseNpgsql(defaultConn).UseSnakeCaseNamingConvention());

// DI helpers (add others you use in resolvers)
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddSingleton<EnvelopeCrypto>();

// GraphQL + guardrails
var gql = builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .AddMaxExecutionDepthRule(16)
    .ModifyRequestOptions(o =>
    {
        o.ExecutionTimeout = TimeSpan.FromMinutes(2);
        o.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    })
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

// Swagger (dev only)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (dev: wide open; prod: restrict origins; JWT only => no credentials)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    options.AddPolicy("SpaHost", p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// JWT (hardening)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer missing");
        var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience missing");
        var keyB64 = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var keyBytes = Convert.FromBase64String(keyB64);

        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization();

// Dev seed (dev only; add an env flag later if you want staging seeds)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<ThoughtGarden.Api.Infrastructure.DevSeedHostedService>();
}

var app = builder.Build();

// ---- Middleware ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment()) app.UseCors("DevAll");
else app.UseCors("SpaHost");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGraphQL("/graphql");

app.Run();

public partial class Program { }
