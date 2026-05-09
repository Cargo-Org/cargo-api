using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cargo.BuildingBlocks.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Handling {RequestName}", requestName);

        var timer = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        timer.Stop();

        if (timer.ElapsedMilliseconds > 500)
        {
            logger.LogWarning(
                "Long running request: {RequestName} took {ElapsedMs}ms",
                requestName, timer.ElapsedMilliseconds);
        }

        if (response.IsError)
        {
            // Log codes so you know WHICH errors occurred, not just how many.
            var errorCodes = string.Join(", ", response.Errors?.Select(e => e.Code) ?? []);
            logger.LogWarning(
                "Request {RequestName} failed with {ErrorCount} error(s): {ErrorCodes}",
                requestName, response.Errors?.Count ?? 0, errorCodes);
        }
        else if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Request {RequestName} completed in {ElapsedMs}ms",
                requestName, timer.ElapsedMilliseconds);
        }

        return response;
    }
}