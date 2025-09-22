using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;
using Xunit;

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
        private (int Id, string UserName, string Email) EnsureUser(
            string userName = "seeduser",
            string email = "seed@test.com",
            UserRole role = UserRole.User)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var u = db.Users.FirstOrDefault(x => x.UserName == userName);
            if (u == null)
            {
                var planId = db.SubscriptionPlans.Select(p => p.Id).First();
                u = new User
                {
                    UserName = userName,
                    Email = email,
                    PasswordHash = PasswordHelper.HashPassword("P@ssw0rd!"),
                    Role = role,
                    SubscriptionPlanId = planId
                };
                db.Users.Add(u);
                db.SaveChanges();
            }
            return (u.Id, u.UserName, u.Email);
        }

        private void Authenticate((int Id, string UserName, string Email) user, string role)
        {
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                user.Id, user.UserName, user.Email, role);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private GardenState CreateGardenState(int userId, int size = 5)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var gs = new GardenState
            {
                UserId = userId,
                SnapshotAt = DateTime.UtcNow,
                Size = size
            };
            db.GardenStates.Add(gs);
            db.SaveChanges();
            return gs;
        }

        // ---------------------------
        // Query Tests
        // ---------------------------
        [Fact]
        public async Task GetGardens_Allows_Self()
        {
            var user = EnsureUser("garden_self", "garden_self@test.com");
            Authenticate(user, "User");
            CreateGardenState(user.Id);

            var payload = new { query = $"{{ gardens(userId:{user.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{user.Id}", json);
        }

        [Fact]
        public async Task GetGardens_Allows_Admin()
        {
            var admin = EnsureUser("garden_admin", "garden_admin@test.com", UserRole.Admin);
            var other = EnsureUser("garden_other", "garden_other@test.com");
            Authenticate(admin, "Admin");
            CreateGardenState(other.Id);

            var payload = new { query = $"{{ gardens(userId:{other.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{other.Id}", json);
        }

        [Fact]
        public async Task GetGardens_Denies_Other_User()
        {
            var u1 = EnsureUser("garden_u1", "garden_u1@test.com");
            var u2 = EnsureUser("garden_u2", "garden_u2@test.com");
            Authenticate(u1, "User");
            CreateGardenState(u2.Id);

            var payload = new { query = $"{{ gardens(userId:{u2.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetGardens_Returns_Empty_When_None()
        {
            var user = EnsureUser("garden_none", "garden_none@test.com");
            Authenticate(user, "User");

            var payload = new { query = $"{{ gardens(userId:{user.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        [Fact]
        public async Task GetGardenById_Allows_Self()
        {
            var user = EnsureUser("garden_byid_self", "garden_byid_self@test.com");
            Authenticate(user, "User");
            var gs = CreateGardenState(user.Id);

            var payload = new { query = $"{{ gardenById(id:{gs.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{gs.Id}", json);
        }

        [Fact]
        public async Task GetGardenById_Allows_Admin()
        {
            var admin = EnsureUser("garden_byid_admin", "garden_byid_admin@test.com", UserRole.Admin);
            var other = EnsureUser("garden_byid_other", "garden_byid_other@test.com");
            Authenticate(admin, "Admin");
            var gs = CreateGardenState(other.Id);

            var payload = new { query = $"{{ gardenById(id:{gs.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{gs.Id}", json);
        }

        [Fact]
        public async Task GetGardenById_Denies_Other_User()
        {
            var u1 = EnsureUser("garden_byid_u1", "garden_byid_u1@test.com");
            var u2 = EnsureUser("garden_byid_u2", "garden_byid_u2@test.com");
            Authenticate(u1, "User");
            var gs = CreateGardenState(u2.Id);

            var payload = new { query = $"{{ gardenById(id:{gs.Id}) {{ id userId size }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            // Expect an authorization error in the response
            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetGardenById_Returns_Empty_When_Not_Found()
        {
            var admin = EnsureUser("garden_byid_nf", "garden_byid_nf@test.com", UserRole.Admin);
            Authenticate(admin, "Admin");

            var payload = new { query = "{ gardenById(id:99999) { id userId size } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            // GraphQL now returns null instead of []
            Assert.Contains("\"gardenById\":null", json);
        }


        // ---------------------------
        // Mutation Tests
        // ---------------------------
        [Fact]
        public async Task CreateGardenState_Allows_Self()
        {
            var user = EnsureUser("garden_create", "garden_create@test.com");
            Authenticate(user, "User");

            var payload = new { query = "mutation { createGardenState { id userId size } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{user.Id}", json);
        }

        [Fact]
        public async Task CreateGardenState_Allows_Admin()
        {
            var admin = EnsureUser("garden_create_admin", "garden_create_admin@test.com", UserRole.Admin);
            Authenticate(admin, "Admin");

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
