using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Cards.Commands.CastVote;

public record CastVoteCommand(string Slug, Guid CardId, string SessionId) : IRequest<VoteResultDto>;
