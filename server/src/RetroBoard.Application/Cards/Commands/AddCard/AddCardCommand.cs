using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Cards.Commands.AddCard;

public record AddCardCommand(string Slug, string Text, string Author, int ColumnIndex) : IRequest<CardDto>;
