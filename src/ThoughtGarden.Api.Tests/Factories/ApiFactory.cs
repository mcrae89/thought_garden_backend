using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL; // <-- needed for UseSnakeCaseNamingConvention
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Tests.Factories
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        // Strong 256-bit key for HMAC-SHA256
        public string JwtKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        public string JwtIssuer { get; } = "TestIssuer";
        public string JwtAudience { get; } = "TestAudience";

        public ApiFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Force the app into "Testing" so Program.cs skips Doppler/.env and default DbContext.
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                var primaryId = "test-primary";
                var recoveryId = "test-recovery";
                var primaryB64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var recoveryB64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

                var dict = new Dictionary<string, string?>
                {
                    // JWT used by tests
                    ["Jwt:Key"] = JwtKey,
                    ["Jwt:Issuer"] = JwtIssuer,
                    ["Jwt:Audience"] = JwtAudience,

                    // DB (factory re-wires DbContext to this Testcontainers connection)
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,

                    // ✅ EXACT shape EnvelopeCrypto expects
                    ["Encryption:ActivePrimaryKeyId"] = primaryId,
                    ["Encryption:ActiveRecoveryKeyId"] = recoveryId,
                    // children under Encryption:Keys with a *value* (no :Key)
                    [$"Encryption:Keys:{primaryId}"] = primaryB64,
                    [$"Encryption:Keys:{recoveryId}"] = recoveryB64,

                    // optional legacy key (safe to keep)
                    ["Encryption:JournalEncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                };

                config.AddInMemoryCollection(dict);
            });

            builder.ConfigureServices(services =>
            {
                // Remove app’s default DbContext registration (if any slipped in).
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ThoughtGardenDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                // IMPORTANT: mirror production EF options, including snake_case
                services.AddDbContext<ThoughtGardenDbContext>(options =>
                    options
                        .UseNpgsql(_connectionString, npgsql =>
                            npgsql.MigrationsAssembly(typeof(ThoughtGardenDbContext).Assembly.FullName))
                        .UseSnakeCaseNamingConvention()
                );

                // Apply migrations and seed baseline data
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

                db.Database.Migrate();

                // Seed subscription plan (needed by user)
                if (!db.SubscriptionPlans.Any())
                {
                    db.SubscriptionPlans.Add(new SubscriptionPlan { Name = "Default", Price = 0 });
                    db.SaveChanges();
                }

                // Seed a deterministic user used by some tests
                if (!db.Users.Any(u => u.UserName == "seeduser"))
                {
                    var planId = db.SubscriptionPlans.Select(p => p.Id).First();
                    db.Users.Add(new User
                    {
                        UserName = "seeduser",
                        Email = "seed@test.com",
                        PasswordHash = PasswordHelper.HashPassword("P@ssw0rd!"),
                        Role = UserRole.User,
                        SubscriptionPlanId = planId
                    });
                    db.SaveChanges();
                }
            });
        }
    }
}
