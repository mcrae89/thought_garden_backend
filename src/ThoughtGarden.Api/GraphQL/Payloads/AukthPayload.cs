namespace ThoughtGarden.Api.GraphQL.Payloads
{
    public class AuthPayload
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}
