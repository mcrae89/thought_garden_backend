using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;
using Xunit;
using static ThoughtGarden.Api.Tests.Utils.GraphQLTestClient;

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
        private (int Id, string UserName, string Email) CreateUser(string userName, string email, string role = "User")
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
            return (user.Id, user.UserName, user.Email);
        }

        private static string? GetTextForId(string json, int id)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("journalEntries", out var arr)) return null;

            foreach (var it in arr.EnumerateArray())
            {
                if (it.TryGetProperty("id", out var idProp) && idProp.GetInt32() == id)
                {
                    return it.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
                }
            }
            return null;
        }

        // ---------------------------
        // Query Tests
        // ---------------------------
        [Fact]
        public async Task GetJournalEntries_Returns_EmptyList_For_NewUser()
        {
            var user = CreateUser("nouser_entries", "nouser_entries@test.com");

            var query = new { query = "{ journalEntries { id text moodId } }" };
            var resp = await PostAsUserAsync(_client, _factory, query, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("journalEntries", json);
            Assert.DoesNotContain("text\":\"", json);
        }

        [Fact]
        public async Task GetJournalEntries_Shows_Only_OwnEntries()
        {
            var userA = CreateUser("owner_entries", "owner_entries@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Mine\", moodId:1, secondaryEmotions:[]) { id } }" };
            await PostAsUserAsync(_client, _factory, add, userA, "User");

            var userB = CreateUser("other_entries", "other_entries@test.com");
            var query = new { query = "{ journalEntries { id text } }" };
            var resp = await PostAsUserAsync(_client, _factory, query, userB, "User");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.DoesNotContain("Mine", json);
        }

        [Fact]
        public async Task GetJournalEntries_Admin_Sees_AllEntries()
        {
            var user = CreateUser("user_entries", "user_entries@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"UserEntry\", moodId:1, secondaryEmotions:[]) { id } }" };
            await PostAsUserAsync(_client, _factory, add, user, "User");

            var admin = CreateUser("admin_entries", "admin_entries@test.com", role: "Admin");
            var query = new { query = "{ journalEntries { id text } }" };
            var resp = await PostAsUserAsync(_client, _factory, query, admin, "Admin");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.DoesNotContain("UserEntry", json);
            Assert.Contains("\"text\":\"[encrypted]\"", json);
            Assert.DoesNotContain("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetJournalEntries_Admin_Sees_Own_Decrypted_In_List()
        {
            var other = CreateUser("list_other_u", "list_other_u@test.com");
            var addOther = new { query = "mutation { addJournalEntry(text:\"OtherListText\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addOtherResp = await PostAsUserAsync(_client, _factory, addOther, other, "User");
            var otherId = JsonDocument.Parse(await addOtherResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var admin = CreateUser("list_admin_u", "list_admin_u@test.com", role: "Admin");
            var addAdmin = new { query = "mutation { addJournalEntry(text:\"AdminOwnListText\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addAdminResp = await PostAsUserAsync(_client, _factory, addAdmin, admin, "Admin");
            var adminOwnId = JsonDocument.Parse(await addAdminResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var query = new { query = "{ journalEntries { id text } }" };
            var resp = await PostAsUserAsync(_client, _factory, query, admin, "Admin");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Equal("AdminOwnListText", GetTextForId(json, adminOwnId));
            Assert.DoesNotContain("OtherListText", json);
        }

        [Fact]
        public async Task GetJournalEntries_Admin_Sees_Others_Masked_In_List()
        {
            var other = CreateUser("mask_other_u", "mask_other_u@test.com");
            var addOther = new { query = "mutation { addJournalEntry(text:\"OtherMaskedText\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addOtherResp = await PostAsUserAsync(_client, _factory, addOther, other, "User");
            var otherId = JsonDocument.Parse(await addOtherResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var admin = CreateUser("mask_admin_u", "mask_admin_u@test.com", role: "Admin");
            var list = new { query = "{ journalEntries { id text } }" };
            var resp = await PostAsUserAsync(_client, _factory, list, admin, "Admin");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Equal("[encrypted]", GetTextForId(json, otherId));
            Assert.DoesNotContain("OtherMaskedText", json);
        }

        [Fact]
        public async Task GetJournalEntryById_Allows_Self()
        {
            var user = CreateUser("entry_self", "entry_self@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"SelfCanSee\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, user, "User");
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }}}}" };
            var resp = await PostAsUserAsync(_client, _factory, query, user, "User");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("SelfCanSee", json);
        }

        [Fact]
        public async Task GetJournalEntryById_Fails_For_NonOwner()
        {
            var owner = CreateUser("entry_owner", "entry_owner@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Private\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, owner, "User");
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var other = CreateUser("other_user", "other_user@test.com");
            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }}}}" };
            var resp = await PostAsUserAsync(_client, _factory, query, other, "User");
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Private", json);
        }

        [Fact]
        public async Task GetJournalEntryById_Admin_CanAccess()
        {
            var user = CreateUser("entry_user", "entry_user@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"AdminCanSee\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, user, "User");
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var admin = CreateUser("admin", "admin@test.com", role: "Admin");
            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }}}}" };
            var resp = await PostAsUserAsync(_client, _factory, query, admin, "Admin");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.DoesNotContain("AdminCanSee", json);
            Assert.Contains("\"text\":\"[encrypted]\"", json);
            Assert.DoesNotContain("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Anonymous access tests
        // ---------------------------
        [Fact]
        public async Task GetJournalEntries_Denies_Anonymous()
        {
            var payload = new { query = "{ journalEntries { id text } }" };
            var resp = await PostGraphQLAsync(_client, payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetJournalEntryById_Denies_Anonymous()
        {
            var u = CreateUser("anon_seed_u", "anon_seed_u@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"SeedAnon\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, u, "User");
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }}}}" };
            var resp = await PostGraphQLAsync(_client, query);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Mutation Tests
        // ---------------------------
        [Fact]
        public async Task AddJournalEntry_With_SecondaryEmotions()
        {
            var u = CreateUser("sec_emotions", "sec_emotions@test.com");
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

            var resp = await PostAsUserAsync(_client, _factory, mutation, u, "User");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Mood check", json);
        }

        [Fact]
        public async Task AddJournalEntry_Denies_Anonymous()
        {
            var payload = new { query = "mutation { addJournalEntry(text:\"Anon\", moodId:1, secondaryEmotions:[]) { id } }" };
            var resp = await PostGraphQLAsync(_client, payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateJournalEntry_Updates_SecondaryEmotions()
        {
            var user = CreateUser("update_sec", "update_sec@test.com");
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
            var addResp = await PostAsUserAsync(_client, _factory, add, user, "User");
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

            var updateResp = await PostAsUserAsync(_client, _factory, update, user, "User");
            updateResp.EnsureSuccessStatusCode();
            var updateJson = await updateResp.Content.ReadAsStringAsync();

            Assert.Contains("Changed", updateJson);
        }

        [Fact]
        public async Task UpdateJournalEntry_Fails_If_NotFound()
        {
            var user = CreateUser("noentry_user", "noentry_user@test.com");

            var update = new { query = "mutation { updateJournalEntry(id:9999, text:\"nope\", secondaryEmotions:[]) { id text } }" };
            var resp = await PostAsUserAsync(_client, _factory, update, user, "User");
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Entry not found", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateJournalEntry_Denies_Anonymous()
        {
            var payload = new { query = "mutation { updateJournalEntry(id:1, text:\"AnonUpdate\") { id text } }" };
            var resp = await PostGraphQLAsync(_client, payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteJournalEntry_Allows_Owner()
        {
            var user = CreateUser("owner_delete", "owner_delete@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"DeleteMe\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, user, "User");
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var del = new { query = $"mutation {{ deleteJournalEntry(id:{entryId}) }}" };
            var delResp = await PostAsUserAsync(_client, _factory, del, user, "User");
            delResp.EnsureSuccessStatusCode();
            var json = await delResp.Content.ReadAsStringAsync();

            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteJournalEntry_Fails_For_NonOwner()
        {
            var owner = CreateUser("owner_del", "owner_del@test.com");
            var add = new { query = "mutation { addJournalEntry(text:\"Delete target\", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, owner, "User");
            var entryId = JsonDocument.Parse(await addResp.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            var intruder = CreateUser("intruder_del", "intruder_del@test.com");
            var del = new { query = $"mutation {{ deleteJournalEntry(id:{entryId}) }}" };
            var delResp = await PostAsUserAsync(_client, _factory, del, intruder, "User");
            var json = await delResp.Content.ReadAsStringAsync();

            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteJournalEntry_Fails_If_NotFound()
        {
            var user = CreateUser("delete_fail", "delete_fail@test.com");

            var mutation = new { query = "mutation { deleteJournalEntry(id:9999) }" };
            var resp = await PostAsUserAsync(_client, _factory, mutation, user, "User");
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Entry not found", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteJournalEntry_Denies_Anonymous()
        {
            var payload = new { query = "mutation { deleteJournalEntry(id:1) }" };
            var resp = await PostGraphQLAsync(_client, payload);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Encryption Tests
        // ---------------------------
        [Fact]
        public async Task JournalEntry_IsEncryptedInDatabase_ButDecryptedInGraphQL()
        {
            var user = CreateUser("enc_test", "enc_test@test.com");

            var add = new { query = "mutation { addJournalEntry(text:\"SensitiveNote\", moodId:1, secondaryEmotions:[]) { id text } }" };
            var addResp = await PostAsUserAsync(_client, _factory, add, user, "User");
            addResp.EnsureSuccessStatusCode();
            var addJson = await addResp.Content.ReadAsStringAsync();

            Assert.Contains("SensitiveNote", addJson);

            var entryId = JsonDocument.Parse(addJson)
                .RootElement.GetProperty("data").GetProperty("addJournalEntry").GetProperty("id").GetInt32();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
                var dbEntry = db.JournalEntries.First(e => e.Id == entryId);

                Assert.NotEqual("SensitiveNote", dbEntry.Text);
                Assert.DoesNotContain("SensitiveNote", dbEntry.Text);
                Assert.False(string.IsNullOrWhiteSpace(dbEntry.DataNonce));
                Assert.False(string.IsNullOrWhiteSpace(dbEntry.DataTag));
                Assert.False(string.IsNullOrWhiteSpace(dbEntry.WrappedKeys));
            }

            var query = new { query = $"{{ journalEntryById(id:{entryId}) {{ id text }}}}" };
            var resp = await PostAsUserAsync(_client, _factory, query, user, "User");
            resp.EnsureSuccessStatusCode();
            var queryJson = await resp.Content.ReadAsStringAsync();

            Assert.Contains("SensitiveNote", queryJson);
        }

        [Fact]
        public async Task JournalEntry_WithoutEnvelope_FailsGracefully()
        {
            var user = CreateUser("bad_env_user", "bad_env_user@test.com");
            int badEntryId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
                var entry = new JournalEntry
                {
                    UserId = user.Id,
                    Text = "CorruptedCiphertext",
                    DataNonce = "AAA=",
                    DataTag = "AAA=",
                    WrappedKeys = "{}",   // forces DecryptionFailedException
                    MoodId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                db.JournalEntries.Add(entry);
                db.SaveChanges();
                badEntryId = entry.Id;
            }

            var query = new { query = $"{{ journalEntryById(id:{badEntryId}) {{ id text }}}}" };
            var resp = await PostAsUserAsync(_client, _factory, query, user, "User");
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("errors", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Unable to decrypt journal entry.", json);
        }
    }
}
