using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;
using Xunit;

namespace ThoughtGarden.Api.Tests.GraphQL
{
    public class EmotionTagGraphQLTests : IClassFixture<PostgresFixture>
    {
        private readonly HttpClient _client;
        private readonly ApiFactory _factory;

        public EmotionTagGraphQLTests(PostgresFixture fixture)
        {
            _factory = new ApiFactory(fixture.ConnectionString);
            _client = _factory.CreateClient();
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private (int Id, string UserName, string Email) CreateAndAuthenticateTempUser(string username, string email, string role = "User")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var planId = db.SubscriptionPlans.First().Id;
            var user = new User
            {
                UserName = username,
                Email = email,
                PasswordHash = PasswordHelper.HashPassword("P@ssw0rd!"),
                Role = role == "Admin" ? UserRole.Admin : UserRole.User,
                SubscriptionPlanId = planId
            };
            db.Users.Add(user);
            db.SaveChanges();

            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                user.Id, user.UserName, user.Email, role);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (user.Id, user.UserName, user.Email);
        }

        // ---------------------------
        // Query Tests
        // ---------------------------

        [Fact]
        public async Task GetEmotions_Returns_SeededTags()
        {
            CreateAndAuthenticateTempUser("query_emotions", "query_emotions@test.com");

            var query = new { query = "{ emotions { id name color } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("id", json);
            Assert.Contains("name", json);
        }

        [Fact]
        public async Task GetEmotionById_Returns_One()
        {
            CreateAndAuthenticateTempUser("query_emotion_byid", "query_emotion_byid@test.com");

            // Seeded DB has EmotionTags (Ids 1..4 from HasData)
            var query = new { query = "{ emotionById(id:1) { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("id", json);
            Assert.Contains("name", json);
        }

        [Fact]
        public async Task GetEmotionById_Returns_Empty_For_InvalidId()
        {
            CreateAndAuthenticateTempUser("query_invalid", "query_invalid@test.com");

            var query = new { query = "{ emotionById(id:9999) { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("\"emotionById\":[]", json);
        }

        // ---------------------------
        // Mutation Tests
        // ---------------------------

        [Fact]
        public async Task AddEmotionTag_Allows_Admin()
        {
            CreateAndAuthenticateTempUser("admin_addtag", "admin_addtag@test.com", role: "Admin");

            var mutation = new
            {
                query = "mutation { addEmotionTag(name:\"Joy\", color:\"#ff0\", icon:\"smile\") { id name color icon } }"
            };

            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Joy", json);
        }

        [Fact]
        public async Task AddEmotionTag_Fails_For_User()
        {
            CreateAndAuthenticateTempUser("user_addtag", "user_addtag@test.com", role: "User");

            var mutation = new { query = "mutation { addEmotionTag(name:\"Blocked\", color:\"#000\", icon:\"ban\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateEmotionTag_Allows_Admin()
        {
            CreateAndAuthenticateTempUser("admin_updatetag", "admin_updatetag@test.com", role: "Admin");

            var mutation = new
            {
                query = "mutation { updateEmotionTag(id:1, name:\"Updated\", color:\"#abc\", icon:\"edit\") { id name color icon } }"
            };

            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Updated", json);
        }

        [Fact]
        public async Task UpdateEmotionTag_Returns_Null_When_NotFound()
        {
            CreateAndAuthenticateTempUser("admin_updatenull", "admin_updatenull@test.com", role: "Admin");

            var mutation = new { query = "mutation { updateEmotionTag(id:9999, name:\"Ghost\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("\"updateEmotionTag\":null", json);
        }

        [Fact]
        public async Task UpdateEmotionTag_Fails_For_User()
        {
            CreateAndAuthenticateTempUser("user_updatetag", "user_updatetag@test.com", role: "User");

            var mutation = new { query = "mutation { updateEmotionTag(id:1, name:\"Hacker\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteEmotionTag_Allows_Admin()
        {
            CreateAndAuthenticateTempUser("admin_deletetag", "admin_deletetag@test.com", role: "Admin");

            // First add a tag
            var add = new { query = "mutation { addEmotionTag(name:\"TempDel\", color:\"#ccc\", icon:\"trash\") { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            addResp.EnsureSuccessStatusCode();
            var id = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addEmotionTag").GetProperty("id").GetInt32();

            // Delete it
            var del = new { query = $"mutation {{ deleteEmotionTag(id:{id}) }}" };
            var delResp = await _client.PostAsJsonAsync("/graphql", del);
            delResp.EnsureSuccessStatusCode();
            var json = await delResp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteEmotionTag_Returns_False_When_NotFound()
        {
            CreateAndAuthenticateTempUser("admin_deletefail", "admin_deletefail@test.com", role: "Admin");

            var mutation = new { query = "mutation { deleteEmotionTag(id:9999) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("false", json);
        }

        [Fact]
        public async Task DeleteEmotionTag_Fails_For_User()
        {
            CreateAndAuthenticateTempUser("user_deletefail", "user_deletefail@test.com", role: "User");

            var mutation = new { query = "mutation { deleteEmotionTag(id:1) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
