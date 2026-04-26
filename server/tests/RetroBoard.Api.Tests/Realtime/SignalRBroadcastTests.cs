using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Realtime;

[Collection(nameof(PostgresCollection))]
public class SignalRBroadcastTests(PostgresFixture pg)
{
    [Fact]
    public async Task CardAdded_received_by_subscribed_client()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "RT Retro" });

        await using var hub = SignalRTestClient.Build(factory);
        var tcs = new TaskCompletionSource<CardDto>();
        hub.On<CardDto>("CardAdded", c => tcs.TrySetResult(c));
        await hub.StartAsync();
        await hub.InvokeAsync<List<ParticipantDto>>("JoinBoard", "rt-retro", "sess-1", "Alice");

        await http.PostAsJsonAsync("/api/boards/rt-retro/cards",
            new { text = "Hello", author = "Alice", columnIndex = 0 });

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Text.Should().Be("Hello");
    }

    [Fact]
    public async Task PresenceChanged_received_when_second_client_joins()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "Pres Retro" });

        await using var alice = SignalRTestClient.Build(factory);
        var presenceCount = new TaskCompletionSource<int>();
        alice.On<List<ParticipantDto>>("PresenceChanged", list =>
        {
            if (list.Count >= 2) presenceCount.TrySetResult(list.Count);
        });
        await alice.StartAsync();
        await alice.InvokeAsync<List<ParticipantDto>>("JoinBoard", "pres-retro", "sess-a", "Alice");

        await using var bob = SignalRTestClient.Build(factory);
        await bob.StartAsync();
        await bob.InvokeAsync<List<ParticipantDto>>("JoinBoard", "pres-retro", "sess-b", "Bob");

        var count = await presenceCount.Task.WaitAsync(TimeSpan.FromSeconds(5));
        count.Should().BeGreaterThanOrEqualTo(2);
    }
}
