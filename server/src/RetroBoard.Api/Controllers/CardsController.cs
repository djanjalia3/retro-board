using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetroBoard.Application.Cards.Commands.AddCard;
using RetroBoard.Application.Cards.Commands.DeleteCard;

namespace RetroBoard.Api.Controllers;

[ApiController]
[Route("api/boards/{slug}/cards")]
public class CardsController(IMediator mediator) : ControllerBase
{
    public record AddCardRequest(string Text, string Author, int ColumnIndex);

    [HttpPost]
    public async Task<IActionResult> Add(string slug, [FromBody] AddCardRequest req, CancellationToken ct)
    {
        var dto = await mediator.Send(new AddCardCommand(slug, req.Text, req.Author, req.ColumnIndex), ct);
        return CreatedAtAction(null, new { slug, cardId = dto.Id }, dto);
    }

    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> Delete(string slug, Guid cardId, CancellationToken ct)
    {
        await mediator.Send(new DeleteCardCommand(slug, cardId), ct);
        return NoContent();
    }
}
