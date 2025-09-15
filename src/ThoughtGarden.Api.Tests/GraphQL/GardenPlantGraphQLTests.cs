using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Tests.GraphQL
{
    public class GardenPlantGraphQLTests : IClassFixture<PostgresFixture>
    {
        private readonly HttpClient _client;
        private readonly ApiFactory _factory;

        public GardenPlantGraphQLTests(PostgresFixture fixture)
        {
            _factory = new ApiFactory(fixture.ConnectionString);
            _client = _factory.CreateClient();
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private (int Id, string UserName, string Email) EnsureSeedUser(UserRole role, string userName, string email)
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

        private int CreateEmotionTag(string name = "Joy", string color = "#00FF00")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var tag = new EmotionTag { Name = name, Color = color };
            db.EmotionTags.Add(tag);
            db.SaveChanges();
            return tag.Id;
        }

        private PlantType CreatePlantType(string name = "Rose")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var etid = CreateEmotionTag("Happy");
            var pt = new PlantType { Name = name, EmotionTagId = etid };
            db.PlantTypes.Add(pt);
            db.SaveChanges();
            return pt;
        }

        private GardenState CreateGardenState(int userId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var gs = new GardenState { UserId = userId, SnapshotAt = DateTime.UtcNow };
            db.GardenStates.Add(gs);
            db.SaveChanges();
            return gs;
        }

        private GardenPlant CreateGardenPlant(int userId, string plantName = "Tulip")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var gs = CreateGardenState(userId);
            var pt = CreatePlantType(plantName);

            var gp = new GardenPlant
            {
                GardenStateId = gs.Id,
                PlantTypeId = pt.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsStored = true
            };

            db.GardenPlants.Add(gp);
            db.SaveChanges();
            return gp;
        }

        // ---------------------------
        // Query Tests
        // ---------------------------

        [Fact]
        public async Task GetStoredPlants_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_self", "gp_self@test.com");
            AuthenticateAs(user, "User");
            CreateGardenPlant(user.Id);

            var payload = new { query = $"{{ storedPlants(userId:{user.Id}) {{ id gardenStateId plantTypeId isStored }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task GetStoredPlants_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "gp_admin", "gp_admin@test.com");
            var user = EnsureSeedUser(UserRole.User, "gp_user", "gp_user@test.com");
            AuthenticateAs(admin, "Admin");
            var plant = CreateGardenPlant(user.Id);

            var payload = new { query = $"{{ storedPlants(userId:{user.Id}) {{ id gardenStateId plantTypeId isStored }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{plant.Id}", json);
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task GetStoredPlants_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_u1", "gp_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_u2", "gp_u2@test.com");
            AuthenticateAs(u1, "User");
            CreateGardenPlant(u2.Id);

            var payload = new { query = $"{{ storedPlants(userId:{u2.Id}) {{ id }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetActivePlants_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_active_self", "gp_active_self@test.com");
            AuthenticateAs(user, "User");
            var gp = CreateGardenPlant(user.Id);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
                gp.IsStored = false;
                gp.Order = 1;
                db.GardenPlants.Update(gp);
                db.SaveChanges();
            }

            var payload = new { query = $"{{ activePlants(gardenStateId:{gp.GardenStateId}) {{ id order isStored }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("false", json);
        }

        [Fact]
        public async Task GetActivePlants_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_act_u1", "gp_act_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_act_u2", "gp_act_u2@test.com");
            AuthenticateAs(u1, "User");
            var gp = CreateGardenPlant(u2.Id);

            var payload = new { query = $"{{ activePlants(gardenStateId:{gp.GardenStateId}) {{ id order }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        [Fact]
        public async Task GetActivePlants_Returns_Empty_When_None()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_empty", "gp_empty@test.com");
            AuthenticateAs(user, "User");
            var gs = CreateGardenState(user.Id);

            var payload = new { query = $"{{ activePlants(gardenStateId:{gs.Id}) {{ id order }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        // ---------------------------
        // Mutation Tests
        // ---------------------------

        // --- Add ---
        [Fact]
        public async Task AddGardenPlant_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_add_self", "gp_add_self@test.com");
            AuthenticateAs(user, "User");
            var gs = CreateGardenState(user.Id);
            var pt = CreatePlantType("Lily");

            var payload = new { query = $"mutation {{ addGardenPlant(gardenStateId:{gs.Id}, plantTypeId:{pt.Id}) {{ id gardenStateId plantTypeId }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("gardenStateId", json);
        }

        [Fact]
        public async Task AddGardenPlant_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "gp_add_admin", "gp_add_admin@test.com");
            var user = EnsureSeedUser(UserRole.User, "gp_add_target", "gp_add_target@test.com");
            AuthenticateAs(admin, "Admin");
            var gs = CreateGardenState(user.Id);
            var pt = CreatePlantType("AdminPlant");

            var payload = new { query = $"mutation {{ addGardenPlant(gardenStateId:{gs.Id}, plantTypeId:{pt.Id}) {{ id gardenStateId plantTypeId }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains($"{gs.Id}", json);
            Assert.Contains($"{pt.Id}", json);
        }

        [Fact]
        public async Task AddGardenPlant_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_add_u1", "gp_add_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_add_u2", "gp_add_u2@test.com");
            AuthenticateAs(u1, "User");
            var gs = CreateGardenState(u2.Id);
            var pt = CreatePlantType("Blocked");

            var payload = new { query = $"mutation {{ addGardenPlant(gardenStateId:{gs.Id}, plantTypeId:{pt.Id}) {{ id }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // --- Grow ---
        [Fact]
        public async Task GrowGardenPlant_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_grow_self", "gp_grow_self@test.com");
            AuthenticateAs(user, "User");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ growGardenPlant(plantId:{gp.Id}) {{ id growthProgress }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("growthProgress", json);
        }

        [Fact]
        public async Task GrowGardenPlant_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "gp_grow_admin", "gp_grow_admin@test.com");
            var user = EnsureSeedUser(UserRole.User, "gp_grow_target", "gp_grow_target@test.com");
            AuthenticateAs(admin, "Admin");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ growGardenPlant(plantId:{gp.Id}, growthMultiplier:2) {{ id growthProgress }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("growthProgress", json);
        }

        [Fact]
        public async Task GrowGardenPlant_Returns_Null_When_Not_Found()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_grow_nf", "gp_grow_nf@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = "mutation { growGardenPlant(plantId:99999) { id growthProgress } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("null", json);
        }

        [Fact]
        public async Task GrowGardenPlant_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_grow_u1", "gp_grow_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_grow_u2", "gp_grow_u2@test.com");
            AuthenticateAs(u1, "User");
            var gp = CreateGardenPlant(u2.Id);

            var payload = new { query = $"mutation {{ growGardenPlant(plantId:{gp.Id}) {{ id growthProgress }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // --- Move ---
        [Fact]
        public async Task MoveGardenPlant_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_move_self", "gp_move_self@test.com");
            AuthenticateAs(user, "User");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ moveGardenPlant(plantId:{gp.Id}, newOrder:5) {{ id order }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("5", json);
        }

        [Fact]
        public async Task MoveGardenPlant_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "gp_move_admin", "gp_move_admin@test.com");
            var user = EnsureSeedUser(UserRole.User, "gp_move_target", "gp_move_target@test.com");
            AuthenticateAs(admin, "Admin");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ moveGardenPlant(plantId:{gp.Id}, newOrder:3) {{ id order }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("3", json);
        }

        [Fact]
        public async Task MoveGardenPlant_Returns_Null_When_Not_Found()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_move_nf", "gp_move_nf@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = "mutation { moveGardenPlant(plantId:99999, newOrder:1) { id order } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("null", json);
        }

        [Fact]
        public async Task MoveGardenPlant_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_move_u1", "gp_move_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_move_u2", "gp_move_u2@test.com");
            AuthenticateAs(u1, "User");
            var gp = CreateGardenPlant(u2.Id);

            var payload = new { query = $"mutation {{ moveGardenPlant(plantId:{gp.Id}, newOrder:5) {{ id order }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // --- Store ---
        [Fact]
        public async Task StoreGardenPlant_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_store_self", "gp_store_self@test.com");
            AuthenticateAs(user, "User");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ storeGardenPlant(plantId:{gp.Id}) {{ id isStored }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task StoreGardenPlant_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "gp_store_admin", "gp_store_admin@test.com");
            var user = EnsureSeedUser(UserRole.User, "gp_store_target", "gp_store_target@test.com");
            AuthenticateAs(admin, "Admin");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ storeGardenPlant(plantId:{gp.Id}) {{ id isStored }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task StoreGardenPlant_Returns_Null_When_Not_Found()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_store_nf", "gp_store_nf@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = "mutation { storeGardenPlant(plantId:99999) { id isStored } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("null", json);
        }

        [Fact]
        public async Task StoreGardenPlant_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_store_u1", "gp_store_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_store_u2", "gp_store_u2@test.com");
            AuthenticateAs(u1, "User");
            var gp = CreateGardenPlant(u2.Id);

            var payload = new { query = $"mutation {{ storeGardenPlant(plantId:{gp.Id}) {{ id isStored }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // --- Restore ---
        [Fact]
        public async Task RestoreGardenPlant_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_restore_self", "gp_restore_self@test.com");
            AuthenticateAs(user, "User");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ restoreGardenPlant(plantId:{gp.Id}, newOrder:2) {{ id isStored order }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("false", json); // not stored
            Assert.Contains("2", json);    // new order
        }

        [Fact]
        public async Task RestoreGardenPlant_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "gp_restore_admin", "gp_restore_admin@test.com");
            var user = EnsureSeedUser(UserRole.User, "gp_restore_target", "gp_restore_target@test.com");
            AuthenticateAs(admin, "Admin");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ restoreGardenPlant(plantId:{gp.Id}, newOrder:4) {{ id isStored order }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("false", json);
            Assert.Contains("4", json);
        }

        [Fact]
        public async Task RestoreGardenPlant_Returns_Null_When_Not_Found()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_restore_nf", "gp_restore_nf@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = "mutation { restoreGardenPlant(plantId:99999, newOrder:1) { id isStored } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("null", json);
        }

        [Fact]
        public async Task RestoreGardenPlant_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_restore_u1", "gp_restore_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_restore_u2", "gp_restore_u2@test.com");
            AuthenticateAs(u1, "User");
            var gp = CreateGardenPlant(u2.Id);

            var payload = new { query = $"mutation {{ restoreGardenPlant(plantId:{gp.Id}, newOrder:2) {{ id isStored }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // --- Delete ---
        [Fact]
        public async Task DeleteGardenPlant_Allows_Self()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_delete_self", "gp_delete_self@test.com");
            AuthenticateAs(user, "User");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ deleteGardenPlant(id:{gp.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteGardenPlant_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "gp_delete_admin", "gp_delete_admin@test.com");
            var user = EnsureSeedUser(UserRole.User, "gp_delete_target", "gp_delete_target@test.com");
            AuthenticateAs(admin, "Admin");
            var gp = CreateGardenPlant(user.Id);

            var payload = new { query = $"mutation {{ deleteGardenPlant(id:{gp.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteGardenPlant_Returns_False_When_Not_Found()
        {
            var user = EnsureSeedUser(UserRole.User, "gp_delete_nf", "gp_delete_nf@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = "mutation { deleteGardenPlant(id:99999) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("false", json);
        }

        [Fact]
        public async Task DeleteGardenPlant_Denies_Other_User()
        {
            var u1 = EnsureSeedUser(UserRole.User, "gp_delete_u1", "gp_delete_u1@test.com");
            var u2 = EnsureSeedUser(UserRole.User, "gp_delete_u2", "gp_delete_u2@test.com");
            AuthenticateAs(u1, "User");
            var gp = CreateGardenPlant(u2.Id);

            var payload = new { query = $"mutation {{ deleteGardenPlant(id:{gp.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
