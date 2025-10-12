// src/ThoughtGarden.Api/GraphQL/Queries/ServerInfoQueries.cs
using ThoughtGarden.Api.Infrastructure;

namespace ThoughtGarden.Api.GraphQL.Queries;

[ExtendObjectType("Query")]
public sealed class ServerInfoQueries
{
    public ServerInfo ServerInfo([Service] IServerInfoProvider provider) => provider.Get();
}
