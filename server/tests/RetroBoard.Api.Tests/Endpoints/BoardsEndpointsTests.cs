using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RetroBoard.Api.Tests.TestSupport;
using RetroBoard.Application.Common.Dtos;
using Xunit;

namespace RetroBoard.Api.Tests.Endpoints;

[Collection(nameof(PostgresCollection))]
public class BoardsEndpointsTests(PostgresFixture pg)
{
    [Fact]
    public async Task Create_then_get_then_list()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        var create = await http.PostAsJsonAsync("/api/boards", new { name = "Endpoints Retro" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await create.Content.ReadFromJsonAsync<BoardDto>())!;
        dto.Slug.Should().Be("endpoints-retro");

        var get = await http.GetFromJsonAsync<BoardDto>($"/api/boards/{dto.Slug}");
        get!.Id.Should().Be(dto.Id);

        var list = await http.GetFromJsonAsync<List<BoardSummaryDto>>("/api/boards");
        list!.Should().Contain(s => s.Slug == "endpoints-retro");
    }

    [Fact]
    public async Task Duplicate_create_returns_409()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        await http.PostAsJsonAsync("/api/boards", new { name = "Dup Retro" });
        var dup = await http.PostAsJsonAsync("/api/boards", new { name = "Dup Retro" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_missing_returns_404()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();
        var resp = await http.GetAsync("/api/boards/no-such-board");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Head_returns_404_then_200()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var http = factory.CreateClient();

        (await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/boards/headtest"))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        await http.PostAsJsonAsync("/api/boards", new { name = "HeadTest" });
        (await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/boards/headtest"))).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }
}
