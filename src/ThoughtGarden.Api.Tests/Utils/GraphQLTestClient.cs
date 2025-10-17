using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ThoughtGarden.Api.Tests.Factories;

namespace ThoughtGarden.Api.Tests.Utils
{
    public static class GraphQLTestClient
    {
        public static Task<HttpResponseMessage> PostGraphQLAsync(HttpClient client, object payload, string? bearer = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = JsonContent.Create(payload) };
            if (!string.IsNullOrEmpty(bearer)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return client.SendAsync(req);
        }

        public static string GenerateBearer(ApiFactory factory, (int Id, string UserName, string Email) user, string role)
        {
            return JwtTokenGenerator.GenerateToken(factory.JwtKey, "TestIssuer", "TestAudience", user.Id, user.UserName, user.Email, role: role);
        }

        public static Task<HttpResponseMessage> PostAsUserAsync(HttpClient client, ApiFactory factory, object payload, (int Id, string UserName, string Email) user, string role)
        {
            var token = GenerateBearer(factory, user, role);
            return PostGraphQLAsync(client, payload, bearer: token);
        }
    }
}
