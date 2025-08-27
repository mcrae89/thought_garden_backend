using System.Reflection;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    public class ServerInfo
    {
        public string Status { get; set; } = "Healthy";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string TimeZone { get; set; } = TimeZoneInfo.Local.DisplayName;
        public string Version { get; set; } = "unknown";
    }

    [ExtendObjectType("Query")]
    public class ServerInfoQueries
    {
        public ServerInfo GetServerInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "unknown";

            return new ServerInfo
            {
                Status = "Healthy",
                Version = version
            };
        }
    }
}
