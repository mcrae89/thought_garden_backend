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
        private (int Id, string UserName, string Email) CreateAndAuthenticateUser(string userName, string email, string role = "User")
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
            CreateAndAuthenticateUser("nouser_entries", "nouser_entries@test.com");

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
            var userA = CreateAndAuthenticateUser("owner_entries", "owner_entries@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Mine\", moodId:1, secondaryEmotions:[]) { id } }" };
            await _client.PostAsJsonAsync("/graphql", add);

            CreateAndAuthenticateUser("other_entries", "other_entries@test.com");
            var query = new { query = "{ journalEntries { id text } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Mine", json);
        }

        [Fact]
        public async Task GetJournalEntries_Admin_Sees_AllEntries()
        {
            CreateAndAuthenticateUser("user_entries", "user_entries@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"UserEntry\", moodId:1, secondaryEmotions:[]) { id } }" };
            await _client.PostAsJsonAsync("/graphql", add);

            CreateAndAuthenticateUser("admin_entries", "admin_entries@test.com", role: "Admin");
            var query = new { query = "{ journalEntries { id text } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("UserEntry", json);
        }

        [Fact]
        public async Task GetJournalEntryById_Allows_Self()
        {
            var user = CreateAndAuthenticateUser("entry_self", "entry_self@test.com");
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
            CreateAndAuthenticateUser("entry_owner", "entry_owner@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Private\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            CreateAndAuthenticateUser("other_user", "other_user@test.com");
            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Private", json);
        }



        [Fact]
        public async Task GetJournalEntryById_Admin_CanAccess()
        {
            CreateAndAuthenticateUser("entry_user", "entry_user@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"AdminCanSee\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            CreateAndAuthenticateUser("admin", "admin@test.com", role: "Admin");
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
            CreateAndAuthenticateUser("sec_emotions", "sec_emotions@test.com");
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

            // Expect plaintext in GraphQL response
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
            CreateAndAuthenticateUser("update_sec", "update_sec@test.com");
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
            CreateAndAuthenticateUser("noentry_user", "noentry_user@test.com");

            var update = new { query = "mutation { updateJournalEntry(id:9999, text:\"nope\", secondaryEmotions:[]) { id text } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", update);
            var json = await resp.Content.ReadAsStringAsync();

            // 🔧 Fix: match resolver behavior ("Entry not found")
            Assert.Contains("Entry not found", json, StringComparison.OrdinalIgnoreCase);
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
            var user = CreateAndAuthenticateUser("owner_delete", "owner_delete@test.com");
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
            CreateAndAuthenticateUser("owner_del", "owner_del@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Delete target\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            CreateAndAuthenticateUser("intruder_del", "intruder_del@test.com");
            var del = new { query = $"mutation {{ deleteJournalEntry(id:{entryId}) }}" };
            var delResp = await _client.PostAsJsonAsync("/graphql", del);
            var json = await delResp.Content.ReadAsStringAsync();

            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteJournalEntry_Fails_If_NotFound()
        {
            CreateAndAuthenticateUser("delete_fail", "delete_fail@test.com");

            var mutation = new { query = "mutation { deleteJournalEntry(id:9999) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", mutation);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Entry not found", json, StringComparison.OrdinalIgnoreCase);
        }


        [Fact]
        public async Task DeleteJournalEntry_Denies_Anonymous()
        {
            var payload = new { query = "mutation { deleteJournalEntry(id:1) }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Encryption Tests
        // ---------------------------
        [Fact]
        public async Task JournalEntry_IsEncryptedInDatabase_ButDecryptedInGraphQL()
        {
            var user = CreateAndAuthenticateUser("enc_test", "enc_test@test.com");

            // Add entry
            var add = new { query = "mutation { addJournalEntry(text:\"SensitiveNote\", moodId:1, secondaryEmotions:[]) { id text } }" };
            var addResp = await _client.PostAsJsonAsync("/graphql", add);
            addResp.EnsureSuccessStatusCode();
            var addJson = await addResp.Content.ReadAsStringAsync();

            // GraphQL shows plaintext
            Assert.Contains("SensitiveNote", addJson);

            var entryId = JsonDocument.Parse(addJson)
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            // DB contains ciphertext
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
                var dbEntry = db.JournalEntries.First(e => e.Id == entryId);

                Assert.NotEqual("SensitiveNote", dbEntry.Text);
                Assert.DoesNotContain("SensitiveNote", dbEntry.Text);
                Assert.False(string.IsNullOrWhiteSpace(dbEntry.IV));
            }

            // GraphQL decrypts again
            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            resp.EnsureSuccessStatusCode();
            var queryJson = await resp.Content.ReadAsStringAsync();

            Assert.Contains("SensitiveNote", queryJson);
        }

        [Fact]
        public async Task JournalEntry_WithoutIV_FailsGracefully()
        {
            var user = CreateAndAuthenticateUser("bad_iv_user", "bad_iv_user@test.com");
            int badEntryId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
                var entry = new JournalEntry
                {
                    UserId = user.Id,
                    Text = "CorruptedCiphertext",
                    IV = "", // missing IV
                    MoodId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                db.JournalEntries.Add(entry);
                db.SaveChanges();
                badEntryId = entry.Id;
            }

            var query = new { query = $"{{ journalEntryById(id:{badEntryId}) {{ id text }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", query);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("errors", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Unable to decrypt journal entry.", json);
        }
    }
}
