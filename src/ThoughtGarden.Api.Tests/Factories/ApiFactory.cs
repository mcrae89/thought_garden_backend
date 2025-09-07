using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;
using System.Security.Cryptography;

namespace ThoughtGarden.Api.Tests.Factories
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        // Strong random 256-bit key
        public string JwtKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        public string JwtIssuer { get; } = "TestIssuer";
        public string JwtAudience { get; } = "TestAudience";

        public ApiFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var dict = new Dictionary<string, string?>
                {
                    { "Jwt:Key", JwtKey },
                    { "Jwt:Issuer", JwtIssuer },
                    { "Jwt:Audience", JwtAudience }
                };
                config.AddInMemoryCollection(dict!);
            });

            builder.ConfigureServices(services =>
            {
                // Replace DbContext with Testcontainers connection string
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ThoughtGardenDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ThoughtGardenDbContext>(options =>
                    options.UseNpgsql(_connectionString, npgsql =>
                        npgsql.MigrationsAssembly(typeof(ThoughtGardenDbContext).Assembly.FullName)));

                // Apply migrations + seed
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
                db.Database.Migrate();

                if (!db.SubscriptionPlans.Any())
                {
                    db.SubscriptionPlans.Add(new SubscriptionPlan { Name = "Default", Price = 0 });
                    db.SaveChanges();
                }

                if (!db.Users.Any(u => u.UserName == "seeduser"))
                {
                    var planId = db.SubscriptionPlans.First().Id;
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
