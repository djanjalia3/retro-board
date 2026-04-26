using MediatR;
using Microsoft.AspNetCore.Mvc;
using RetroBoard.Application.Cards.Commands.CastVote;

namespace RetroBoard.Api.Controllers;

[ApiController]
[Route("api/boards/{slug}/cards/{cardId:guid}/votes")]
public class VotesController(IMediator mediator) : ControllerBase
{
    public record CastVoteRequest(string SessionId);

    [HttpPost]
    public Task<Application.Common.Dtos.VoteResultDto> Cast(
        string slug, Guid cardId, [FromBody] CastVoteRequest req, CancellationToken ct) =>
        mediator.Send(new CastVoteCommand(slug, cardId, req.SessionId), ct);
}
