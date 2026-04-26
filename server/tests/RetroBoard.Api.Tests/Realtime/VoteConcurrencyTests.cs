using System.Net.Http.Json;
using FluentAssertions;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Realtime;

[Collection(nameof(PostgresCollection))]
public class VoteConcurrencyTests(PostgresFixture pg)
{
    [Fact]
    public async Task Same_session_voting_in_parallel_records_one_vote()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "Race Retro" });
        var add = await http.PostAsJsonAsync("/api/boards/race-retro/cards",
            new { text = "Hot", author = "A", columnIndex = 0 });
        var card = (await add.Content.ReadFromJsonAsync<CardDto>())!;

        const int parallelism = 25;
        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => http.PostAsJsonAsync(
                $"/api/boards/race-retro/cards/{card.Id}/votes",
                new { sessionId = "same-sess" }))
            .ToArray();
        await Task.WhenAll(tasks);

        var board = await http.GetFromJsonAsync<BoardDto>("/api/boards/race-retro");
        board!.Cards.Single(c => c.Id == card.Id).Votes.Should().Be(1);
    }

    [Fact]
    public async Task Different_sessions_voting_in_parallel_record_each_vote()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/boards", new { name = "Race2 Retro" });
        var add = await http.PostAsJsonAsync("/api/boards/race2-retro/cards",
            new { text = "Hot", author = "A", columnIndex = 0 });
        var card = (await add.Content.ReadFromJsonAsync<CardDto>())!;

        const int parallelism = 25;
        var tasks = Enumerable.Range(0, parallelism)
            .Select(i => http.PostAsJsonAsync(
                $"/api/boards/race2-retro/cards/{card.Id}/votes",
                new { sessionId = $"sess-{i}" }))
            .ToArray();
        await Task.WhenAll(tasks);

        var board = await http.GetFromJsonAsync<BoardDto>("/api/boards/race2-retro");
        board!.Cards.Single(c => c.Id == card.Id).Votes.Should().Be(parallelism);
    }
}
