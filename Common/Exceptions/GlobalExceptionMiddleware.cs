
using FinalProjectApi.Common.ApplicationErrors;

namespace FinalProjectApi.Common.Exceptions;

public class GlobalExceptionMiddleware(ILogger<GlobalExceptionMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
		try
		{
			await next(context);
		}
		catch (Exception e)
		{
			var error = new ApiError(
				ErrorCode.Internal, 
				"An exception occurred while processing the request.",
				500,
				e.Message ?? e.InnerException?.Message);
			logger.LogWarning($"GlobalExceptionMiddleware => An exception occurred while processing the request.\n\t{e.Message}");
			await context.Response.WriteAsJsonAsync(Result<object>.Failure(error));
		}
    }
}
