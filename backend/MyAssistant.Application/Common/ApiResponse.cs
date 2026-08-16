namespace MyAssistant.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "") => new() { Success = true, Data = data, Message = message };
    public static ApiResponse<T> Fail(string message, string? errorCode = null) => new() { Success = false, Message = message, ErrorCode = errorCode };
}

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public class AppError : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }

    public AppError(string message, int statusCode = 400, string? errorCode = null) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}