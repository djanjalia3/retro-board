using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Presence.Commands.JoinBoard;
using RetroBoard.Application.Presence.Commands.LeaveBoard;
using RetroBoard.Application.Presence.Commands.RefreshPresence;

namespace RetroBoard.Api.Hubs;

public class BoardHub(IMediator mediator) : Hub<IBoardHubClient>
{
    private const string GroupsKey = "groups";

    public static string GroupName(string slug) => $"board:{slug}";

    public async Task<IReadOnlyList<ParticipantDto>> JoinBoard(string slug, string sessionId, string displayName)
    {
        var result = await mediator.Send(new JoinBoardCommand(slug, Context.ConnectionId, sessionId, displayName));
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(slug));
        var groups = (HashSet<string>?)Context.Items[GroupsKey] ?? new HashSet<string>();
        groups.Add(slug);
        Context.Items[GroupsKey] = groups;
        return result.Participants;
    }

    public async Task LeaveBoard(string slug)
    {
        await mediator.Send(new LeaveBoardCommand(slug, Context.ConnectionId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(slug));
        if (Context.Items[GroupsKey] is HashSet<string> groups)
            groups.Remove(slug);
    }

    public Task Heartbeat() =>
        mediator.Send(new RefreshPresenceCommand(Context.ConnectionId));

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[GroupsKey] is HashSet<string> groups)
        {
            foreach (var slug in groups.ToList())
                await mediator.Send(new LeaveBoardCommand(slug, Context.ConnectionId));
        }
        await base.OnDisconnectedAsync(exception);
    }
}
