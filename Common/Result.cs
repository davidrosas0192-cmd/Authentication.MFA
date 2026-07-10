namespace Authentication.Fido2.Common;

public class Result
{
    public bool IsSuccess { get; protected init; }

    public bool IsFailure => !IsSuccess;

    public string? Message { get; protected init; }

    public string? Error { get; protected init; }

    public int? StatusCode { get; protected init; }

    protected virtual object? ResponseData => null;

    public static Result Success(string? message = null) =>
        new() { IsSuccess = true, Message = message };

    public static Result Failure(string error, int? statusCode = null, string? message = null)
    {
        return new()
        {
            IsSuccess = false,
            Error = error,
            StatusCode = statusCode,
            Message = message,
        };
    }

    public object ToResponsePayload()
    {
        if (IsSuccess)
        {
            return new
            {
                success = true,
                message = Message,
                data = ResponseData,
            };
        }

        return new { success = false, message = Error ?? Message };
    }
}

public class Result<T> : Result
{
    public T? Data { get; protected init; }

    protected override object? ResponseData => Data;

    protected Result() { }

    protected Result(
        T? data,
        bool isSuccess,
        string? error = null,
        string? message = null,
        int? statusCode = null
    )
    {
        Data = data;
        IsSuccess = isSuccess;
        Error = error;
        Message = message;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T data, string? message = null)
    {
        return new(data, true, null, message, null);
    }

    public new static Result<T> Failure(string error, int? statusCode = null, string? message = null)
    {
        return new(default, false, error, message, statusCode);
    }
}
