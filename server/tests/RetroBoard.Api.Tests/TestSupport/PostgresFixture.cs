using Testcontainers.PostgreSql;
using Xunit;

namespace RetroBoard.Api.Tests.TestSupport;

public class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithUsername("retro")
        .WithPassword("retro")
        .WithDatabase("retroboard_test")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync() => Container.StopAsync();
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture> { }
