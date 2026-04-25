using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RetroBoard.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next();
            logger.LogInformation("{Request} handled in {Elapsed} ms", name, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Request} failed after {Elapsed} ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
