using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;
using Xunit;
using static ThoughtGarden.Api.Tests.Utils.GraphQLTestClient;

namespace ThoughtGarden.Api.Tests.GraphQL
{
    public class UserGraphQLTests : IClassFixture<PostgresFixture>
    {
        private readonly HttpClient _client;
        private readonly ApiFactory _factory;

        public UserGraphQLTests(PostgresFixture fixture)
        {
            _factory = new ApiFactory(fixture.ConnectionString);
            _client = _factory.CreateClient();
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private (int Id, string UserName, string Email) EnsureSeedUser(
            UserRole role = UserRole.User,
            string userName = "seeduser",
            string email = "seed@test.com")
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

        private (int Id, string UserName, string Email) CreateTempUser(string userName, string email, string? plainPassword = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();

            var planId = db.SubscriptionPlans.Select(p => p.Id).FirstOrDefault();
            if (planId == 0) throw new InvalidOperationException("No subscription plan found for temp user.");

            var user = new User
            {
                UserName = userName,
                Email = email,
                PasswordHash = plainPassword is null ? "x" : PasswordHelper.HashPassword(plainPassword),
                Role = UserRole.User,
                SubscriptionPlanId = planId
            };

            db.Users.Add(user);
            db.SaveChanges();

            return (user.Id, user.UserName, user.Email);
        }

        // ---------------------------
        // Query Tests
        // ---------------------------
        [Fact]
        public async Task GetProfile_Returns_SeededUser_As_User()
        {
            var user = EnsureSeedUser();
            var resp = await PostAsUserAsync(_client, _factory, new { query = "{ profile { userName email } }" }, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("seeduser", json);
            Assert.Contains("seed@test.com", json);
        }

        [Fact]
        public async Task GetProfile_Fails_When_Not_Authenticated()
        {
            var resp = await PostGraphQLAsync(_client, new { query = "{ profile { userName email } }" });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUsers_Returns_SeededUser_As_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "admin_users", "admin_users@test.com");
            var resp = await PostAsUserAsync(_client, _factory, new { query = "{ users { userName email } }" }, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("seeduser", json);
            Assert.Contains("seed@test.com", json);
        }

        [Fact]
        public async Task GetUsers_Fails_For_Normal_User()
        {
            var temp = CreateTempUser("normal_for_getusers", "normal_for_getusers@test.com");
            var resp = await PostAsUserAsync(_client, _factory, new { query = "{ users { userName email } }" }, temp, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUsers_Denies_Anonymous()
        {
            var resp = await PostGraphQLAsync(_client, new { query = "{ users { userName email } }" });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUserById_Returns_SeededUser_As_Admin()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "admin_userbyid", "admin_userbyid@test.com");
            var seed = EnsureSeedUser();

            var resp = await PostAsUserAsync(_client, _factory, new { query = $"{{ userById(id: {seed.Id}) {{ userName email }} }}" }, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("seeduser", json);
            Assert.Contains("seed@test.com", json);
        }

        [Fact]
        public async Task GetUserById_Fails_For_Normal_User()
        {
            var normal = CreateTempUser("normal_for_getuserbyid", "normal_for_getuserbyid@test.com");
            var victim = CreateTempUser("victim_for_getuserbyid", "victim_for_getuserbyid@test.com");

            var resp = await PostAsUserAsync(_client, _factory, new { query = $"{{ userById(id: {victim.Id}) {{ userName email }} }}" }, normal, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUserById_Denies_Anonymous()
        {
            var resp = await PostGraphQLAsync(_client, new { query = "{ userById(id: 1) { userName email } }" });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Mutation Tests - Update
        // ---------------------------
        [Fact]
        public async Task UpdateUser_Allows_Self_Update()
        {
            var user = EnsureSeedUser();
            var resp = await PostAsUserAsync(_client, _factory, new { query = $"mutation {{ updateUser(id: {user.Id}, userName: \"updatedName\") {{ userName email }} }}" }, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("updatedName", json);
        }

        [Fact]
        public async Task UpdateUser_Allows_Admin_Update_Other_User()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "admin_update", "admin_update@test.com");
            var other = CreateTempUser("otheruser_update", "other_update@test.com");

            var resp = await PostAsUserAsync(_client, _factory, new { query = $"mutation {{ updateUser(id: {other.Id}, userName: \"adminUpdated\") {{ userName email }} }}" }, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("adminUpdated", json);
        }

        [Fact]
        public async Task UpdateUser_Fails_For_Normal_User_Updating_Other()
        {
            var user = EnsureSeedUser(UserRole.User, "user_updatefail", "user_updatefail@test.com");
            var other = CreateTempUser("otheruser_block", "other_block@test.com");

            var resp = await PostAsUserAsync(_client, _factory, new { query = $"mutation {{ updateUser(id: {other.Id}, userName: \"hacker\") {{ userName email }} }}" }, user, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateUser_Denies_Anonymous()
        {
            var resp = await PostGraphQLAsync(_client, new { query = "mutation { updateUser(id:1, userName:\"Anon\") { id userName } }" });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Mutation Tests - Delete
        // ---------------------------
        [Fact]
        public async Task DeleteUser_Allows_Self_Delete()
        {
            var temp = CreateTempUser("selfdelete", "selfdelete@test.com");

            var resp = await PostAsUserAsync(_client, _factory, new { query = $"mutation {{ deleteUser(id: {temp.Id}) }}" }, temp, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteUser_Allows_Admin_Delete_Other()
        {
            var admin = EnsureSeedUser(UserRole.Admin, "admin_delete", "admin_delete@test.com");
            var other = CreateTempUser("admindelete", "admindelete@test.com");

            var resp = await PostAsUserAsync(_client, _factory, new { query = $"mutation {{ deleteUser(id: {other.Id}) }}" }, admin, "Admin");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteUser_Fails_For_Normal_User_Deleting_Other()
        {
            var normal = CreateTempUser("normal_for_delete", "normal_for_delete@test.com");
            var victim = CreateTempUser("victim_delete", "victim_delete@test.com");

            var resp = await PostAsUserAsync(_client, _factory, new { query = $"mutation {{ deleteUser(id: {victim.Id}) }}" }, normal, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteUser_Denies_Anonymous()
        {
            var resp = await PostGraphQLAsync(_client, new { query = "mutation { deleteUser(id:1) }" });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Mutation Tests - Password
        // ---------------------------
        [Fact]
        public async Task UpdatePassword_Succeeds_For_Self_With_Correct_Current()
        {
            var temp = CreateTempUser("pw_ok", "pw_ok@test.com", plainPassword: "OldPass123!");
            var resp = await PostAsUserAsync(_client, _factory, new { query = "mutation { updatePassword(currentPassword:\"OldPass123!\", newPassword:\"NewPass456!\") }" }, temp, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task UpdatePassword_Fails_With_Wrong_Current()
        {
            var temp = CreateTempUser("pw_bad", "pw_bad@test.com", plainPassword: "OldPass123!");
            var resp = await PostAsUserAsync(_client, _factory, new { query = "mutation { updatePassword(currentPassword:\"WRONG!\", newPassword:\"NewPass456!\") }" }, temp, "User");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Current password is incorrect", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdatePassword_Denies_Anonymous()
        {
            var resp = await PostGraphQLAsync(_client, new { query = "mutation { updatePassword(currentPassword:\"x\", newPassword:\"y\") }" });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------
        // Mutation Tests - Auth
        // ---------------------------
        [Fact]
        public async Task RegisterUser_Succeeds_And_Can_Login()
        {
            var userName = $"reg_{Guid.NewGuid():N}".Substring(0, 8);
            var email = $"{userName}@test.com";

            var register = new { query = $"mutation {{ registerUser(username:\"{userName}\", email:\"{email}\", password:\"P@ssw0rd!\") {{ id userName email }} }}" };
            var regResp = await PostGraphQLAsync(_client, register);
            regResp.EnsureSuccessStatusCode();

            var regJson = await regResp.Content.ReadAsStringAsync();
            Assert.Contains(userName, regJson);
            Assert.Contains(email, regJson);

            var login = new { query = $"mutation {{ loginUser(email:\"{email}\", password:\"P@ssw0rd!\") {{ accessToken refreshToken }} }}" };
            var loginResp = await PostGraphQLAsync(_client, login);
            loginResp.EnsureSuccessStatusCode();

            var loginJson = await loginResp.Content.ReadAsStringAsync();
            Assert.Contains("accessToken", loginJson);
            Assert.Contains("refreshToken", loginJson);
        }

        [Fact]
        public async Task LoginUser_Fails_With_Wrong_Password()
        {
            var u = CreateTempUser("login_bad", "login_bad@test.com", plainPassword: "RightPass1!");

            var login = new { query = $"mutation {{ loginUser(email:\"{u.Email}\", password:\"WrongPass!\") {{ accessToken refreshToken }} }}" };
            var resp = await PostGraphQLAsync(_client, login);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Invalid password", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RefreshToken_Succeeds_With_Valid_Refresh()
        {
            var u = CreateTempUser("refresh_me", "refresh_me@test.com", plainPassword: "P@ssw0rd!");

            var login = new { query = $"mutation {{ loginUser(email:\"{u.Email}\", password:\"P@ssw0rd!\") {{ accessToken refreshToken }} }}" };
            var loginResp = await PostGraphQLAsync(_client, login);
            loginResp.EnsureSuccessStatusCode();

            var loginJson = await loginResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(loginJson);
            var data = doc.RootElement.GetProperty("data").GetProperty("loginUser");
            var accessToken = data.GetProperty("accessToken").GetString();
            var refreshToken = data.GetProperty("refreshToken").GetString();

            var refresh = new { query = $"mutation {{ refreshToken(refreshToken:\"{refreshToken}\") {{ accessToken refreshToken }} }}" };
            var refreshResp = await PostGraphQLAsync(_client, refresh, bearer: accessToken); // bearer now optional
            refreshResp.EnsureSuccessStatusCode();

            var refreshJson = await refreshResp.Content.ReadAsStringAsync();
            Assert.Contains("accessToken", refreshJson);
            Assert.Contains("refreshToken", refreshJson);
        }

        [Fact]
        public async Task RefreshToken_Fails_With_Invalid_Token()
        {
            var refresh = new { query = "mutation { refreshToken(refreshToken:\"bogus\") { accessToken refreshToken } }" };

            var resp = await PostGraphQLAsync(_client, refresh);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Invalid or expired", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LogoutUser_Succeeds_With_Active_RefreshToken()
        {
            var u = CreateTempUser("logout_me", "logout_me@test.com", plainPassword: "P@ssw0rd!");

            var login = new { query = $"mutation {{ loginUser(email:\"{u.Email}\", password:\"P@ssw0rd!\") {{ accessToken refreshToken }} }}" };
            var loginResp = await PostGraphQLAsync(_client, login);
            loginResp.EnsureSuccessStatusCode();

            var loginJson = await loginResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(loginJson);
            var data = doc.RootElement.GetProperty("data").GetProperty("loginUser");
            var refreshToken = data.GetProperty("refreshToken").GetString();

            var logout = new { query = $"mutation {{ logoutUser(refreshToken:\"{refreshToken}\") }}" };
            var resp = await PostGraphQLAsync(_client, logout);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }
    }
}
