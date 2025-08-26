namespace ThoughtGarden.Api.GraphQL;

[ExtendObjectType("Mutation")]
public class RootMutations
{
    public string Ping(string message) => $"You said: {message}";
}
