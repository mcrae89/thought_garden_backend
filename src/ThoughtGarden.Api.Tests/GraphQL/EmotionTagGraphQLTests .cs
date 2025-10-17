using Microsoft.Extensions.DependencyInjection;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;
using Xunit;
using static ThoughtGarden.Api.Tests.Utils.GraphQLTestClient;

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

            var query = new { query = "{ emotions { id name color } }" };
            var resp = await PostAsUserAsync(_client, _factory, query, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("id", json);
            Assert.Contains("name", json);
        }

        [Fact]
        public async Task GetEmotionById_Returns_One()
        {
            var user = EnsureUser("query_emotion_byid", "query_emotion_byid@test.com");
            var etid = GetAnyEmotionTagId();

            var query = new { query = $"{{ emotionById(id:{etid}) {{ id name }} }}" };
            var resp = await PostAsUserAsync(_client, _factory, query, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("id", json);
            Assert.Contains("name", json);
        }

        [Fact]
        public async Task GetEmotionById_Returns_Null_For_InvalidId()
        {
            var user = EnsureUser("query_invalid", "query_invalid@test.com");

            var query = new { query = "{ emotionById(id:9999) { id name } }" };
            var resp = await PostAsUserAsync(_client, _factory, query, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("\"emotionById\":null", json);
        }

        // ---------------------------
        // Mutation Tests
        // ---------------------------

        [Fact]
        public async Task AddEmotionTag_Allows_Admin()
        {
            var admin = EnsureUser("admin_addtag", "admin_addtag@test.com", UserRole.Admin);

            var mutation = new { query = "mutation { addEmotionTag(name:\"Joy\", color:\"#ff0\", icon:\"smile\") { id name color icon } }" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Joy", json);
        }

        [Fact]
        public async Task AddEmotionTag_Fails_For_User()
        {
            var user = EnsureUser("user_addtag", "user_addtag@test.com", UserRole.User);

            var mutation = new { query = "mutation { addEmotionTag(name:\"Blocked\", color:\"#000\", icon:\"ban\") { id name } }" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddEmotionTag_Denies_Anonymous()
        {
            var mutation = new { query = "mutation { addEmotionTag(name:\"Anon\", color:\"#123\", icon:\"ghost\") { id name } }" };
            var resp = await PostGraphQLAsync(_client, mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateEmotionTag_Allows_Admin()
        {
            var admin = EnsureUser("admin_updatetag", "admin_updatetag@test.com", UserRole.Admin);
            var etid = GetAnyEmotionTagId();

            var mutation = new { query = $"mutation {{ updateEmotionTag(id:{etid}, name:\"Updated\", color:\"#abc\", icon:\"edit\") {{ id name color icon }} }}" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Updated", json);
        }

        [Fact]
        public async Task UpdateEmotionTag_Returns_Null_When_NotFound()
        {
            var admin = EnsureUser("admin_updatenull", "admin_updatenull@test.com", UserRole.Admin);

            var mutation = new { query = "mutation { updateEmotionTag(id:9999, name:\"Ghost\") { id name } }" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not found", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateEmotionTag_Fails_For_User()
        {
            var user = EnsureUser("user_updatetag", "user_updatetag@test.com", UserRole.User);
            var etid = GetAnyEmotionTagId();

            var mutation = new { query = $"mutation {{ updateEmotionTag(id:{etid}, name:\"Hacker\") {{ id name }} }}" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateEmotionTag_Denies_Anonymous()
        {
            var mutation = new { query = "mutation { updateEmotionTag(id:1, name:\"AnonUpdate\") { id name } }" };
            var resp = await PostGraphQLAsync(_client, mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteEmotionTag_Allows_Admin()
        {
            var admin = EnsureUser("admin_deletetag", "admin_deletetag@test.com", UserRole.Admin);

            var add = new { query = "mutation { addEmotionTag(name:\"TempDel\", color:\"#ccc\", icon:\"trash\") { id } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, admin, "Admin");
            addResp.EnsureSuccessStatusCode();
            var id = System.Text.Json.JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addEmotionTag").GetProperty("id").GetInt32();

            var del = new { query = $"mutation {{ deleteEmotionTag(id:{id}) }}" };
            var delResp = await PostAsUserAsync(_client, _factory, del, admin, "Admin");
            delResp.EnsureSuccessStatusCode();
            var json = await delResp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteEmotionTag_Returns_False_When_NotFound()
        {
            var admin = EnsureUser("admin_deletefail", "admin_deletefail@test.com", UserRole.Admin);

            var mutation = new { query = "mutation { deleteEmotionTag(id:9999) }" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not found", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteEmotionTag_Fails_For_User()
        {
            var user = EnsureUser("user_deletefail", "user_deletefail@test.com", UserRole.User);
            var etid = GetAnyEmotionTagId();

            var mutation = new { query = $"mutation {{ deleteEmotionTag(id:{etid}) }}" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteEmotionTag_Denies_Anonymous()
        {
            var mutation = new { query = "mutation { deleteEmotionTag(id:1) }" };
            var resp = await PostGraphQLAsync(_client, mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
