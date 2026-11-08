using NexMed360Api.Common.ApplicationErrors;

namespace NexMed360Api.Common;

public class Result<T>
{
    public bool IsSuccess { get; set; }
    public ApiError? Error { get; set; }
    public T? Data { get; set; }

    // Returning data means success; returning an error means failure.
    public static implicit operator Result<T>(T? data) => Success(data);
    public static implicit operator Result<T>(ApiError error) => Failure(error);
    public static implicit operator Result<T>(ErrorCode errorCode) => Failure(errorCode);

    public static Result<T> Success(T? data)
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
        return Failure(ErrorRegistry.GetError(errorCode));
    }

    public static Result<T> Failure(ErrorCode errorCode, string description)
    {
        return Failure(ApiError.From(errorCode, description));
    }

    public static Result<T> Failure(ErrorCode errorCode, string description, int httpStatus)
    {
        return Failure(ApiError.From(errorCode, description, httpStatus));
    }
}
