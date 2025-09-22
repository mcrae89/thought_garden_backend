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
    public class PlantTypeGraphQLTests : IClassFixture<PostgresFixture>
    {
        private readonly HttpClient _client;
        private readonly ApiFactory _factory;

        public PlantTypeGraphQLTests(PostgresFixture fixture)
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

        private int CreateEmotionTag(string name = "Happy", string color = "#FF0000", string? icon = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var tag = new EmotionTag { Name = name, Color = color, Icon = icon };
            db.EmotionTags.Add(tag);
            db.SaveChanges();
            return tag.Id;
        }

        private PlantType CreateSeedPlantType(string name = "Rose")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var etid = CreateEmotionTag("Joy", "#00FF00");
            var pt = new PlantType { Name = name, EmotionTagId = etid };
            db.PlantTypes.Add(pt);
            db.SaveChanges();
            return pt;
        }

        // ---------------------------
        // Query Tests
        // ---------------------------
        [Fact]
        public async Task GetPlantTypes_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_admin", "pt_admin@test.com");
            AuthenticateAs(admin, "Admin");
            CreateSeedPlantType("Tulip");

            var payload = new { query = "{ plantTypes { id name emotionTagId } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Tulip", json);
        }

        [Fact]
        public async Task GetPlantTypes_Denies_User()
        {
            var user = EnsureSeedUser(UserRole.User, "pt_user", "pt_user@test.com");
            AuthenticateAs(user, "User");

            var payload = new { query = "{ plantTypes { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPlantTypes_Returns_Empty_When_No_Records()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_empty_admin", "pt_empty_admin@test.com");
            AuthenticateAs(admin, "Admin");

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
                db.PlantTypes.RemoveRange(db.PlantTypes);
                db.SaveChanges();
            }

            var payload = new { query = "{ plantTypes { id name emotionTagId } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        [Fact]
        public async Task GetPlantTypeById_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_byid_admin", "pt_byid_admin@test.com");
            AuthenticateAs(admin, "Admin");
            var pt = CreateSeedPlantType("Sunflower");

            var payload = new { query = $"{{ plantTypeById(id:{pt.Id}) {{ id name emotionTagId }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Sunflower", json);
        }

        [Fact]
        public async Task GetPlantTypeById_Denies_User()
        {
            var user = EnsureSeedUser(UserRole.User, "pt_byid_user", "pt_byid_user@test.com");
            AuthenticateAs(user, "User");
            var pt = CreateSeedPlantType("Daisy");

            var payload = new { query = $"{{ plantTypeById(id:{pt.Id}) {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPlantTypeById_Returns_Empty_When_Id_Not_Found()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_nf_admin", "pt_nf_admin@test.com");
            AuthenticateAs(admin, "Admin");

            var payload = new { query = "{ plantTypeById(id:99999) { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        // ---------------------------
        // Mutation Tests
        // ---------------------------
        [Fact]
        public async Task AddPlantType_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_add_admin", "pt_add_admin@test.com");
            AuthenticateAs(admin, "Admin");
            var etid = CreateEmotionTag("Excited", "#00FF00");

            var payload = new { query = $"mutation {{ addPlantType(name:\"Lily\", emotionTagId:{etid}) {{ id name emotionTagId }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Lily", json);
        }

        [Fact]
        public async Task AddPlantType_Denies_User()
        {
            var user = EnsureSeedUser(UserRole.User, "pt_add_user", "pt_add_user@test.com");
            AuthenticateAs(user, "User");
            var etid = CreateEmotionTag("Blocked", "#0000FF");

            var payload = new { query = $"mutation {{ addPlantType(name:\"Forbidden\", emotionTagId:{etid}) {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddPlantType_Returns_Error_When_EmotionTag_Invalid()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_add_invalid", "pt_add_invalid@test.com");
            AuthenticateAs(admin, "Admin");

            var payload = new { query = "mutation { addPlantType(name:\"BadPlant\", emotionTagId:99999) { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("error", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdatePlantType_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_update_admin", "pt_update_admin@test.com");
            AuthenticateAs(admin, "Admin");
            var pt = CreateSeedPlantType("Orchid");

            var payload = new { query = $"mutation {{ updatePlantType(id:{pt.Id}, name:\"UpdatedFlower\") {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("UpdatedFlower", json);
        }

        [Fact]
        public async Task UpdatePlantType_Denies_User()
        {
            var user = EnsureSeedUser(UserRole.User, "pt_update_user", "pt_update_user@test.com");
            AuthenticateAs(user, "User");
            var pt = CreateSeedPlantType("Peony");

            var payload = new { query = $"mutation {{ updatePlantType(id:{pt.Id}, name:\"Hacked\") {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdatePlantType_Returns_Null_When_Id_Not_Found()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_update_nf", "pt_update_nf@test.com");
            AuthenticateAs(admin, "Admin");

            var payload = new { query = "mutation { updatePlantType(id:99999, name:\"Ghost\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("null", json);
        }

        [Fact]
        public async Task DeletePlantType_Allows_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_delete_admin", "pt_delete_admin@test.com");
            AuthenticateAs(admin, "Admin");
            var pt = CreateSeedPlantType("ToDelete");

            var payload = new { query = $"mutation {{ deletePlantType(id:{pt.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeletePlantType_Denies_User()
        {
            var user = EnsureSeedUser(UserRole.User, "pt_delete_user", "pt_delete_user@test.com");
            AuthenticateAs(user, "User");
            var pt = CreateSeedPlantType("CantDelete");

            var payload = new { query = $"mutation {{ deletePlantType(id:{pt.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeletePlantType_Returns_False_When_Id_Not_Found()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "pt_delete_nf", "pt_delete_nf@test.com");
            AuthenticateAs(admin, "Admin");

            var payload = new { query = "mutation { deletePlantType(id:99999) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("false", json);
        }
    }
}
