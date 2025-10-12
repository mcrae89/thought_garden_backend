// src/ThoughtGarden.Api/Infrastructure/ServerInfo.cs
using System.Reflection;

namespace ThoughtGarden.Api.Infrastructure;

public record ServerInfo(
    string status,
    DateTime timestampUtc,
    string timeZoneId,
    string version,
    string environment,
    double uptimeSeconds,
    string? commitSha
);

public interface IServerInfoProvider
{
    ServerInfo Get();
}

public sealed class ServerInfoProvider(IHostEnvironment env) : IServerInfoProvider
{
    private static readonly DateTime StartedUtc = DateTime.UtcNow;

    public ServerInfo Get()
    {
        var asm = Assembly.GetExecutingAssembly();
        var version =
            asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";

        var commit = Environment.GetEnvironmentVariable("BUILD_SHA")
                    ?? Environment.GetEnvironmentVariable("GITHUB_SHA");

        return new ServerInfo(
            status: "Healthy",
            timestampUtc: DateTime.UtcNow,
            timeZoneId: TimeZoneInfo.Local.Id,
            version: version,
            environment: env.EnvironmentName,
            uptimeSeconds: (DateTime.UtcNow - StartedUtc).TotalSeconds,
            commitSha: commit
        );
    }
}
