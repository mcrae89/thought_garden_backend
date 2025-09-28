using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;

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
        private (int Id, string UserName, string Email) EnsureUser(string userName, string email, UserRole role = UserRole.User)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var u = db.Users.FirstOrDefault(x => x.UserName == userName);
            if (u == null)
            {
                var planId = db.SubscriptionPlans.First().Id;
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

        private int GetAnyEmotionTagId()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            return db.EmotionTags.Select(e => e.Id).First();
        }

        // ---------------------------
        // Query Tests
        // ---------------------------

        [Fact]
        public async Task GetEmotions_Returns_SeededTags()
        {
            var user = EnsureUser("query_emotions", "query_emotions@test.com");
            Authenticate(user, "User");

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
            var user = EnsureUser("query_emotion_byid", "query_emotion_byid@test.com");
            Authenticate(user, "User");
            var etid = GetAnyEmotionTagId();

            var query = new { query = $"{{ emotionById(id:{etid}) {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("id", json);
            Assert.Contains("name", json);
        }

        [Fact]
        public async Task GetEmotionById_Returns_Empty_For_InvalidId()
        {
            var user = EnsureUser("query_invalid", "query_invalid@test.com");
            Authenticate(user, "User");

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
            var admin = EnsureUser("admin_addtag", "admin_addtag@test.com", UserRole.Admin);
            Authenticate(admin, "Admin");

            var mutation = new { query = "mutation { addEmotionTag(name:\"Joy\", color:\"#ff0\", icon:\"smile\") { id name color icon } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Joy", json);
        }

        [Fact]
        public async Task AddEmotionTag_Fails_For_User()
        {
            var user = EnsureUser("user_addtag", "user_addtag@test.com", UserRole.User);
            Authenticate(user, "User");

            var mutation = new { query = "mutation { addEmotionTag(name:\"Blocked\", color:\"#000\", icon:\"ban\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddEmotionTag_Denies_Anonymous()
        {
            var mutation = new { query = "mutation { addEmotionTag(name:\"Anon\", color:\"#123\", icon:\"ghost\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateEmotionTag_Allows_Admin()
        {
            var admin = EnsureUser("admin_updatetag", "admin_updatetag@test.com", UserRole.Admin);
            Authenticate(admin, "Admin");
            var etid = GetAnyEmotionTagId();

            var mutation = new { query = $"mutation {{ updateEmotionTag(id:{etid}, name:\"Updated\", color:\"#abc\", icon:\"edit\") {{ id name color icon }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Updated", json);
        }

        [Fact]
        public async Task UpdateEmotionTag_Returns_Null_When_NotFound()
        {
            var admin = EnsureUser("admin_updatenull", "admin_updatenull@test.com", UserRole.Admin);
            Authenticate(admin, "Admin");

            var mutation = new { query = "mutation { updateEmotionTag(id:9999, name:\"Ghost\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("\"updateEmotionTag\":null", json);
        }

        [Fact]
        public async Task UpdateEmotionTag_Fails_For_User()
        {
            var user = EnsureUser("user_updatetag", "user_updatetag@test.com", UserRole.User);
            Authenticate(user, "User");
            var etid = GetAnyEmotionTagId();

            var mutation = new { query = $"mutation {{ updateEmotionTag(id:{etid}, name:\"Hacker\") {{ id name }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateEmotionTag_Denies_Anonymous()
        {
            var mutation = new { query = "mutation { updateEmotionTag(id:1, name:\"AnonUpdate\") { id name } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteEmotionTag_Allows_Admin()
        {
            var admin = EnsureUser("admin_deletetag", "admin_deletetag@test.com", UserRole.Admin);
            Authenticate(admin, "Admin");

            var add = new { query = "mutation { addEmotionTag(name:\"TempDel\", color:\"#ccc\", icon:\"trash\") { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            addResp.EnsureSuccessStatusCode();
            var id = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addEmotionTag").GetProperty("id").GetInt32();

            var del = new { query = $"mutation {{ deleteEmotionTag(id:{id}) }}" };
            var delResp = await _client.PostAsJsonAsync("/graphql", del);
            delResp.EnsureSuccessStatusCode();
            var json = await delResp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteEmotionTag_Returns_False_When_NotFound()
        {
            var admin = EnsureUser("admin_deletefail", "admin_deletefail@test.com", UserRole.Admin);
            Authenticate(admin, "Admin");

            var mutation = new { query = "mutation { deleteEmotionTag(id:9999) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("false", json);
        }

        [Fact]
        public async Task DeleteEmotionTag_Fails_For_User()
        {
            var user = EnsureUser("user_deletefail", "user_deletefail@test.com", UserRole.User);
            Authenticate(user, "User");
            var etid = GetAnyEmotionTagId();

            var mutation = new { query = $"mutation {{ deleteEmotionTag(id:{etid}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteEmotionTag_Denies_Anonymous()
        {
            var mutation = new { query = "mutation { deleteEmotionTag(id:1) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
