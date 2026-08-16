using System.Net;
using System.Text.Json;
using MyAssistant.Application.Common;

namespace MyAssistant.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppError ex)
        {
            await WriteAsync(context, (HttpStatusCode)ex.StatusCode, ApiResponse<object>.Fail(ex.Message, ex.ErrorCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, HttpStatusCode.InternalServerError, ApiResponse<object>.Fail("An unexpected error occurred", "INTERNAL"));
        }
    }

    private static Task WriteAsync(HttpContext context, HttpStatusCode status, ApiResponse<object> body)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}