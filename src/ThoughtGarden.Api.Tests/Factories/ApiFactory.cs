using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Tests.Factories
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        public string JwtKey { get; private set; } = string.Empty;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // 🔹 Generate a secure 256-bit (32 byte) key for HS256
                var keyBytes = RandomNumberGenerator.GetBytes(32);
                JwtKey = Convert.ToBase64String(keyBytes);

                var testSettings = new Dictionary<string, string?>
            {
                { "Jwt:Key", JwtKey },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" }
            };

                config.AddInMemoryCollection(testSettings!);
            });

            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registrations
                var toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ThoughtGardenDbContext>) ||
                        d.ServiceType == typeof(ThoughtGardenDbContext) ||
                        (d.ServiceType.IsGenericType &&
                         d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextFactory<>)))
                    .ToList();

                foreach (var d in toRemove)
                    services.Remove(d);

                // Point to your test Postgres
                var conn = Environment.GetEnvironmentVariable("TG_TEST_DB")
                           ?? "Host=localhost;Port=5432;Database=thoughtgarden_test;Username=postgres;Password=postgres";

                services.AddDbContext<ThoughtGardenDbContext>(o => o.UseNpgsql(conn));

                // Build the container so we can migrate & seed immediately.
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

                db.Database.EnsureDeleted();
                db.Database.Migrate();

                // Always ensure a plan exists
                var plan = db.SubscriptionPlans.FirstOrDefault();
                if (plan == null)
                {
                    plan = new SubscriptionPlan
                    {
                        Name = "Test Plan",
                        MaxJournalEntriesPerDay = 5,
                        MaxGardenCustomizationsPerDay = 3,
                        Price = 0m,
                        BillingPeriod = "Monthly"
                    };
                    db.SubscriptionPlans.Add(plan);
                    db.SaveChanges();
                }

                // Always ensure a user exists
                var user = db.Users.FirstOrDefault(u => u.UserName == "seeduser");
                if (user == null)
                {
                    user = new User
                    {
                        UserName = "seeduser",
                        Email = "seed@test.com",
                        PasswordHash = "hash",
                        Role = UserRole.User,
                        SubscriptionPlanId = plan.Id
                    };
                    db.Users.Add(user);
                    db.SaveChanges();
                }

            });
        }
    }
}
