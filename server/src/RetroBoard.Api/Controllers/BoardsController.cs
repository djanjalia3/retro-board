using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetroBoard.Application.Boards.Commands.CreateBoard;
using RetroBoard.Application.Boards.Commands.ImportBoard;
using RetroBoard.Application.Boards.Queries.BoardExists;
using RetroBoard.Application.Boards.Queries.GetBoard;
using RetroBoard.Application.Boards.Queries.ListBoards;

namespace RetroBoard.Api.Controllers;

[ApiController]
[Route("api/boards")]
public class BoardsController(IMediator mediator) : ControllerBase
{
    public record CreateBoardRequest(string Name, IReadOnlyList<string>? Columns);
    public record ImportBoardRequest(
        string Name,
        IReadOnlyList<string> Columns,
        IReadOnlyList<ImportedCard> Cards);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBoardRequest req, CancellationToken ct)
    {
        var dto = await mediator.Send(new CreateBoardCommand(req.Name, req.Columns), ct);
        return CreatedAtAction(nameof(Get), new { slug = dto.Slug }, dto);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportBoardRequest req, CancellationToken ct)
    {
        var dto = await mediator.Send(new ImportBoardCommand(req.Name, req.Columns, req.Cards), ct);
        return CreatedAtAction(nameof(Get), new { slug = dto.Slug }, dto);
    }

    [HttpGet]
    public Task<IReadOnlyList<Application.Common.Dtos.BoardSummaryDto>> List(CancellationToken ct) =>
        mediator.Send(new ListBoardsQuery(), ct);

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetBoardQuery(slug), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpHead("{slug}")]
    public async Task<IActionResult> Head(string slug, CancellationToken ct) =>
        await mediator.Send(new BoardExistsQuery(slug), ct) ? Ok() : NotFound();
}
