using exchange_simulator.Contracts;
using exchange_simulator.Services;

namespace exchange_simulator.Server;

internal sealed class ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ApiErrorMiddleware> _logger = logger;
    
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
            await WriteEmptyFrameworkErrorAsync(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (MarketFaultedException)
        {
            if (context.Response.HasStarted)
                throw;
            
            context.Response.Clear();

            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse(
                    "MarketUnavailable",
                    "The market is unavailable until the server is restarted"));
        }
        catch (BadHttpRequestException exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(exception, "A bad HTTP request failed after the response had started");
                throw;
            }

            context.Response.Clear();

            if (exception.StatusCode == StatusCodes.Status415UnsupportedMediaType)
            {
                await WriteErrorAsync(context, StatusCodes.Status415UnsupportedMediaType,
                    new ApiErrorResponse(
                        "UnsupportedMediaType",
                        "Request Content-Type must be application/json"));
                return;
            }

            await WriteErrorAsync(context, StatusCodes.Status400BadRequest,
                new ApiErrorResponse(
                    "InvalidRequest",
                    "The HTTP request is invalid"));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "An unhandled exception occurred while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();

            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                new ApiErrorResponse(
                    "InternalServerError",
                    "An internal server error occurred."));
        }
    }

    private static async Task WriteEmptyFrameworkErrorAsync(HttpContext context)
    {
        if (context.Response.HasStarted || context.Response.ContentLength is > 0 || context.Response.ContentType is not null)
            return;

        var error = context.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest =>
                new ApiErrorResponse(
                    "InvalidRequest",
                    "The HTTP request is invalid"),
            StatusCodes.Status404NotFound =>
                new ApiErrorResponse(
                    "RouteNotFound",
                    "The requested route was not found"),
            StatusCodes.Status405MethodNotAllowed =>
                new ApiErrorResponse(
                    "MethodNotAllowed",
                    "The HTTP method is not supported for this route"),
            StatusCodes.Status415UnsupportedMediaType =>
                new ApiErrorResponse(
                    "UnsupportedMediaType",
                    "Request Content-Type must be application/json"),
            StatusCodes.Status500InternalServerError =>
                new ApiErrorResponse(
                    "InternalServerError",
                    "An internal server error occurred"),
            StatusCodes.Status503ServiceUnavailable => 
                new ApiErrorResponse(
                    "MarketUnavailable",
                    "The market is unavailable until the server is restarted"),
            _ => null
        };

        if (error is not null)
            await WriteErrorAsync(context, error);
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, ApiErrorResponse error)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(error);
    }
    
    private static async Task WriteErrorAsync(HttpContext context, ApiErrorResponse error)
    {
        await context.Response.WriteAsJsonAsync(error);
    }
}