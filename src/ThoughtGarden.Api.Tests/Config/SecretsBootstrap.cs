using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace ThoughtGarden.Api.Config;

public static class SecretsBootstrap
{
    public static void Apply(WebApplicationBuilder builder)
    {
        // Tests should be hermetic: skip both .env and Doppler when Testing.
        var isTesting = string.Equals(builder.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        if (isTesting) return;

        // 1) Load .env for Development only
        if (builder.Environment.IsDevelopment())
        {
            var envPath = FindUp(builder.Environment.ContentRootPath, ".env");
            if (envPath is not null)
            {
                DotEnv.Load(envPath); // loads into process env vars
            }
        }

        // 2) Layer Doppler on top (dev/prod), if present
        var dopplerToken = Environment.GetEnvironmentVariable("DOPPLER_TOKEN");
        if (!string.IsNullOrWhiteSpace(dopplerToken))
        {
            builder.Configuration.AddDopplerSecrets(); // merges, case-insensitive keys
        }
    }

    private static string? FindUp(string startDir, string file)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, file);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent!;
        }
        return null;
    }
}
