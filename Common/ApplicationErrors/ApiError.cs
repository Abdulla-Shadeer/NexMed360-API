namespace NexMed360Api.Common.ApplicationErrors;

public class ApiError(ErrorCode code, string title, int httpStatus, string? description = null)
{
    public ErrorCode Code { get; set; } = code;
    public string Title { get; set; } = title;
    public string? Description { get; set; } = description;
    public int HttpStatus { get; set; } = httpStatus;
}
