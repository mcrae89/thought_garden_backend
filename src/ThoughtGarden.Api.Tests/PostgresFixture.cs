using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Xunit;

namespace ThoughtGarden.Api.Tests
{
    public class PostgresFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgresContainer;

        public string ConnectionString => _postgresContainer.GetConnectionString();

        public PostgresFixture()
        {
            _postgresContainer = new PostgreSqlBuilder()
                .WithDatabase("thoughtgarden")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithCleanUp(true)   // remove container after test run
                .Build();
        }

        public async Task InitializeAsync()
        {
            await _postgresContainer.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _postgresContainer.DisposeAsync();
        }
    }
}
