using System.Net.Http.Headers;
using System.Text.Json;

namespace ThoughtGarden.Api.Config
{
    public static class DopplerConfigExtensions
    {
        public static IConfigurationBuilder AddDopplerSecrets(this IConfigurationBuilder builder)
            => builder.Add(new DopplerConfigurationSource());
    }

    sealed class DopplerConfigurationSource : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new DopplerConfigurationProvider();
    }

    sealed class DopplerConfigurationProvider : ConfigurationProvider
    {
        public override void Load()
        {
            var token = Environment.GetEnvironmentVariable("DOPPLER_TOKEN");
            var project = Environment.GetEnvironmentVariable("DOPPLER_PROJECT");
            var config = Environment.GetEnvironmentVariable("DOPPLER_CONFIG");
            if (string.IsNullOrWhiteSpace(token)) return;

            var baseUri = "https://api.doppler.com/v3/configs/config/secrets/download?format=json";
            var uri = (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(config))
                ? $"{baseUri}&project={Uri.EscapeDataString(project)}&config={Uri.EscapeDataString(config)}"
                : baseUri;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var json = http.GetStringAsync(uri).GetAwaiter().GetResult();

            var flat = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();

            // Normalize __ → : and store with a case-insensitive comparer
            var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in flat)
            {
                var key = k.Replace("__", ":", StringComparison.Ordinal);
                normalized[key] = v;
            }

            // IMPORTANT: keep Data case-insensitive
            Data = normalized;
        }
    }

}
