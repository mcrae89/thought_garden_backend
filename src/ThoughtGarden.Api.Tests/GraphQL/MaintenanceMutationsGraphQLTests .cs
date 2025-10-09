using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.GraphQL.Mutations;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Tests.GraphQL
{
    public class MaintenanceMutationsGraphQLTests : IClassFixture<PostgresFixture>
    {
        private readonly ApiFactory _factoryDev;
        private readonly HttpClient _clientDev;

        public MaintenanceMutationsGraphQLTests(PostgresFixture fixture)
        {
            _factoryDev = new ApiFactory(fixture.ConnectionString);
            _clientDev = _factoryDev.CreateClient();
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private (int Id, string UserName, string Email) CreateAndAuthenticateUser(string userName, string email, string role = "Admin")
        {
            using var scope = _factoryDev.Services.CreateScope();
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

            var token = JwtTokenGenerator.GenerateToken(_factoryDev.JwtKey, "TestIssuer", "TestAudience",
                user.Id, user.UserName, user.Email, role);

            _clientDev.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (user.Id, user.UserName, user.Email);
        }

        private int SeedUser()
        {
            using var scope = _factoryDev.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var planId = db.SubscriptionPlans.First().Id;
            var user = new User
            {
                UserName = "seed_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Email = "seed@test.com",
                PasswordHash = PasswordHelper.HashPassword("P@ssw0rd!"),
                Role = UserRole.User,
                SubscriptionPlanId = planId
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user.Id;
        }

        private void InsertRow(int userId, string wrappedKeysJson, string nonce = "AAA=", string tag = "AAA=")
        {
            using var scope = _factoryDev.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            db.JournalEntries.Add(new JournalEntry
            {
                UserId = userId,
                Text = "cipher",
                DataNonce = nonce,
                DataTag = tag,
                WrappedKeys = wrappedKeysJson,
                AlgVersion = "gcm.v1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                MoodId = 1
            });
            db.SaveChanges();
        }

        // ---------------------------
        // Authorization (HTTP)
        // ---------------------------

        [Fact]
        public async Task RewrapAndPrunePrimary_Denies_Anonymous()
        {
            _clientDev.DefaultRequestHeaders.Authorization = null;
            var payload = new
            {
                query = @"mutation {
                  rewrapAndPrunePrimary(oldPrimaryId:""k_old"", newPrimaryId:""k_new"") {
                    updatedRows
                  }
                }"
            };
            var resp = await _clientDev.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ReencryptAfterCompromise_Denies_Anonymous()
        {
            _clientDev.DefaultRequestHeaders.Authorization = null;
            var payload = new { query = @"mutation { reencryptAfterCompromise(compromisedKeyId:""k_old"") }" };
            var resp = await _clientDev.PostAsJsonAsync("/graphql", payload);
            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Env guard (resolver-level; no second server needed)
        // ---------------------------

        [Fact]
        public async Task RewrapAndPrunePrimary_Throws_NotAuthorized_When_NotDev()
        {
            using var scope = _factoryDev.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<EnvelopeCrypto>();

            var resolver = new MaintenanceMutations();
            var prodHost = new TestHostEnv(Environments.Production);

            var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
                resolver.RewrapAndPrunePrimary(env.PrimaryKeyId, "k_new", db, env, prodHost, CancellationToken.None));

            Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);
        }


        [Fact]
        public async Task ReencryptAfterCompromise_Throws_NotAuthorized_When_NotDev()
        {
            // Use live services from the dev test server
            using var scope = _factoryDev.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<EnvelopeCrypto>();

            var resolver = new MaintenanceMutations();
            var prodHost = new TestHostEnv(Environments.Production); // pretend non-Dev

            var ex = await Assert.ThrowsAsync<GraphQLException>(() =>
                resolver.ReencryptAfterCompromise(env.PrimaryKeyId, db, env, prodHost, CancellationToken.None));

            Assert.Contains("not authorized", ex.Message, StringComparison.OrdinalIgnoreCase);
        }


        // ---------------------------
        // Happy paths (HTTP)
        // ---------------------------

        [Fact]
        public async Task RewrapAndPrunePrimary_HappyPath_Updates_And_Prunes()
        {
            // Admin auth
            CreateAndAuthenticateUser("admin_dev", "admin_dev@test.com", role: "Admin");

            // Get ids from live crypto
            string oldPrimaryId, recoveryId;
            using (var scope = _factoryDev.Services.CreateScope())
            {
                var env = scope.ServiceProvider.GetRequiredService<EnvelopeCrypto>();
                oldPrimaryId = env.PrimaryKeyId;
                recoveryId = env.RecoveryKeyId;
            }

            // Seed a REAL entry (envelope produced by the API so WrappedKeys is valid)
            var add = new { query = @"mutation { addJournalEntry(text:""RotateMe"", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _clientDev.PostAsJsonAsync("/graphql", add);
            var addBody = await addResp.Content.ReadAsStringAsync();
            Assert.True(addResp.IsSuccessStatusCode, $"addJournalEntry failed: {addBody}");
            Assert.Contains("\"id\":", addBody);

            // Run rewrap: move from current primary to recovery (acts as our “new” id)
            var payload = new
            {
                query = $@"mutation {{
          rewrapAndPrunePrimary(oldPrimaryId:""{oldPrimaryId}"", newPrimaryId:""{recoveryId}"") {{
            updatedRows addedNewPrimary addedRecovery prunedOldPrimary skippedUnwrapFailed skippedInvalidJson alreadyUpToDate
          }}
        }}"
            };
            var resp = await _clientDev.PostAsJsonAsync("/graphql", payload);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.IsSuccessStatusCode, $"rewrapAndPrunePrimary failed: {body}");
            Assert.DoesNotContain(@"""errors""", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(@"""updatedRows"":", body);
            Assert.Contains(@"""prunedOldPrimary"":", body);
        }

        [Fact]
        public async Task ReencryptAfterCompromise_HappyPath_Reencrypts()
        {
            CreateAndAuthenticateUser("admin_dev2", "admin_dev2@test.com", role: "Admin");

            string primaryId, recoveryId;
            using (var scope = _factoryDev.Services.CreateScope())
            {
                var env = scope.ServiceProvider.GetRequiredService<EnvelopeCrypto>();
                primaryId = env.PrimaryKeyId;
                recoveryId = env.RecoveryKeyId;
            }

            var add = new { query = @"mutation { addJournalEntry(text:""RotateMe"", moodId:1, secondaryEmotions:[]) { id } }" };
            var addResp = await _clientDev.PostAsJsonAsync("/graphql", add);
            var addBody = await addResp.Content.ReadAsStringAsync();
            Assert.True(addResp.IsSuccessStatusCode, $"addJournalEntry failed: {addBody}");

            // use active primary as compromised key (safe with the cursor patch)
            var run = new { query = $@"mutation {{ reencryptAfterCompromise(compromisedKeyId:""{primaryId}"") }}" };
            var runResp = await _clientDev.PostAsJsonAsync("/graphql", run);
            var runBody = await runResp.Content.ReadAsStringAsync();
            Assert.True(runResp.IsSuccessStatusCode, $"reencryptAfterCompromise failed: {runBody}");
            Assert.DoesNotContain(@"""errors""", runBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("true", runBody);

            using var scope2 = _factoryDev.Services.CreateScope();
            var db2 = scope2.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var row = await db2.JournalEntries.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();

            using var doc = System.Text.Json.JsonDocument.Parse(row.WrappedKeys!);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty(primaryId, out _));   // wrapped to active primary
            Assert.True(root.TryGetProperty(recoveryId, out _));  // and recovery
        }



        // ---------------------------
        // Negative cases on resolver (counters)
        // ---------------------------

        [Fact]
        public async Task RewrapAndPrunePrimary_Skips_InvalidJson_UnwrapFailed_AlreadyUpToDate()
        {
            using var scope = _factoryDev.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<EnvelopeCrypto>();
            var devHost = new TestHostEnv(Environments.Development);

            // Clean a small playground (optional if tests isolate DB)
            db.JournalEntries.RemoveRange(db.JournalEntries);
            await db.SaveChangesAsync();

            var oldId = env.PrimaryKeyId;
            var newId = env.RecoveryKeyId; // guaranteed to exist in KEK ring

            // 1) invalid JSON
            db.JournalEntries.Add(new JournalEntry { UserId = 1, Text = "c", DataNonce = "AAA=", DataTag = "AAA=", WrappedKeys = "not-json" });
            // 2) unwrap fail: only an unknown key
            db.JournalEntries.Add(new JournalEntry { UserId = 1, Text = "c", DataNonce = "AAA=", DataTag = "AAA=", WrappedKeys = "{\"unknown\":\"x\"}" });
            // 3) already up to date: has new, not old
            db.JournalEntries.Add(new JournalEntry { UserId = 1, Text = "c", DataNonce = "AAA=", DataTag = "AAA=", WrappedKeys = $"{{\"{newId}\":\"z\"}}" });
            await db.SaveChangesAsync();

            var result = await new MaintenanceMutations().RewrapAndPrunePrimary(oldId, newId, db, env, devHost, CancellationToken.None);

            Assert.Equal(0, result.UpdatedRows);
            Assert.Equal(1, result.SkippedInvalidJson);
            Assert.Equal(1, result.SkippedUnwrapFailed);
            Assert.Equal(1, result.AlreadyUpToDate);
        }


        // ---------------------------
        // Test helpers
        // ---------------------------

        private sealed class TestHostEnv : IHostEnvironment
        {
            public TestHostEnv(string name)
            {
                EnvironmentName = name;
                ApplicationName = "Tests";
                ContentRootPath = AppContext.BaseDirectory;
                ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath); // or new NullFileProvider()
            }

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }
        }

    }
}
