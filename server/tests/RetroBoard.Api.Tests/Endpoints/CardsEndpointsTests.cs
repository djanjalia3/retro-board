using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Endpoints;

[Collection(nameof(PostgresCollection))]
public class CardsEndpointsTests(PostgresFixture pg)
{
    [Fact]
    public async Task Add_then_delete_card_then_vote()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        await http.PostAsJsonAsync("/api/boards", new { name = "Cards Retro" });

        var add = await http.PostAsJsonAsync(
            "/api/boards/cards-retro/cards",
            new { text = "First", author = "Alice", columnIndex = 0 });
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        var card = (await add.Content.ReadFromJsonAsync<CardDto>())!;

        var vote = await http.PostAsJsonAsync(
            $"/api/boards/cards-retro/cards/{card.Id}/votes",
            new { sessionId = "sess-1" });
        vote.StatusCode.Should().Be(HttpStatusCode.OK);
        var voteResult = (await vote.Content.ReadFromJsonAsync<VoteResultDto>())!;
        voteResult.Voted.Should().BeTrue();
        voteResult.Votes.Should().Be(1);

        var del = await http.DeleteAsync($"/api/boards/cards-retro/cards/{card.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
