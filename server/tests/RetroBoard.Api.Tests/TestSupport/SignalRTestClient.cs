using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace RetroBoard.Api.Tests.TestSupport;

public static class SignalRTestClient
{
    public static HubConnection Build(ApiFactory factory)
    {
        var server = factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl($"{server.BaseAddress}hubs/board",
                opts =>
                {
                    opts.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    opts.WebSocketFactory = (_, _) => throw new NotSupportedException("Use long polling in tests");
                    opts.SkipNegotiation = false;
                    opts.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
            .Build();
        return connection;
    }
}
