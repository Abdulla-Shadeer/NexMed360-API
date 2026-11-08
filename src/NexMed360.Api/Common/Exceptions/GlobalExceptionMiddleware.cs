using NexMed360Api.Common.ApplicationErrors;

namespace NexMed360Api.Common.Exceptions;

public class GlobalExceptionMiddleware(ILogger<GlobalExceptionMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (UnauthorizedAccessException exception)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(Result<object>.Failure(ErrorCode.SYS_Unauthorized, exception.Message, 403));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not complete the request.");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(Result<object>.Failure(ErrorCode.Internal, "Could not complete the request. Please try again.", 500));
        }
    }
}
