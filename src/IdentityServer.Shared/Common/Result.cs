namespace IdentityServer.Shared.Common;

/// <summary>
/// Generic result pattern for handling success/failure responses
/// </summary>
/// <typeparam name="T">The type of data returned on success</typeparam>
public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }

    public static Result<T> Success(T data, string? message = null)
    {
        return new Result<T>
        {
            IsSuccess = true,
            Data = data,
            Message = message
        };
    }

    public static Result<T> Failure(string error)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Errors = new List<string> { error }
        };
    }

    public static Result<T> Failure(List<string> errors)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Errors = errors
        };
    }
}

/// <summary>
/// Non-generic result pattern for operations that don't return data
/// </summary>
public class Result
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }

    public static Result Success(string? message = null)
    {
        return new Result
        {
            IsSuccess = true,
            Message = message
        };
    }

    public static Result Failure(string error)
    {
        return new Result
        {
            IsSuccess = false,
            Errors = new List<string> { error }
        };
    }

    public static Result Failure(List<string> errors)
    {
        return new Result
        {
            IsSuccess = false,
            Errors = errors
        };
    }
}
