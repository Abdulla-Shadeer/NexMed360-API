namespace FinalProjectApi.Common.ApplicationErrors;

public class ApiError(ErrorCode code, string message, int httpStatus)
{
    public ErrorCode Code { get; set; } = code;
    public string Message { get; set; } = message;
    public int HttpStatus { get; set; } = httpStatus;
}
