using NexMed360Api.Common.ApplicationErrors;
using Microsoft.AspNetCore.Mvc;

namespace NexMed360Api.Common;

public static class InvalidModelStateResponseFactory
{
    public static IActionResult Function(ActionContext context)
    {
        var errors = context.ModelState
            .SelectMany(stt => stt.Value.Errors.Select(err => err.ErrorMessage))
            .Aggregate((errPrev, errCur) => errPrev += errCur);

        return new BadRequestObjectResult(
            Result<string>.Failure(
                new ApiError(
                    ErrorCode.SYS_BadRequest,
                    "One or more validation errors occurred!",
                    400,
                    errors
                    )));
    }
}
