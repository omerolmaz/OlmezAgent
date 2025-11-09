namespace Server.Application.Common;

/// <summary>
/// Result pattern for service operations
/// </summary>
public class Result<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }

    public static Result<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static Result<T> Fail(string errorMessage, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}

/// <summary>
/// Result pattern without data
/// </summary>
public class Result
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }

    public static Result Ok() => new() { Success = true };

    public static Result Fail(string errorMessage, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
