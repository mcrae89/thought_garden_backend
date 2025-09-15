using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;

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
        private void AuthenticateAsAdmin()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var u = db.Users.First(x => x.UserName == "seeduser");
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                u.Id, u.UserName, u.Email, role: "Admin");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private void AuthenticateAsUser()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var u = db.Users.First(x => x.UserName == "seeduser");
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                u.Id, u.UserName, u.Email, role: "User");
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
        // Query tests
        // ---------------------------

        [Fact]
        public async Task GetPlantTypes_Allows_Admin()
        {
            AuthenticateAsAdmin();
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
            AuthenticateAsUser();
            var payload = new { query = "{ plantTypes { id name } }" };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPlantTypes_Returns_Empty_When_No_Records()
        {
            AuthenticateAsAdmin();

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
            AuthenticateAsAdmin();
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
            AuthenticateAsUser();
            var pt = CreateSeedPlantType("Daisy");
            var payload = new { query = $"{{ plantTypeById(id:{pt.Id}) {{ id name }} }}" };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPlantTypeById_Returns_Empty_When_Id_Not_Found()
        {
            AuthenticateAsAdmin();
            var payload = new { query = "{ plantTypeById(id:99999) { id name } }" };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("[]", json);
        }

        // ---------------------------
        // Mutation tests
        // ---------------------------

        // --- Add ---
        [Fact]
        public async Task AddPlantType_Allows_Admin()
        {
            AuthenticateAsAdmin();
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
            AuthenticateAsUser();
            var etid = CreateEmotionTag("Blocked", "#0000FF");

            var payload = new { query = $"mutation {{ addPlantType(name:\"Forbidden\", emotionTagId:{etid}) {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddPlantType_Returns_Error_When_EmotionTag_Invalid()
        {
            AuthenticateAsAdmin();
            var payload = new { query = "mutation { addPlantType(name:\"BadPlant\", emotionTagId:99999) { id name } }" };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("error", json, StringComparison.OrdinalIgnoreCase);
        }

        // --- Update ---
        [Fact]
        public async Task UpdatePlantType_Allows_Admin()
        {
            AuthenticateAsAdmin();
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
            AuthenticateAsUser();
            var pt = CreateSeedPlantType("Peony");

            var payload = new { query = $"mutation {{ updatePlantType(id:{pt.Id}, name:\"Hacked\") {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdatePlantType_Returns_Null_When_Id_Not_Found()
        {
            AuthenticateAsAdmin();
            var payload = new { query = "mutation { updatePlantType(id:99999, name:\"Ghost\") { id name } }" };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("null", json);
        }

        // --- Delete ---
        [Fact]
        public async Task DeletePlantType_Allows_Admin()
        {
            AuthenticateAsAdmin();
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
            AuthenticateAsUser();
            var pt = CreateSeedPlantType("CantDelete");

            var payload = new { query = $"mutation {{ deletePlantType(id:{pt.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeletePlantType_Returns_False_When_Id_Not_Found()
        {
            AuthenticateAsAdmin();
            var payload = new { query = "mutation { deletePlantType(id:99999) }" };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("false", json);
        }
    }
}
