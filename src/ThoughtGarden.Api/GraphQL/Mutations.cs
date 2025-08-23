namespace ThoughtGarden.Api.GraphQL;

public class Mutations
{
    public string Ping(string message) => $"You said: {message}";
}
