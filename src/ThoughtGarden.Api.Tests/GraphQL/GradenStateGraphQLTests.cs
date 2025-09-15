using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Tests.GraphQL
{
    public class GardenStateGraphQLTests : IClassFixture<PostgresFixture>
    {
        private readonly HttpClient _client;
        private readonly ApiFactory _factory;

        public GardenStateGraphQLTests(PostgresFixture fixture)
        {
            _factory = new ApiFactory(fixture.ConnectionString);
            _client = _factory.CreateClient();
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private (int Id, string UserName, string Email) EnsureSeedUser(
            UserRole role = UserRole.User,
            string userName = "seeduser",
            string email = "seed@test.com")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var u = db.Users.SingleOrDefault(x => x.UserName == userName);
            if (u == null)
            {
                var planId = db.SubscriptionPlans.Select(p => p.Id).FirstOrDefault();
                if (planId == 0) throw new InvalidOperationException("No subscription plan found.");

                u = new User
                {
                    UserName = userName,
                    Email = email,
                    PasswordHash = "x",
                    Role = role,
                    SubscriptionPlanId = planId
                };
                db.Users.Add(u);
                db.SaveChanges();
            }

            return (u.Id, u.UserName, u.Email);
        }

        private void AuthenticateAs((int Id, string UserName, string Email) user, string role)
        {
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                user.Id, user.UserName, user.Email, role: role);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private GardenState CreateSeedGarden(int userId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var gs = new GardenState
            {
                UserId = userId,
                SnapshotAt = DateTime.UtcNow,
                Size = 5
            };

            db.GardenStates.Add(gs);
            db.SaveChanges();
            return gs;
        }

        // ---------------------------
        // Query tests
        // ---------------------------

        // --- GetGardens ---
        [Fact]
        public async Task GetGardens_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "garden_self", "garden_self@test.com");
            AuthenticateAs(user, "User");
            CreateSeedGarden(user.Id);

            var payload = new { query = $"{{ gardens(userId:{user.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{user.Id}", json);
        }

        [Fact]
        public async Task GetGardens_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "garden_admin", "garden_admin@test.com");
            var other = EnsureSeedUser(UserRole.User, "garden_other", "garden_other@test.com");
            AuthenticateAs(admin, "Admin");
            CreateSeedGarden(other.Id);

            var payload = new { query = $"{{ gardens(userId:{other.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{other.Id}", json);
        }

        [Fact]
        public async Task GetGardens_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "garden_u1", "garden_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "garden_u2", "garden_u2@test.com");
            AuthenticateAs(u1, "User");
            CreateSeedGarden(u2.Id);

            var payload = new { query = $"{{ gardens(userId:{u2.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetGardens_Returns_Empty_When_None()
        {
            var user = EnsureSeedUser(UserRole.User, "garden_none", "garden_none@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = $"{{ gardens(userId:{user.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        // --- GetGardenById ---
        [Fact]
        public async Task GetGardenById_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "garden_byid_self", "garden_byid_self@test.com");
            AuthenticateAs(user, "User");
            var garden = CreateSeedGarden(user.Id);

            var payload = new { query = $"{{ gardenById(id:{garden.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{garden.Id}", json);
        }

        [Fact]
        public async Task GetGardenById_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "garden_byid_admin", "garden_byid_admin@test.com");
            var other = EnsureSeedUser(UserRole.User, "garden_byid_other", "garden_byid_other@test.com");
            AuthenticateAs(admin, "Admin");
            var garden = CreateSeedGarden(other.Id);

            var payload = new { query = $"{{ gardenById(id:{garden.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{garden.Id}", json);
        }

        [Fact]
        public async Task GetGardenById_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "garden_byid_u1", "garden_byid_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "garden_byid_u2", "garden_byid_u2@test.com");
            AuthenticateAs(u1, "User");
            var garden = CreateSeedGarden(u2.Id);

            var payload = new { query = $"{{ gardenById(id:{garden.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        [Fact]
        public async Task GetGardenById_Returns_Empty_When_Not_Found()
        {
            var user = EnsureSeedUser(UserRole.Admin, "garden_byid_nf", "garden_byid_nf@test.com");
            AuthenticateAs(user, "Admin");

            var payload = new { query = "{ gardenById(id:99999) { id userId size } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        // ---------------------------
        // Mutation tests
        // ---------------------------

        [Fact]
        public async Task CreateGardenState_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "garden_create", "garden_create@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = "mutation { createGardenState { id userId size } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{user.Id}", json);
        }

        [Fact]
        public async Task CreateGardenState_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "garden_create_admin", "garden_create_admin@test.com");
            AuthenticateAs(admin, "Admin");

            var payload = new { query = "mutation { createGardenState { id userId size } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{admin.Id}", json);
        }

        [Fact]
        public async Task CreateGardenState_Denies_Anonymous()
        {
            var payload = new { query = "mutation { createGardenState { id userId size } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}