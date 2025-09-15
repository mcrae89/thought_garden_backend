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
    public class JournalEntryGraphQLTests : IClassFixture<PostgresFixture>
    {
        private readonly HttpClient _client;
        private readonly ApiFactory _factory;

        public JournalEntryGraphQLTests(PostgresFixture fixture)
        {
            _factory = new ApiFactory(fixture.ConnectionString);
            _client = _factory.CreateClient();
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private (int Id, string UserName, string Email) CreateAndAuthenticateTempUser(string userName, string email, string role = "User")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var planId = db.SubscriptionPlans.First().Id;
            var user = new User
            {
                UserName = userName,
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
        public async Task GetJournalEntries_Returns_EmptyList_For_NewUser()
        {
            CreateAndAuthenticateTempUser("nouser_entries", "nouser_entries@test.com");

            var query = new { query = "{ journalEntries { id text moodId } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("journalEntries", json);
            Assert.DoesNotContain("text", json);
        }

        [Fact]
        public async Task GetJournalEntries_Shows_Only_OwnEntries()
        {
            var userA = CreateAndAuthenticateTempUser("owner_entries", "owner_entries@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Mine\", moodId:1, secondaryEmotions:[]) { id } }" };
            await _client.PostAsJsonAsync("/graphql", add);

            CreateAndAuthenticateTempUser("other_entries", "other_entries@test.com");
            var query = new { query = "{ journalEntries { id text } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Mine", json);
        }

        [Fact]
        public async Task GetJournalEntries_Admin_Sees_AllEntries()
        {
            CreateAndAuthenticateTempUser("user_entries", "user_entries@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"UserEntry\", moodId:1, secondaryEmotions:[]) { id } }" };
            await _client.PostAsJsonAsync("/graphql", add);

            CreateAndAuthenticateTempUser("admin_entries", "admin_entries@test.com", role: "Admin");
            var query = new { query = "{ journalEntries { id text } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("UserEntry", json);
        }

        [Fact]
        public async Task GetJournalEntryById_Allows_Self()
        {
            var user = CreateAndAuthenticateTempUser("entry_self", "entry_self@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"SelfCanSee\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("SelfCanSee", json);
        }

        [Fact]
        public async Task GetJournalEntryById_Fails_For_NonOwner()
        {
            CreateAndAuthenticateTempUser("entry_owner", "entry_owner@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Private\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            CreateAndAuthenticateTempUser("other_user", "other_user@test.com");
            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("\"journalEntryById\":[]", json);
            Assert.DoesNotContain("Private", json);
        }

        [Fact]
        public async Task GetJournalEntryById_Admin_CanAccess()
        {
            CreateAndAuthenticateTempUser("entry_user", "entry_user@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"AdminCanSee\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            CreateAndAuthenticateTempUser("admin", "admin@test.com", role: "Admin");
            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("AdminCanSee", json);
        }

        // ---------------------------
        // Mutation Tests
        // ---------------------------

        [Fact]
        public async Task AddJournalEntry_With_SecondaryEmotions()
        {
            CreateAndAuthenticateTempUser("sec_emotions", "sec_emotions@test.com");
            var mutation = new
            {
                query = @"
                    mutation AddEntry($text:String!, $moodId:Int!, $secs:[SecondaryEmotionInput!]) {
                      addJournalEntry(text:$text, moodId:$moodId, secondaryEmotions:$secs) {
                        id text
                      }
                    }",
                variables = new
                {
                    text = "Mood check",
                    moodId = 1,
                    secs = new[] { new { emotionId = 1, intensity = 3 } }
                }
            };

            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Mood check", json);
        }

        [Fact]
        public async Task AddJournalEntry_Denies_Anonymous()
        {
            var payload = new { query = "mutation { addJournalEntry(text:\"Anon\", moodId:1, secondaryEmotions:[]) { id } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateJournalEntry_Updates_SecondaryEmotions()
        {
            CreateAndAuthenticateTempUser("update_sec", "update_sec@test.com");
            var add = new
            {
                query = @"
                    mutation AddEntry($text:String!, $moodId:Int!, $secs:[SecondaryEmotionInput!]) {
                      addJournalEntry(text:$text, moodId:$moodId, secondaryEmotions:$secs) { id }
                    }",
                variables = new
                {
                    text = "Temp",
                    moodId = 1,
                    secs = new[] { new { emotionId = 1, intensity = 2 } }
                }
            };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var update = new
            {
                query = @"
                    mutation UpdateEntry($id:Int!, $text:String!, $moodId:Int!, $secs:[SecondaryEmotionInput!]) {
                      updateJournalEntry(id:$id, text:$text, moodId:$moodId, secondaryEmotions:$secs) { id text }
                    }",
                variables = new
                {
                    id = entryId,
                    text = "Changed",
                    moodId = 2,
                    secs = new[] { new { emotionId = 2, intensity = 4 } }
                }
            };

            var updateResp = await _client.PostAsJsonAsync("/graphql", update);
            updateResp.EnsureSuccessStatusCode();
            var updateJson = await updateResp.Content.ReadAsStringAsync();

            Assert.Contains("Changed", updateJson);
        }

        [Fact]
        public async Task UpdateJournalEntry_Fails_If_NotFound()
        {
            CreateAndAuthenticateTempUser("noentry_user", "noentry_user@test.com");
            var update = new { query = "mutation { updateJournalEntry(id:9999, text:\"nope\", secondaryEmotions:[]) { id text } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", update);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateJournalEntry_Denies_Anonymous()
        {
            var payload = new { query = "mutation { updateJournalEntry(id:1, text:\"AnonUpdate\") { id text } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteJournalEntry_Allows_Owner()
        {
            var user = CreateAndAuthenticateTempUser("owner_delete", "owner_delete@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"DeleteMe\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var del = new { query = $"mutation {{ deleteJournalEntry(id:{entryId}) }}" };
            var delResp = await _client.PostAsJsonAsync("/graphql", del);
            delResp.EnsureSuccessStatusCode();
            var json = await delResp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteJournalEntry_Fails_For_NonOwner()
        {
            CreateAndAuthenticateTempUser("owner_del", "owner_del@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Delete target\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            CreateAndAuthenticateTempUser("intruder_del", "intruder_del@test.com");
            var del = new { query = $"mutation {{ deleteJournalEntry(id:{entryId}) }}" };
            var delResp = await _client.PostAsJsonAsync("/graphql", del);
            var json = await delResp.Content.ReadAsStringAsync();

            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteJournalEntry_Fails_If_NotFound()
        {
            CreateAndAuthenticateTempUser("delete_fail", "delete_fail@test.com");
            var mutation = new { query = "mutation { deleteJournalEntry(id:9999) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteJournalEntry_Denies_Anonymous()
        {
            var payload = new { query = "mutation { deleteJournalEntry(id:1) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
