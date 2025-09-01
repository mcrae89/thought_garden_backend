using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Api.Tests.Factories;
using ThoughtGarden.Api.Tests.Utils;
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

        private void AuthenticateAsUser()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var seededUser = db.Users.Single(u => u.UserName == "seeduser");

            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey,
                "TestIssuer",
                "TestAudience",
                seededUser.Id,
                seededUser.UserName,
                seededUser.Email,
                "User" // normal user role
            );

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        private void AuthenticateAsAdmin()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var seededUser = db.Users.Single(u => u.UserName == "seeduser");

            var token = JwtTokenGenerator.GenerateToken(
                _factory.JwtKey,
                "TestIssuer",
                "TestAudience",
                seededUser.Id,
                seededUser.UserName,
                seededUser.Email,
                "Admin" // admin role
            );

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        [Fact]
        public async Task GetProfile_Returns_SeededUser_As_User()
        {
            AuthenticateAsUser();

            var payload = new { query = "{ profile { userName email } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var jsonText = await resp.Content.ReadAsStringAsync();

            Assert.Contains("seeduser", jsonText);
            Assert.Contains("seed@test.com", jsonText);
        }

        [Fact]
        public async Task GetUsers_Returns_SeededUser_As_Admin()
        {
            AuthenticateAsAdmin();

            var payload = new { query = "{ users { userName email } }" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var jsonText = await resp.Content.ReadAsStringAsync();

            Assert.Contains("seeduser", jsonText);
            Assert.Contains("seed@test.com", jsonText);
        }

        [Fact]
        public async Task GetUserById_Returns_SeededUser_As_Admin()
        {
            AuthenticateAsAdmin();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
            var seededUser = db.Users.Single(u => u.UserName == "seeduser");

            var payload = new { query = $"{{ userById(id: {seededUser.Id}) {{ userName email }} }}" };
            var resp = await _client.PostAsJsonAsync("/graphql", payload);
            resp.EnsureSuccessStatusCode();

            var jsonText = await resp.Content.ReadAsStringAsync();

            Assert.Contains("seeduser", jsonText);
            Assert.Contains("seed@test.com", jsonText);
        }
    }
}
