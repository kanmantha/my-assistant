namespace MyAssistant.Application.Common;

/// <summary>
/// Simple discriminated result used across the application layer.
/// </summary>
public class Result
{
    public bool Succeeded { get; protected set; }

    public string? Error { get; protected set; }

    public int StatusCode { get; protected set; } = 400;

    protected Result() { }

    public static Result Ok() => new() { Succeeded = true };

    public static Result<T> Ok<T>(T value) => new() { Succeeded = true, Value = value };

    public static Result Fail(string error, int statusCode = 400) =>
        new() { Succeeded = false, Error = error, StatusCode = statusCode };

    public static Result<T> Fail<T>(string error, int statusCode = 400) =>
        new() { Succeeded = false, Error = error, StatusCode = statusCode };
}

public class Result<T> : Result
{
    public T? Value { get; set; }
}
