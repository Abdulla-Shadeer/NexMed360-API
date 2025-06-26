using FinalProjectApi.Common.ApplicationErrors;

namespace FinalProjectApi.Common;

public class Result<T>
{
    public bool IsSuccess { get; set; }
    public ApiError? Error { get; set; }
    public T? Data { get; set; }
    public static Result<T> Success(T data)
    {
        return new Result<T>
        {
            IsSuccess = true,
            Data = data
        };
    }
    public static Result<T> Failure(ApiError error)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Error = error
        };
    }

    public static Result<T> Failure(ErrorCode errorCode)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Error = ErrorRegistry.GetError(errorCode)
        };
    }

    public static Result<T> Failure(ErrorCode errorCode, string description)
    {
        var baseError = ErrorRegistry.GetError(errorCode);
        return new Result<T>
        {
            IsSuccess = false,
            Error = new(baseError.Code, baseError.Title, baseError.HttpStatus, description)
        };
    }

    public static Result<T> Failure(ErrorCode errorCode, string description, int httpStatus)
    {
        var baseError = ErrorRegistry.GetError(errorCode);
        return new Result<T>
        {
            IsSuccess = false,
            Error = new(baseError.Code, baseError.Title, httpStatus, description)
        };
    }
}
