using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
using ThoughtGarden.Models;
using Xunit;

namespace ThoughtGarden.Api.Tests.GraphQL
{
    public class UserGraphQLTests : IClassFixture<ApiFactory>
    {
        private readonly HttpClient _client;
        private readonly ApiFactory _factory;

        public UserGraphQLTests(ApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // ---------------------------
        // Auth helpers
        // ---------------------------
        private (int Id, string UserName, string Email) GetSeedUser()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var u = db.Users.SingleOrDefault(x => x.UserName == "seeduser")
                ?? throw new InvalidOperationException("Seed user not found. Ensure ApiFactory seeding runs.");
            return (u.Id, u.UserName, u.Email);
        }

        private void AuthenticateAsUser()
        {
            var u = GetSeedUser();
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                u.Id, u.UserName, u.Email, role: "User");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private void AuthenticateAsAdmin()
        {
            var u = GetSeedUser();
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                u.Id, u.UserName, u.Email, role: "Admin");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Create a disposable user just for a specific test scenario
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
        // Query tests
        // ---------------------------

        [Fact]
        public async Task GetProfile_Returns_SeededUser_As_User()
        {
            AuthenticateAsUser();

            var payload = new { query = "{ profile { userName email } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("seeduser", json);
            Assert.Contains("seed@test.com", json);
        }

        [Fact]
        public async Task GetProfile_Fails_When_Not_Authenticated()
        {
            // no auth header
            var payload = new { query = "{ profile { userName email } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUsers_Returns_SeededUser_As_Admin()
        {
            AuthenticateAsAdmin();

            var payload = new { query = "{ users { userName email } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("seeduser", json);
            Assert.Contains("seed@test.com", json);
        }

        [Fact]
        public async Task GetUsers_Fails_For_Normal_User()
        {
            // create a temp normal user and auth as them
            var temp = CreateTempUser("normal_for_getusers", "normal_for_getusers@test.com");

            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                temp.Id, temp.UserName, temp.Email, role: "User");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new { query = "{ users { userName email } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotContain("seeduser", json);
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUserById_Returns_SeededUser_As_Admin()
        {
            AuthenticateAsAdmin();
            var seed = GetSeedUser();

            var payload = new { query = $"{{ userById(id: {seed.Id}) {{ userName email }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("seeduser", json);
            Assert.Contains("seed@test.com", json);
        }

        [Fact]
        public async Task GetUserById_Fails_For_Normal_User()
        {
            // create a normal user
            var normal = CreateTempUser("normal_for_getuserbyid", "normal_for_getuserbyid@test.com");
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                normal.Id, normal.UserName, normal.Email, role: "User");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // create another victim user (the one normal user should NOT be able to query)
            var victim = CreateTempUser("victim_for_getuserbyid", "victim_for_getuserbyid@test.com");

            var payload = new { query = $"{{ userById(id: {victim.Id}) {{ userName email }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotContain(victim.UserName, json);
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }


        // ---------------------------
        // Mutation tests
        // ---------------------------

        // UPDATE USER
        [Fact]
        public async Task UpdateUser_Allows_Self_Update()
        {
            AuthenticateAsUser();

            var seed = GetSeedUser();
            var payload = new
            {
                query = $"mutation {{ updateUser(id: {seed.Id}, userName: \"updatedName\") {{ userName email }} }}"
            };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("updatedName", json);
        }

        [Fact]
        public async Task UpdateUser_Allows_Admin_Update_Other_User()
        {
            AuthenticateAsAdmin();

            var other = CreateTempUser("otheruser_update", "other_update@test.com");
            var payload = new
            {
                query = $"mutation {{ updateUser(id: {other.Id}, userName: \"adminUpdated\") {{ userName email }} }}"
            };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("adminUpdated", json);
        }

        [Fact]
        public async Task UpdateUser_Fails_For_Normal_User_Updating_Other()
        {
            AuthenticateAsUser();

            var other = CreateTempUser("otheruser_block", "other_block@test.com");
            var payload = new
            {
                query = $"mutation {{ updateUser(id: {other.Id}, userName: \"hacker\") {{ userName email }} }}"
            };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // DELETE USER
        [Fact]
        public async Task DeleteUser_Allows_Self_Delete()
        {
            // create a temp user and auth as that user
            var temp = CreateTempUser("selfdelete", "selfdelete@test.com");
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                temp.Id, temp.UserName, temp.Email, role: "User");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new { query = $"mutation {{ deleteUser(id: {temp.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteUser_Allows_Admin_Delete_Other()
        {
            AuthenticateAsAdmin();

            var other = CreateTempUser("admindelete", "admindelete@test.com");
            var payload = new { query = $"mutation {{ deleteUser(id: {other.Id}) }}" };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task DeleteUser_Fails_For_Normal_User_Deleting_Other()
        {
            // normal user
            var normal = CreateTempUser("normal_for_delete", "normal_for_delete@test.com");
            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                normal.Id, normal.UserName, normal.Email, role: "User");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // try to delete someone else (another temp user)
            var victim = CreateTempUser("victim_delete", "victim_delete@test.com");

            var payload = new { query = $"mutation {{ deleteUser(id: {victim.Id}) }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        // UPDATE PASSWORD
        [Fact]
        public async Task UpdatePassword_Succeeds_For_Self_With_Correct_Current()
        {
            // create temp user with known password
            var temp = CreateTempUser("pw_ok", "pw_ok@test.com", plainPassword: "OldPass123!");

            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                temp.Id, temp.UserName, temp.Email, role: "User");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                query = "mutation { updatePassword(currentPassword:\"OldPass123!\", newPassword:\"NewPass456!\") }"
            };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }

        [Fact]
        public async Task UpdatePassword_Fails_With_Wrong_Current()
        {
            var temp = CreateTempUser("pw_bad", "pw_bad@test.com", plainPassword: "OldPass123!");

            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey, "TestIssuer", "TestAudience",
                temp.Id, temp.UserName, temp.Email, role: "User");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                query = "mutation { updatePassword(currentPassword:\"WRONG!\", newPassword:\"NewPass456!\") }"
            };

            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Current password is incorrect", json, StringComparison.OrdinalIgnoreCase);
        }

        // LOGIN / REGISTER / REFRESH / LOGOUT
        [Fact]
        public async Task RegisterUser_Succeeds_And_Can_Login()
        {
            var userName = $"reg_{Guid.NewGuid():N}".Substring(0, 8);
            var email = $"{userName}@test.com";

            // Register
            var register = new
            {
                query = $"mutation {{ registerUser(username:\"{userName}\", email:\"{email}\", password:\"P@ssw0rd!\") {{ id userName email }} }}"
            };
            var regResp = await _client.PostAsJsonAsync("/graphql", register);
            regResp.EnsureSuccessStatusCode();
            var regJson = await regResp.Content.ReadAsStringAsync();
            Assert.Contains(userName, regJson);
            Assert.Contains(email, regJson);

            // Login
            var login = new
            {
                query = $"mutation {{ loginUser(email:\"{email}\", password:\"P@ssw0rd!\") {{ accessToken refreshToken }} }}"
            };
            var loginResp = await _client.PostAsJsonAsync("/graphql", login);
            loginResp.EnsureSuccessStatusCode();

            var loginJson = await loginResp.Content.ReadAsStringAsync();
            Assert.Contains("accessToken", loginJson);
            Assert.Contains("refreshToken", loginJson);
        }

        [Fact]
        public async Task LoginUser_Fails_With_Wrong_Password()
        {
            var u = CreateTempUser("login_bad", "login_bad@test.com", plainPassword: "RightPass1!");

            var login = new
            {
                query = $"mutation {{ loginUser(email:\"{u.Email}\", password:\"WrongPass!\") {{ accessToken refreshToken }} }}"
            };
            var resp = await _client.PostAsJsonAsync("/graphql", login);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Invalid password", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RefreshToken_Succeeds_With_Valid_Refresh_And_Auth()
        {
            // Create a user with a known password
            var u = CreateTempUser("refresh_me", "refresh_me@test.com", plainPassword: "P@ssw0rd!");

            // Login first to get tokens
            var login = new
            {
                query = $"mutation {{ loginUser(email:\"{u.Email}\", password:\"P@ssw0rd!\") {{ accessToken refreshToken }} }}"
            };
            var loginResp = await _client.PostAsJsonAsync("/graphql", login);
            loginResp.EnsureSuccessStatusCode();
            var loginJson = await loginResp.Content.ReadAsStringAsync();

            // Extract tokens
            using var doc = JsonDocument.Parse(loginJson);
            var data = doc.RootElement.GetProperty("data").GetProperty("loginUser");
            var accessToken = data.GetProperty("accessToken").GetString();
            var refreshToken = data.GetProperty("refreshToken").GetString();
            Assert.False(string.IsNullOrWhiteSpace(accessToken));
            Assert.False(string.IsNullOrWhiteSpace(refreshToken));

            // Auth header with access token
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Call refreshToken
            var refresh = new
            {
                query = $"mutation {{ refreshToken(refreshToken:\"{refreshToken}\") {{ accessToken refreshToken }} }}"
            };
            var refreshResp = await _client.PostAsJsonAsync("/graphql", refresh);
            refreshResp.EnsureSuccessStatusCode();
            var refreshJson = await refreshResp.Content.ReadAsStringAsync();
            Assert.Contains("accessToken", refreshJson);
            Assert.Contains("refreshToken", refreshJson);
        }

        [Fact]
        public async Task RefreshToken_Fails_When_Not_Authenticated()
        {
            // no auth header
            var refresh = new
            {
                query = $"mutation {{ refreshToken(refreshToken:\"bogus\") {{ accessToken refreshToken }} }}"
            };
            var resp = await _client.PostAsJsonAsync("/graphql", refresh);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("not authorized", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LogoutUser_Succeeds_With_Active_RefreshToken()
        {
            // Create temp user, login to get refresh token
            var u = CreateTempUser("logout_me", "logout_me@test.com", plainPassword: "P@ssw0rd!");

            var login = new
            {
                query = $"mutation {{ loginUser(email:\"{u.Email}\", password:\"P@ssw0rd!\") {{ accessToken refreshToken }} }}"
            };
            var loginResp = await _client.PostAsJsonAsync("/graphql", login);
            loginResp.EnsureSuccessStatusCode();
            var loginJson = await loginResp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(loginJson);
            var data = doc.RootElement.GetProperty("data").GetProperty("loginUser");
            var refreshToken = data.GetProperty("refreshToken").GetString();
            Assert.False(string.IsNullOrWhiteSpace(refreshToken));

            var logout = new
            {
                query = $"mutation {{ logoutUser(refreshToken:\"{refreshToken}\") }}"
            };
            var resp = await _client.PostAsJsonAsync("/graphql", logout);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            Assert.Contains("true", json);
        }
    }
}
